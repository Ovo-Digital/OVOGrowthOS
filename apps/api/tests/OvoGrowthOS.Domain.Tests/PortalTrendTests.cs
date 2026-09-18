using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class PortalTrendTests
{
    private static PublishedPortalPeriod Period(int year, int month, decimal revenue, decimal ads = 10, int version = 1, string currency = "TRY")
    {
        var metrics = new BrandReportMetrics(Guid.NewGuid(), year, month, MonthlyPerformanceStatus.Locked, revenue, 5, ads,
            ads > 0 ? revenue / ads : null, .1m, 30, 2, 3);
        return new(Guid.NewGuid(), year, month, version, DateTimeOffset.UtcNow, new("Marka", currency, version, DateTimeOffset.UtcNow, metrics, []));
    }
    [Fact]
    public void Latest_visible_version_is_counted_once_with_missing_months_explicit()
    {
        var jan = Period(2026, 1, 100); var march = Period(2026, 3, 300.1234m, 90, 2);
        var plan = PortalTrends.Build([jan, Period(2026, 3, 999), march, Period(2025, 12, 99999)], 2026, 3, 3, "TRY");
        Assert.Equal(3, plan.Items.Count); Assert.Equal(2, plan.SharedMonths); Assert.Null(plan.Items[1].Metrics);
        Assert.Equal(march.Id, plan.Items[2].ReportId); Assert.Equal(2, plan.Items[2].Version);
        Assert.Equal(400.1234m, plan.Totals!.NetRevenue); Assert.Equal(4.001234m, plan.Totals.Mer);
        Assert.Equal(10, plan.Totals.OvoFee); Assert.Equal(4, plan.Totals.Paid); Assert.Equal(6, plan.Totals.Outstanding);
        Assert.Null(plan.Items[2].NetRevenueChange); // Missing February is not silently skipped.
    }
    [Fact]
    public void Currency_change_does_not_resurrect_an_older_version_in_other_currency()
    {
        var source = new[] { Period(2026, 1, 100), Period(2026, 1, 50, version: 2, currency: "USD") };
        Assert.Null(PortalTrends.Build(source, 2026, 1, 3, "TRY").Totals);
        Assert.Equal(50, PortalTrends.Build(source, 2026, 1, 3, "USD").Totals!.NetRevenue);
    }
    [Fact]
    public void Zero_negative_and_missing_bases_do_not_produce_growth_or_infinite_efficiency()
    {
        var rows = PortalTrends.Build([Period(2026, 1, 0, 0), Period(2026, 2, -5, 0), Period(2026, 3, 10, 0)], 2026, 3, 3, "TRY");
        Assert.All(rows.Items, x => Assert.Null(x.NetRevenueChange)); Assert.Null(rows.Totals!.Mer); Assert.Equal(5, rows.Totals.NetRevenue);
        var positive = PortalTrends.Build([Period(2026, 1, 100), Period(2026, 2, 125)], 2026, 2, 3, "TRY");
        Assert.Equal(.25m, positive.Items[2].NetRevenueChange);
        Assert.Null(PortalTrends.Build([], 2026, 3, 3, "TRY").Totals);
    }
    [Theory]
    [InlineData(3, 2025, 11)]
    [InlineData(6, 2025, 8)]
    [InlineData(12, 2025, 2)]
    public void Calendar_windows_include_end_month_and_cross_year_boundary(int months, int year, int month)
    {
        var plan = PortalTrends.Build([], 2026, 1, months, "TRY"); Assert.Equal(months, plan.Items.Count);
        Assert.Equal(year, plan.Items[0].Year); Assert.Equal(month, plan.Items[0].Month);
        Assert.Equal(1, plan.Items[^1].Month); Assert.Equal(2026, plan.Items[^1].Year);
        Assert.Throws<ArgumentOutOfRangeException>(() => PortalTrends.Build([], 2026, 1, 4, "TRY"));
    }
}
