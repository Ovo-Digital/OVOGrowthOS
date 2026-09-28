using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;
using HealthScore = OvoGrowthOS.Domain.BrandHealth;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapBrandHealth(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/health", ReadBrandHealth).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ReadBrandHealth(Guid id, AppDbContext db, CancellationToken ct)
    {
        var brand = await db.Brands.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name, x.Industry, x.Currency }).SingleOrDefaultAsync(ct);
        if (brand is null) return Results.NotFound(new { error = "Marka bulunamadı." });
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var report = await BuildQualityReport(db, today.Year, today.Month, id);
        var quality = report.Brands.SingleOrDefault(x => x.BrandId == id);

        var target = await db.MonthlyTargets.AsNoTracking()
            .Where(x => x.BrandId == id)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        TargetComparison? comparison = null;
        if (target is not null)
        {
            var period = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal)
                .Where(x => x.BrandId == id && x.Year == target.Year && x.Month == target.Month)
                .SingleOrDefaultAsync(ct);
            comparison = MonthlyTargetEngine.Compare(target, period);
        }

        var promiseRow = await (from promise in db.CollectionPromises.AsNoTracking()
                                join performance in db.MonthlyPerformances.AsNoTracking() on promise.MonthlyPerformanceId equals performance.Id
                                where performance.BrandId == id && !promise.IsCancelled
                                orderby promise.PromisedOn descending
                                select new { promise.Id }).FirstOrDefaultAsync(ct);
        PromiseBalance? promiseBalance = null;
        if (promiseRow is not null)
        {
            var promisePeriod = await (from promise in db.CollectionPromises
                                       join performance in db.MonthlyPerformances on promise.MonthlyPerformanceId equals performance.Id
                                       where promise.Id == promiseRow.Id
                                       select performance.Id).FirstOrDefaultAsync(ct);
            var loaded = await db.MonthlyPerformances
                .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
                .SingleOrDefaultAsync(x => x.Id == promisePeriod, ct);
            if (loaded is not null) promiseBalance = CollectionPromises.Balance(loaded, today);
        }

        var dealStatus = await db.Deals.AsNoTracking()
            .Where(x => x.BrandId == id && x.Status == DealStatus.Active)
            .Select(x => (DealStatus?)x.Status).FirstOrDefaultAsync(ct)
            ?? await db.Deals.AsNoTracking().Where(x => x.BrandId == id)
                .OrderByDescending(x => x.CreatedAt).Select(x => (DealStatus?)x.Status).FirstOrDefaultAsync(ct);

        var score = HealthScore.Evaluate(new BrandHealthInput(
            quality?.Readiness ?? "none",
            (quality?.Alerts.Count ?? 0),
            comparison, promiseBalance, dealStatus, today));
        return Results.Ok(new
        {
            brandId = id, brandName = brand.Name, score = score.Score, band = score.Band,
            factors = score.Factors.Select(x => new { x.Code, x.Label, x.Effect, x.Detail }),
            qualityPeriod = report.Label,
            targetPeriod = target is null ? null : DataQuality.Label(target.Year, target.Month)
        });
    }
}
