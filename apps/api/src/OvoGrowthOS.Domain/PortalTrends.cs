namespace OvoGrowthOS.Domain;

public sealed record PublishedPortalPeriod(Guid Id, int Year, int Month, int Version, DateTimeOffset PublishedAt, PortalReportSnapshot Snapshot);
public sealed record PortalTrendMonth(int Year, int Month, Guid? ReportId, int? Version, DateTimeOffset? PublishedAt,
    BrandReportMetrics? Metrics, decimal? NetRevenueChange);
public sealed record PortalTrendTotals(decimal NetRevenue, decimal OvoFee, decimal AdSpend, decimal BrandContribution,
    decimal Paid, decimal Outstanding, decimal? Mer);
public sealed record PortalTrend(int EndYear, int EndMonth, int Months, string Currency, int SharedMonths,
    IReadOnlyList<PortalTrendMonth> Items, PortalTrendTotals? Totals);

public static class PortalTrends
{
    // The API supplies only this customer's non-revoked snapshots. Never query live financial records here.
    public static IReadOnlyList<PublishedPortalPeriod> Latest(IEnumerable<PublishedPortalPeriod> published) => published
        .GroupBy(x => new { x.Year, x.Month }).Select(g => g.OrderByDescending(x => x.Version).ThenByDescending(x => x.PublishedAt).ThenBy(x => x.Id).First()).ToArray();

    public static PortalTrend Build(IEnumerable<PublishedPortalPeriod> published, int endYear, int endMonth, int months, string currency)
    {
        if (months is not (3 or 6 or 12)) throw new ArgumentOutOfRangeException(nameof(months));
        var end = new DateOnly(endYear, endMonth, 1); var start = end.AddMonths(1 - months);
        // Choose the latest visible version before currency filtering; never fall back to an older currency version.
        var chosen = Latest(published).Where(x => x.Snapshot.Currency == currency).ToDictionary(x => (x.Year, x.Month));
        var rows = new List<PortalTrendMonth>();
        for (var i = 0; i < months; i++)
        {
            var date = start.AddMonths(i); chosen.TryGetValue((date.Year, date.Month), out var report);
            var metrics = report?.Snapshot.Metrics;
            rows.Add(new(date.Year, date.Month, report?.Id, report?.Version, report?.PublishedAt, metrics,
                metrics is null ? null : BrandReporting.Change(metrics.NetRevenue, rows.LastOrDefault()?.Metrics?.NetRevenue)));
        }
        var shared = rows.Select(x => x.Metrics).OfType<BrandReportMetrics>().ToArray();
        var revenue = shared.Sum(x => x.NetRevenue); var ads = shared.Sum(x => x.AdSpend);
        return new(endYear, endMonth, months, currency, shared.Length, rows, shared.Length == 0 ? null : new(
            revenue, shared.Sum(x => x.OvoFee), ads, shared.Sum(x => x.BrandContribution), shared.Sum(x => x.Paid), shared.Sum(x => x.Outstanding), ads > 0 ? revenue / ads : null));
    }
}
