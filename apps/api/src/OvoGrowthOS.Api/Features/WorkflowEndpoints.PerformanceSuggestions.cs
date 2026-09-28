using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapPerformanceSuggestions(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/performance-suggestions", ReadPerformanceSuggestions)
            .RequireAuthorization("OperationsWrite").RequireRateLimiting("user-action");
    }

    private static async Task<IResult> ReadPerformanceSuggestions(Guid id, string? period, AppDbContext db, CancellationToken ct)
    {
        if (!StoreOrderPeriod.TryParse(period, out var parsed)) return Results.BadRequest(new { error = "Dönem 'YYYY-AA' biçiminde olmalı, örneğin 2026-08." });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        var rows = await db.StoreOrderStagings.AsNoTracking()
            .Where(x => x.BrandId == id && x.PlacedOnUtc >= parsed.UtcStart && x.PlacedOnUtc < parsed.UtcEnd)
            .ToListAsync(ct);
        var currencies = rows.Select(x => x.Currency).Where(x => x.Length > 0).Distinct().ToList();
        var summary = StoreOrderSummary.Summarize(rows, currencies.Count == 1 ? currencies[0] : "");
        var panelQuery = db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.BrandId == id && x.Year == parsed.Year && x.Month == parsed.Month);
        decimal? panelSales = await panelQuery.AnyAsync(ct) ? await panelQuery.SumAsync(x => x.GrossSales, ct) : null;
        var adPlatforms = await db.BrandAdSettings.AsNoTracking().Where(x => x.BrandId == id)
            .Select(x => x.Platform).ToListAsync(ct);
        return Results.Ok(new
        {
            period = parsed.Key,
            gross = new
            {
                available = rows.Count > 0,
                amount = summary.ActiveTotal,
                currency = summary.Currency,
                orderCount = summary.OrderCount - summary.CancelledCount,
                lastSyncAt = rows.Count == 0 ? null : rows.Max(x => (DateTimeOffset?)x.ImportedAt),
                panelGrossSales = panelSales
            },
            adSpend = new
            {
                metaConfigured = adPlatforms.Contains(AdPlatform.Meta),
                googleConfigured = adPlatforms.Contains(AdPlatform.Google)
            }
        });
    }
}
