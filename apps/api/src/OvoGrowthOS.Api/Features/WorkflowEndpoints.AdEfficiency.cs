using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapAdEfficiency(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/ad-efficiency", ReadAdEfficiency).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ReadAdEfficiency(Guid id, AppDbContext db)
    {
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (brand is null) return Results.NotFound();

        var closedStatuses = new[] { MonthlyPerformanceStatus.Locked, MonthlyPerformanceStatus.Invoiced, MonthlyPerformanceStatus.Paid };
        var rows = await db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.BrandId == id && closedStatuses.Contains(x.Status))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Take(6).ToListAsync();

        var target = await db.Evaluations.AsNoTracking()
            .Where(x => x.BrandId == id && (x.Status == EvaluationStatus.Approved || x.Status == EvaluationStatus.Analyzed))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (decimal?)x.RecommendedTargetMer)
            .FirstOrDefaultAsync();

        var points = rows.Select(x => new AdEfficiencyPoint(x.Year, x.Month, x.TotalAdSpend, x.NetRevenue,
            x.TotalAdSpend <= 0 ? null : (decimal?)x.Mer)).ToList();
        var summary = AdEfficiency.Build(target is > 0 ? target : null, points);
        return Results.Ok(new
        {
            brandId = id,
            brandName = brand.Name,
            bandCode = summary.BandCode,
            bandLabel = summary.BandLabel,
            bandDetail = summary.BandDetail,
            latestMer = summary.LatestMer,
            breakEvenMer = summary.BreakEvenMer,
            periodLabel = summary.PeriodLabel,
            trend = points,
            note = summary.Note
        });
    }
}
