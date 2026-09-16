using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record TemplateScope([property: JsonRequired] WorkTemplateKind Kind, Guid? DealId, int Year, int Month, DateOnly StartOn);
public sealed record TemplateAssignment(string Step, Guid AssigneeId, DateOnly DueOn, Guid? ExistingTaskId, int? ExistingTaskRevision);
public sealed record TemplateApplyRequest(TemplateScope Scope, IReadOnlyList<TemplateAssignment> Items);
public sealed record WeeklyCapacityRequest(DateOnly WeekStart, [property: JsonRequired] decimal WorkingHours,
    [property: JsonRequired] decimal UnavailableHours, [property: JsonRequired] int Revision, string Reason);
public sealed record TaskHourPlanRequest(DateOnly WeekStart, [property: JsonRequired] decimal Hours, [property: JsonRequired] int Revision,
    [property: JsonRequired] int TaskRevision, string Reason);

public static partial class WorkflowEndpoints
{
    private static void MapWorkPlanning(WebApplication app)
    {
        app.MapPost("/api/brands/{id:guid}/work-template/preview", PreviewWorkTemplate).AddEndpointFilter<ValidationFilter<TemplateScope>>().RequireAuthorization("OperationsWrite");
        app.MapPost("/api/brands/{id:guid}/work-template/apply", ApplyWorkTemplate).AddEndpointFilter<ValidationFilter<TemplateApplyRequest>>().RequireAuthorization("OperationsWrite");
        app.MapGet("/api/work-planning", ReadWorkPlanning).RequireAuthorization("ReadAccess");
        app.MapPut("/api/team/{id:guid}/capacity", SaveWeeklyCapacity).AddEndpointFilter<ValidationFilter<WeeklyCapacityRequest>>().RequireAuthorization("OperationsWrite");
        app.MapGet("/api/work-tasks/{id:guid}/hour-plan", ReadTaskHourPlan).RequireAuthorization("ReadAccess");
        app.MapPut("/api/work-tasks/{id:guid}/hour-plan", SaveTaskHourPlan).AddEndpointFilter<ValidationFilter<TaskHourPlanRequest>>().RequireAuthorization("OperationsWrite");
    }

    private static async Task<string?> TemplateScopeError(Guid brandId, TemplateScope r, AppDbContext db)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return "Marka bulunamadı.";
        if (r.DealId.HasValue && !await db.Deals.AnyAsync(x => x.Id == r.DealId && x.BrandId == brandId)) return "Anlaşma bu markaya ait değil.";
        if (await db.WorkTemplateRuns.AnyAsync(x => x.BrandId == brandId && x.Kind == r.Kind && x.Year == r.Year && x.Month == r.Month))
            return "Bu şablon bu marka ve kapsam için zaten uygulandı. Mevcut görevleri açın; tamamlananları gerekirse yeniden açabilirsiniz.";
        return null;
    }

    private static Task<WorkTask?> ExistingTemplateClose(Guid brandId, TemplateScope r, AppDbContext db) =>
        r.Kind == WorkTemplateKind.MonthlyClose ? db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId && x.Kind == WorkKind.MonthlyClose && x.Year == r.Year && x.Month == r.Month)
            : Task.FromResult<WorkTask?>(null);

    private static async Task<IResult> PreviewWorkTemplate(Guid id, TemplateScope r, AppDbContext db)
    {
        var error = await TemplateScopeError(id, r, db); if (error is not null) return Results.Conflict(new { error });
        var existing = await ExistingTemplateClose(id, r, db);
        if (existing is not null && existing.DealId != r.DealId) return Results.Conflict(new { error = "Bu ayın kapanış işi başka bir anlaşmaya bağlı. Mevcut işi kontrol edin; yeni kapanış işi oluşturulmadı." });
        return Results.Ok(new { scope = r, items = WorkPlanning.Steps(r.Kind).Select(step => new {
            step.Key, step.Title, step.Description, dueOn = r.StartOn.AddDays(step.DaysAfterStart),
            existing = step.Kind == WorkKind.MonthlyClose && existing is not null
                ? new { existing.Id, existing.Revision, existing.Title, existing.AssigneeId, existing.DueOn, existing.CompletedAt } : null }) });
    }

    private static async Task<IResult> ApplyWorkTemplate(Guid id, TemplateApplyRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var error = await TemplateScopeError(id, r.Scope, db); if (error is not null) return Results.Conflict(new { error });
        var steps = WorkPlanning.Steps(r.Scope.Kind);
        if (r.Items.Count != steps.Count || r.Items.Select(x => x.Step).Distinct().Count() != steps.Count || r.Items.Any(x => !steps.Any(s => s.Key == x.Step)))
            return Results.BadRequest(new { error = "Şablondaki bütün adımları birer kez kontrol edin. Ön izlemeyi yeniden açın." });
        var existing = await ExistingTemplateClose(id, r.Scope, db);
        var run = new WorkTemplateRun { BrandId = id, Kind = r.Scope.Kind, Year = r.Scope.Year, Month = r.Scope.Month, DealId = r.Scope.DealId };
        var tasks = new List<WorkTask>();
        foreach (var step in steps)
        {
            var item = r.Items.Single(x => x.Step == step.Key);
            var previous = step.Kind == WorkKind.MonthlyClose ? existing : null;
            if (item.ExistingTaskId != previous?.Id || item.ExistingTaskRevision != previous?.Revision || previous is not null && previous.DealId != r.Scope.DealId)
                return Results.Conflict(new { error = "Kapanış görevi ön izlemeden sonra değişmiş. Ön izlemeyi yeniden hazırlayın; hiçbir görev eklenmedi." });
            if (previous is not null) { tasks.Add(previous); continue; }
            if (!await db.UserAccounts.AnyAsync(x => x.Id == item.AssigneeId && x.IsActive && x.Role != "BrandClient"))
                return Results.Conflict(new { error = "Yeni görevlerin her birini etkin bir çalışana atayın." });
            var task = new WorkTask { Id = Guid.NewGuid(), BrandId = id, AssigneeId = item.AssigneeId, Title = step.Title, Description = step.Description,
                Kind = step.Kind, DueOn = item.DueOn, CreatedBy = User(user), DealId = step.Kind == WorkKind.MonthlyClose ? r.Scope.DealId : null,
                Year = step.Kind == WorkKind.MonthlyClose ? r.Scope.Year : null, Month = step.Kind == WorkKind.MonthlyClose ? r.Scope.Month : null };
            tasks.Add(task); db.Add(task); Audit(db, user, "WorkTaskCreated", "WorkTask", task.Id, null, task);
        }
        db.Add(run);
        for (var i = 0; i < steps.Count; i++) db.Add(new WorkTemplateTask { RunId = run.Id, Step = steps[i].Key, TaskId = tasks[i].Id });
        Audit(db, user, "WorkTemplateApplied", "Brand", id, null, new { run, taskIds = tasks.Select(x => x.Id) });
        await db.SaveChangesAsync(); return Results.Ok(new { run.Id, created = tasks.Count(x => x.Id != existing?.Id), reused = existing is null ? 0 : 1 });
    }

    private static async Task<IResult> ReadWorkPlanning(AppDbContext db, DateOnly? weekStart = null, Guid? previewUserId = null, decimal additionalHours = 0)
    {
        var week = weekStart ?? WorkPlanning.WeekStart(TeamWork.Today(DateTimeOffset.UtcNow));
        if (!WorkPlanning.ValidWeek(week) || additionalHours is < 0 or > 168 || decimal.Round(additionalHours, 2) != additionalHours || additionalHours > 0 && previewUserId is null)
            return Results.BadRequest(new { error = "Haftanın pazartesi gününü ve 0–168 arasında, en fazla iki ondalıklı saat seçin." });
        var users = await db.UserAccounts.AsNoTracking().Where(x => x.Role != "BrandClient").OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync();
        if (previewUserId.HasValue && !users.Any(x => x.Id == previewUserId && x.IsActive)) return Results.BadRequest(new { error = "Ön izleme için etkin bir çalışan seçin." });
        var capacities = await db.WeeklyCapacities.AsNoTracking().Where(x => x.WeekStart == week).ToListAsync();
        var plans = await (from p in db.TaskHourPlans.AsNoTracking() join task in db.WorkTasks on p.TaskId equals task.Id
            where p.WeekStart == week select new { p.Id, p.TaskId, p.Hours, p.Revision, task.Title, task.AssigneeId, task.BrandId, task.CompletedAt }).ToListAsync();
        var due = await db.WorkTasks.AsNoTracking().Where(x => x.CompletedAt == null && x.DueOn >= week && x.DueOn < week.AddDays(7))
            .Select(x => new { x.Id, x.AssigneeId }).ToListAsync();
        return Results.Ok(new { weekStart = week, previewUserId, additionalHours, rows = users.Select(u => {
            var capacity = capacities.SingleOrDefault(x => x.UserId == u.Id); var assigned = plans.Where(x => x.AssigneeId == u.Id).ToArray();
            var hours = assigned.Sum(x => x.Hours); var completed = assigned.Where(x => x.CompletedAt.HasValue).Sum(x => x.Hours);
            return new { user = u, capacity, result = WorkPlanning.Compare(capacity, hours, completed),
                preview = previewUserId == u.Id ? WorkPlanning.Compare(capacity, hours, completed, additionalHours) : null, plans = assigned,
                dueWithoutPlan = due.Count(x => x.AssigneeId == u.Id && !assigned.Any(p => p.TaskId == x.Id)) }; }) });
    }

    private static async Task<IResult> SaveWeeklyCapacity(Guid id, WeeklyCapacityRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!await db.UserAccounts.AnyAsync(x => x.Id == id && x.IsActive && x.Role != "BrandClient")) return Results.Conflict(new { error = "Kapasite için etkin bir çalışan seçin." });
        var capacity = await db.WeeklyCapacities.SingleOrDefaultAsync(x => x.UserId == id && x.WeekStart == r.WeekStart);
        if ((capacity?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "Haftalık kapasite değişmiş. Sayfayı yenileyin; girişiniz kaydedilmedi." });
        var old = capacity is null ? null : JsonSerializer.Serialize(capacity, Json);
        if (capacity is null) { capacity = new WeeklyCapacity { UserId = id, WeekStart = r.WeekStart }; db.Add(capacity); }
        capacity.WorkingHours = r.WorkingHours; capacity.UnavailableHours = r.UnavailableHours; capacity.Revision++;
        Audit(db, user, "WeeklyCapacitySaved", "WeeklyCapacity", capacity.Id, old, capacity, r.Reason.Trim());
        await db.SaveChangesAsync(); return Results.Ok(capacity);
    }

    private static async Task<IResult> ReadTaskHourPlan(Guid id, AppDbContext db, DateOnly? weekStart = null)
    {
        var week = weekStart ?? WorkPlanning.WeekStart(TeamWork.Today(DateTimeOffset.UtcNow));
        if (!WorkPlanning.ValidWeek(week)) return Results.BadRequest(new { error = "Haftanın pazartesi gününü seçin." });
        var task = await db.WorkTasks.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.Title, x.AssigneeId,
            assigneeName = x.Assignee!.Name, assigneeActive = x.Assignee!.IsActive, x.Revision, x.CompletedAt }).SingleOrDefaultAsync();
        if (task is null) return Results.NotFound();
        var plan = await db.TaskHourPlans.AsNoTracking().SingleOrDefaultAsync(x => x.TaskId == id && x.WeekStart == week);
        return Results.Ok(new { weekStart = week, task, plan });
    }

    private static async Task<IResult> SaveTaskHourPlan(Guid id, TaskHourPlanRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var task = await db.WorkTasks.AsNoTracking().Include(x => x.Assignee).SingleOrDefaultAsync(x => x.Id == id);
        if (task is null) return Results.NotFound();
        if (task.Revision != r.TaskRevision) return Results.Conflict(new { error = "Görevin sorumlusu veya durumu değişmiş. Önce görev bilgisini yenileyin." });
        if (r.Hours > 0 && (task.CompletedAt is not null || task.Assignee is not { IsActive: true } || task.Assignee.Role == "BrandClient"))
            return Results.Conflict(new { error = "Saat planlamak için açık bir görev ve etkin bir sorumlu gerekir. Önce görevi düzenleyin." });
        var plan = await db.TaskHourPlans.SingleOrDefaultAsync(x => x.TaskId == id && x.WeekStart == r.WeekStart);
        if ((plan?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "Görevin saat planı değişmiş. Sayfayı yenileyin; girişiniz kaydedilmedi." });
        var old = plan is null ? null : JsonSerializer.Serialize(plan, Json);
        if (plan is null) { plan = new TaskHourPlan { TaskId = id, WeekStart = r.WeekStart }; db.Add(plan); }
        plan.Hours = r.Hours; plan.Revision++;
        Audit(db, user, "TaskHourPlanSaved", "TaskHourPlan", plan.Id, old, new { plan, task.AssigneeId, task.Revision }, r.Reason.Trim());
        await db.SaveChangesAsync(); return Results.Ok(plan);
    }
}
