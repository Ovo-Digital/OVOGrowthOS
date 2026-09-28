using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapSectorComparison(WebApplication app)
    {
        app.MapGet("/api/reports/sector-comparison", SectorComparisonReport).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> SectorComparisonReport(int? year, int? month, AppDbContext db, CancellationToken ct)
    {
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var y = year ?? today.Year; var m = month ?? today.Month;
        if (y is < 2020 or > 2100 || m is < 1 or > 12)
            return Results.BadRequest(new { error = "Geçerli bir yıl ve ay seçin." });
        var performances = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal)
            .Where(x => x.Year == y && x.Month == m
                && (x.Status == MonthlyPerformanceStatus.Approved || x.Status == MonthlyPerformanceStatus.Locked
                    || x.Status == MonthlyPerformanceStatus.Invoiced || x.Status == MonthlyPerformanceStatus.Paid))
            .ToListAsync(ct);
        var brandIds = performances.Select(x => x.BrandId).Distinct().ToList();
        var brands = await db.Brands.AsNoTracking().Where(x => brandIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Industry, ct);
        var targets = await db.MonthlyTargets.AsNoTracking()
            .Where(x => x.Year == y && x.Month == m && brandIds.Contains(x.BrandId))
            .ToListAsync(ct);
        var targetByBrand = targets.GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First());
        var samples = new List<(string Industry, SectorSample Sample)>();
        foreach (var p in performances)
        {
            var industry = brands.TryGetValue(p.BrandId, out var value) && value.Trim().Length > 0 ? value.Trim() : "Belirtilmemiş";
            decimal? goal = null;
            if (targetByBrand.TryGetValue(p.BrandId, out var target) && p.Deal is not null
                && string.Equals(p.Deal.Currency, target.Currency, StringComparison.OrdinalIgnoreCase))
                goal = target.NetRevenueGoal;
            samples.Add((industry, new SectorSample(p.GrossSales, p.GrossProfit, DataQuality.Returns(p), p.NetRevenue, goal)));
        }
        var items = samples.GroupBy(x => x.Industry).OrderBy(x => x.Key)
            .Select(g =>
            {
                var metric = SectorComparison.Build(g.Select(x => x.Sample));
                return new
                {
                    industry = g.Key,
                    brandCount = metric.BrandCount,
                    grossMargin = metric.AverageGrossMargin,
                    returnShare = metric.AverageReturnShare,
                    targetAchievement = metric.AverageTargetAchievement
                };
            }).ToList();
        return Results.Ok(new { year = y, month = m, period = $"{y}-{m:00}", label = DataQuality.Label(y, m), items });
    }
}
