using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record TimeEntryRequest(Guid Id, DateOnly WeekStart, decimal Hours, string Note);
public sealed record TimeToCostRequest(Guid Id, Guid TimeEntryId, decimal HourlyCost, DateOnly IncurredOn, string Reference, string Description, bool Confirmed, int Revision);

public static partial class WorkflowEndpoints
{
    private static void MapTimeTracking(WebApplication app)
    {
        app.MapGet("/api/work-tasks/{id:guid}/time-entries", ReadTimeEntries).RequireAuthorization("ReadAccess");
        app.MapPost("/api/work-tasks/{id:guid}/time-entries", AddTimeEntry).AddEndpointFilter<ValidationFilter<TimeEntryRequest>>().RequireAuthorization("ReadAccess");
        app.MapPost("/api/work-tasks/{id:guid}/time-entries/{entryId:guid}/void", VoidTimeEntry).AddEndpointFilter<ValidationFilter<VoidPaymentRequest>>().RequireAuthorization("OperationsWrite");
    }

    private static bool MayLogTime(ClaimsPrincipal user, WorkTask task)
    {
        var actor = Guid.Parse(user.FindFirstValue("uid")!);
        return user.IsInRole("Admin") || user.IsInRole("Partner") || task.AssigneeId == actor;
    }

    private static async Task<IResult> ReadTimeEntries(Guid id, AppDbContext db, ClaimsPrincipal user)
    {
        var task = await db.WorkTasks.AsNoTracking().Include(x => x.Brand).Include(x => x.Assignee)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (task is null) return Results.NotFound();
        var actor = Guid.Parse(user.FindFirstValue("uid")!);
        var entries = await db.TaskTimeEntries.AsNoTracking().Where(x => x.TaskId == id)
            .OrderByDescending(x => x.WeekStart).ThenByDescending(x => x.CreatedAt).ToListAsync();
        var entryIds = entries.Select(x => x.Id).ToList();
        var userIds = entries.Select(x => x.UserId).Distinct().ToList();
        var users = await db.UserAccounts.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var periods = await db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.BrandId == task.BrandId && (x.Status == MonthlyPerformanceStatus.Locked
                || x.Status == MonthlyPerformanceStatus.Invoiced || x.Status == MonthlyPerformanceStatus.Paid))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Select(x => new { x.Id, x.Year, x.Month }).ToListAsync();
        var periodIds = periods.Select(x => x.Id).ToList();
        var costs = await db.ServiceCostEntries.AsNoTracking()
            .Where(x => x.VoidedAt == null && x.SourceTimeEntryId != null && periodIds.Contains(x.MonthlyPerformanceId))
            .Select(x => new { x.SourceTimeEntryId, x.MonthlyPerformanceId }).ToListAsync();
        var periodLabel = periods.ToDictionary(x => x.Id, x => $"{x.Month}/{x.Year}");
        var costLabels = costs.Where(x => x.SourceTimeEntryId is not null && entryIds.Contains(x.SourceTimeEntryId.Value))
            .ToDictionary(x => x.SourceTimeEntryId!.Value, x => periodLabel.GetValueOrDefault(x.MonthlyPerformanceId, ""));
        var plans = await db.TaskHourPlans.AsNoTracking().Where(x => x.TaskId == id).ToListAsync();
        var revisions = await db.ServiceCostAccounts.AsNoTracking()
            .Where(x => periodIds.Contains(x.MonthlyPerformanceId))
            .ToDictionaryAsync(x => x.MonthlyPerformanceId, x => x.Revision);
        return Results.Ok(new
        {
            taskId = task.Id, brandId = task.BrandId, brandName = task.Brand?.Name, title = task.Title,
            assigneeId = task.AssigneeId, assigneeName = task.Assignee?.Name, dueOn = task.DueOn,
            completedAt = task.CompletedAt, canLog = MayLogTime(user, task),
            isManager = user.IsInRole("Admin") || user.IsInRole("Partner"),
            self = actor, summary = TimeTracking.Summary(plans, entries),
            entries = entries.Select(x => new
            {
                x.Id, x.UserId, userName = users.GetValueOrDefault(x.UserId, ""), x.WeekStart, x.Hours, x.Note,
                x.CreatedBy, x.CreatedAt, x.VoidedAt, x.VoidReason,
                converted = costLabels.ContainsKey(x.Id), costLabel = costLabels.GetValueOrDefault(x.Id)
            }),
            periods = periods.Select(x => new
            {
                x.Id, x.Year, x.Month, label = $"{x.Month}/{x.Year}", revision = revisions.GetValueOrDefault(x.Id, 0)
            })
        });
    }

    private static async Task<IResult> AddTimeEntry(Guid id, TimeEntryRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (task is null) return Results.NotFound();
        if (!MayLogTime(user, task)) return Results.Forbid();
        if (!WorkPlanning.ValidWeek(r.WeekStart))
            return Results.BadRequest(new { error = "Hafta başlangıcı Pazartesi olmalı ve 2020 ile 2100 yılları arasında bulunmalıdır." });
        var hoursError = TimeTracking.HoursError(r.Hours);
        if (hoursError is not null) return Results.BadRequest(new { error = hoursError });
        if (await db.TaskTimeEntries.AnyAsync(x => x.Id == r.Id))
            return Results.Conflict(new { error = "Bu saat girişi daha önce kaydedildi. Listeyi yenileyin." });
        var entry = new TaskTimeEntry
        {
            Id = r.Id, TaskId = id, UserId = Guid.Parse(user.FindFirstValue("uid")!), WeekStart = r.WeekStart,
            Hours = r.Hours, Note = (r.Note ?? "").Trim(), CreatedBy = User(user)
        };
        db.TaskTimeEntries.Add(entry);
        Audit(db, user, "TimeEntryAdded", "WorkTask", id, null, new { entry.Id, entry.WeekStart });
        await db.SaveChangesAsync();
        return Results.Created($"/api/work-tasks/{id}/time-entries", entry);
    }

    private static async Task<IResult> VoidTimeEntry(Guid id, Guid entryId, VoidPaymentRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (task is null) return Results.NotFound();
        var actor = Guid.Parse(user.FindFirstValue("uid")!);
        var entry = await db.TaskTimeEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.TaskId == id);
        if (entry is null) return Results.NotFound();
        if (!user.IsInRole("Admin") && !user.IsInRole("Partner") && entry.UserId != actor) return Results.Forbid();
        if (entry.VoidedAt is not null) return Results.Conflict(new { error = "Bu saat girişi zaten iptal edilmiş." });
        if (await db.ServiceCostEntries.AnyAsync(x => x.SourceTimeEntryId == entry.Id && x.VoidedAt == null))
            return Results.Conflict(new { error = "Bu saat girişi hizmet maliyetine aktarılmış. Saati iptal etmek için önce maliyet kaydını yönetici olarak iptal edin." });
        entry.VoidedAt = DateTimeOffset.UtcNow; entry.VoidedBy = User(user); entry.VoidReason = r.Reason.Trim();
        Audit(db, user, "TimeEntryVoided", "WorkTask", id, null, new { entry.Id, reason = entry.VoidReason });
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> AddCostFromTime(Guid id, TimeToCostRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var entry = await db.TaskTimeEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.TimeEntryId);
        if (entry is null) return Results.NotFound();
        if (entry.VoidedAt is not null) return Results.Conflict(new { error = "İptal edilmiş saat girişi maliyete aktarılamaz." });
        if (r.IncurredOn < entry.WeekStart)
            return Results.BadRequest(new { error = "Gider tarihi saat girişinin haftasından önce olamaz." });
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == entry.TaskId);
        if (task is null) return Results.NotFound();
        var period = await db.MonthlyPerformances.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (period is null) return Results.NotFound();
        if (period.BrandId != task.BrandId)
            return Results.Conflict(new { error = "Saat girişi bu döneme ait markanın görevinden gelmiyor." });
        if (await db.ServiceCostEntries.AnyAsync(x => x.SourceTimeEntryId == entry.Id && x.VoidedAt == null))
            return Results.Conflict(new { error = "Bu saat girişi daha önce maliyete aktarılmış. Aynı çalışma iki kez maliyet olmaz." });
        var request = new ServiceCostRequest(r.Id, ServiceCostKind.TeamWork, 0, entry.Hours, r.HourlyCost,
            r.IncurredOn, r.Reference, r.Description, r.Revision);
        return await AddServiceCostCore(id, request, db, user, entry.Id);
    }
}
