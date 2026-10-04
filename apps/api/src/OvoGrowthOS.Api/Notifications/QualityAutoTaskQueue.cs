using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

// Opens one follow-up task per brand whose previous month is still missing sources.
// Runs once per period (audit marker), creates nothing when there is nothing to follow up
// and never changes period data, readiness or financial results.
public sealed class QualityAutoTaskQueue(AppDbContext db)
{
    private const string SummaryAction = "QualityAutoTaskSummary";
    private const string SystemUser = "sistem (otomatik)";
    private const int FirstDays = 7;

    public async Task<int> RunDue(DateTimeOffset now, CancellationToken ct = default)
    {
        var trNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
        if (trNow.Day > FirstDays) return 0;
        var target = trNow.Date.AddMonths(-1);
        var periodKey = $"{target.Year:0000}-{target.Month:00}";
        if (await db.AuditRecords.AsNoTracking()
            .AnyAsync(x => x.Action == SummaryAction && x.EntityId == periodKey, ct)) return 0;

        var report = await WorkflowEndpoints.BuildQualityReport(db, target.Year, target.Month, null);
        var missing = report.Brands
            .Where(x => x.Readiness == "missing" && x.DealId is not null && x.TaskId is null)
            .ToList();

        var users = await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && !x.InvitationPending && x.Role != "BrandClient")
            .OrderBy(x => x.Email).ToListAsync(ct);
        var byId = users.ToDictionary(x => x.Id);
        var fallback = users.FirstOrDefault(x => x.Role == "Admin") ?? users.FirstOrDefault();
        var alreadyTaken = (await db.WorkTasks.AsNoTracking()
            .Where(x => x.Kind == WorkKind.MonthlyClose && x.Year == target.Year && x.Month == target.Month)
            .Select(x => x.BrandId).ToListAsync(ct)).ToHashSet();

        var created = 0;
        var skipped = 0;
        foreach (var item in missing)
        {
            if (alreadyTaken.Contains(item.BrandId)) { skipped++; continue; }
            var assignee = item.ResponsibleId is { } responsible && byId.ContainsKey(responsible)
                ? responsible
                : fallback?.Id;
            if (assignee is null) { skipped++; continue; }
            var task = new WorkTask
            {
                Id = Guid.NewGuid(),
                BrandId = item.BrandId,
                AssigneeId = assignee.Value,
                Title = $"{target.Month}/{target.Year} eksik kaynak takibi",
                Description = WorkflowEndpoints.TaskDescription(item, report.Period),
                Priority = WorkPriority.High,
                Kind = WorkKind.MonthlyClose,
                DealId = item.DealId,
                Year = target.Year,
                Month = target.Month,
                DueOn = FifthOfNextMonth(target.Year, target.Month),
                CreatedBy = SystemUser
            };
            db.WorkTasks.Add(task);
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = SystemUser, Action = "WorkTaskCreated", EntityType = "WorkTask",
                EntityId = task.Id.ToString(),
                NewValueJson = JsonSerializer.Serialize(new { task.Title, task.BrandId, task.AssigneeId, automatic = true }),
                Reason = $"Otomatik veri kalitesi kontrolü ({periodKey})"
            });
            created++;
        }

        db.AuditRecords.Add(new AuditRecord
        {
            UserId = SystemUser, Action = SummaryAction, EntityType = "Period", EntityId = periodKey,
            NewValueJson = JsonSerializer.Serialize(new { missing = missing.Count, created, skipped }),
            Reason = "Otomatik eksik veri takip görevi özeti"
        });
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static DateOnly FifthOfNextMonth(int year, int month)
    {
        var next = month == 12 ? new DateOnly(year + 1, 1, 1) : new DateOnly(year, month + 1, 1);
        return new DateOnly(next.Year, next.Month, 5);
    }
}
