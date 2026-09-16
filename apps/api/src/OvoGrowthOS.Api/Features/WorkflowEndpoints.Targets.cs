using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record MonthlyTargetRequest([property: JsonRequired] decimal NetRevenueGoal, [property: JsonRequired] decimal AdBudget,
    [property: JsonRequired] decimal ContributionMarginGoal, Guid OwnerId, string Reason, [property: JsonRequired] int Revision);
public sealed record TargetActionRequest(TargetMetric Metric, Guid AssigneeId, DateOnly DueOn, string Description, int TargetRevision, DateTimeOffset PerformanceUpdatedAt);

public static partial class WorkflowEndpoints
{
    private static void MapMonthlyTargets(WebApplication app)
    {
        app.MapGet("/api/brands/{brandId:guid}/targets/{year:int}/{month:int}/{currency}", ReadMonthlyTarget).RequireAuthorization("ReadAccess");
        app.MapPut("/api/brands/{brandId:guid}/targets/{year:int}/{month:int}/{currency}", SaveMonthlyTarget)
            .AddEndpointFilter<ValidationFilter<MonthlyTargetRequest>>().RequireAuthorization("OperationsWrite");
        app.MapGet("/api/targets/{id:guid}/history", async (Guid id, AppDbContext db, int page = 1) =>
            Results.Ok(await Page(db.AuditRecords.AsNoTracking().Where(a => a.EntityType == "MonthlyTarget" && a.EntityId == id.ToString()
                && a.Action == "MonthlyTargetSaved").OrderByDescending(a => a.CreatedAt), page, 20))).RequireAuthorization("ReadAccess");
        app.MapPost("/api/targets/{id:guid}/actions", CreateTargetAction)
            .AddEndpointFilter<ValidationFilter<TargetActionRequest>>().RequireAuthorization("OperationsWrite");
    }

    private static bool ValidTargetPeriod(int year, int month, string currency) => year is >= 2020 and <= 2100 && month is >= 1 and <= 12
        && currency.Length == 3 && currency.All(c => c is >= 'A' and <= 'Z');

    private static async Task<IResult> ReadMonthlyTarget(Guid brandId, int year, int month, string currency, AppDbContext db)
    {
        if (!ValidTargetPeriod(year, month, currency)) return Results.BadRequest(new { error = "Geçerli yıl, ay ve TRY gibi üç harfli para birimi seçin." });
        var brand = await db.Brands.AsNoTracking().Where(x => x.Id == brandId).Select(x => new { x.Name }).SingleOrDefaultAsync();
        if (brand is null) return Results.NotFound();
        var target = await db.MonthlyTargets.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == year && x.Month == month && x.Currency == currency);
        if (target is null) return Results.Ok(new { brandName = brand.Name, target = (MonthlyTarget?)null, comparison = (TargetComparison?)null });
        var period = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal).SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == year && x.Month == month);
        var owner = await db.UserAccounts.AsNoTracking().Where(x => x.Id == target.OwnerId).Select(x => new { x.Name, x.IsActive }).SingleAsync();
        var actions = await (from action in db.TargetActions.AsNoTracking() join task in db.WorkTasks on action.TaskId equals task.Id
            where action.TargetId == target.Id select new { action.Metric, action.TaskId, action.TargetRevision, action.PerformanceUpdatedAt,
                task.Title, task.DueOn, task.CompletedAt, task.AssigneeId }).ToListAsync();
        return Results.Ok(new { brandName = brand.Name, target, owner, comparison = MonthlyTargetEngine.Compare(target, period), actions });
    }

    private static async Task<IResult> SaveMonthlyTarget(Guid brandId, int year, int month, string currency, MonthlyTargetRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!ValidTargetPeriod(year, month, currency)) return Results.BadRequest(new { error = "Geçerli yıl, ay ve TRY gibi üç harfli para birimi seçin." });
        if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
        if (!await db.UserAccounts.AnyAsync(x => x.Id == r.OwnerId && x.IsActive && x.Role != "BrandClient"))
            return Results.Conflict(new { error = "Hedef sorumlusu olarak etkin bir çalışan seçin." });
        var target = await db.MonthlyTargets.SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == year && x.Month == month && x.Currency == currency);
        if ((target?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "Hedef siz açtıktan sonra değişmiş. Sayfayı yenileyin; değişikliğiniz kaydedilmedi." });
        var old = target is null ? null : JsonSerializer.Serialize(target, Json);
        if (target is null) { target = new MonthlyTarget { BrandId = brandId, Year = year, Month = month, Currency = currency }; db.Add(target); }
        target.NetRevenueGoal = r.NetRevenueGoal; target.AdBudget = r.AdBudget; target.ContributionMarginGoal = r.ContributionMarginGoal;
        target.OwnerId = r.OwnerId; target.Revision++; target.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, user, "MonthlyTargetSaved", "MonthlyTarget", target.Id, old, target, r.Reason.Trim());
        await db.SaveChangesAsync(); return Results.Ok(target);
    }

    private static async Task<IResult> CreateTargetAction(Guid id, TargetActionRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var target = await db.MonthlyTargets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (target is null) return Results.NotFound();
        if (target.Revision != r.TargetRevision) return Results.Conflict(new { error = "Hedef değişmiş. Son hedefi kontrol edip tekrar deneyin." });
        if (await db.TargetActions.AnyAsync(x => x.TargetId == id && x.Metric == r.Metric))
            return Results.Conflict(new { error = "Bu gösterge için takip işi zaten var. İlgili işi açın; gerekirse mevcut işi yeniden açabilirsiniz." });
        if (!await db.UserAccounts.AnyAsync(x => x.Id == r.AssigneeId && x.IsActive && x.Role != "BrandClient"))
            return Results.Conflict(new { error = "Takip işini etkin bir çalışana atayın." });
        var period = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal)
            .SingleOrDefaultAsync(x => x.BrandId == target.BrandId && x.Year == target.Year && x.Month == target.Month);
        var comparison = MonthlyTargetEngine.Compare(target, period); var metric = comparison.Metrics.Single(x => x.Metric == r.Metric);
        if (comparison.PerformanceUpdatedAt != r.PerformanceUpdatedAt) return Results.Conflict(new { error = "Gerçekleşen sonuç değişmiş veya karşılaştırılabilir kayıt yok. Önce sayfayı yenileyin." });
        if (metric.NeedsAttention != true) return Results.Conflict(new { error = "Bu gösterge için kayıtlı hedef dışı sonuç yok. Diğer işler için markanın normal görev alanını kullanın." });
        var label = r.Metric switch { TargetMetric.NetRevenue => "Net ciro hedefi", TargetMetric.AdSpend => "Reklam bütçesi", _ => "Katkı marjı hedefi" };
        var culture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        string Display(decimal value) => r.Metric == TargetMetric.ContributionMargin ? value.ToString("P2", culture) : value.ToString("N4", culture) + " " + target.Currency;
        var task = new WorkTask { Id = Guid.NewGuid(), BrandId = target.BrandId, AssigneeId = r.AssigneeId, Kind = WorkKind.General,
            Title = $"{target.Month}/{target.Year} · {label} takibi", DueOn = r.DueOn, CreatedBy = User(user),
            Description = $"{r.Description.Trim()}\nHedef sürümü: {target.Revision}; para birimi: {target.Currency}. "
                + $"Kaynak dönem: {target.Month}/{target.Year}; sonuç {(comparison.IsClosed ? "kapanmış" : "geçici; kapanış bekliyor")}. "
                + $"Hedef: {Display(metric.Target)}; gerçekleşen: {Display(metric.Actual!.Value)}. "
                + "Bu görev finansal onay veya bütçe değişikliği yapmaz." };
        var action = new TargetAction { TargetId = id, Metric = r.Metric, TaskId = task.Id, TargetRevision = target.Revision,
            PerformanceId = period!.Id, PerformanceUpdatedAt = period.UpdatedAt };
        db.AddRange(task, action); Audit(db, user, "WorkTaskCreated", "WorkTask", task.Id, null, task);
        Audit(db, user, "TargetActionCreated", "MonthlyTarget", id, null, action, r.Description.Trim());
        await db.SaveChangesAsync(); return Results.Created($"/api/work-tasks?brandId={target.BrandId}", new { task.Id, action.Metric });
    }
}
