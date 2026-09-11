using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class PortfolioReportingTests
{
    [Fact]
    public void Draft_changes_do_not_change_closed_totals_or_historical_receivables()
    {
        var draft = Row(MonthlyPerformanceStatus.Draft, 2026, 9, 900_000m);
        var records = new[] { Row(MonthlyPerformanceStatus.Locked, 2026, 8, 50_000m), Row(MonthlyPerformanceStatus.Paid, 2026, 9, 70_000m), draft };
        var totals = PortfolioReporting.Summarize(records.Where(x => PortfolioReporting.Matches(x.Status, ReportScope.Closed)));
        Assert.Equal(2, totals.RecordCount);
        Assert.Equal(120_000m, totals.OvoFee);
        Assert.Equal(50_000m, PortfolioReporting.Outstanding(records));
        Assert.Equal(70_000m, PortfolioReporting.Paid(records));
        draft.OvoFee = 1_000_000m;
        Assert.Equal(totals, PortfolioReporting.Summarize(records.Where(x => PortfolioReporting.IsClosed(x.Status))));
        Assert.Equal(50_000m, records[0].OvoFee);
        Assert.Equal(MonthlyPerformanceStatus.Locked, records[0].Status);
    }

    [Theory]
    [InlineData(MonthlyPerformanceStatus.Draft, ReportScope.Preparation)]
    [InlineData(MonthlyPerformanceStatus.UnderReview, ReportScope.Preparation)]
    [InlineData(MonthlyPerformanceStatus.Approved, ReportScope.Approved)]
    [InlineData(MonthlyPerformanceStatus.Locked, ReportScope.Closed)]
    [InlineData(MonthlyPerformanceStatus.Invoiced, ReportScope.Closed)]
    [InlineData(MonthlyPerformanceStatus.Paid, ReportScope.Closed)]
    public void Each_workflow_stage_belongs_to_exactly_one_reporting_group(MonthlyPerformanceStatus status, ReportScope scope)
    {
        Assert.True(PortfolioReporting.Matches(status, scope));
        Assert.True(PortfolioReporting.Matches(status, ReportScope.All));
        Assert.Single(new[] { ReportScope.Closed, ReportScope.Approved, ReportScope.Preparation }, x => PortfolioReporting.Matches(status, x));
    }

    [Fact]
    public void Weighted_ratios_are_calculated_from_totals_and_zero_denominator_is_unknown()
    {
        var first = Row(MonthlyPerformanceStatus.Locked, 2026, 9, 100m);
        var second = Row(MonthlyPerformanceStatus.Locked, 2026, 9, 300m);
        first.OvoGrossProfit = 50m; second.OvoGrossProfit = 30m;
        first.NetRevenue = 1_000m; second.NetRevenue = 5_000m;
        first.TotalAdSpend = 200m; second.TotalAdSpend = 1_000m;
        var totals = PortfolioReporting.Summarize([first, second]);
        Assert.Equal(.2m, totals.OvoMargin);
        Assert.Equal(5m, totals.Mer);
        var empty = PortfolioReporting.Summarize([]);
        Assert.Equal(0, empty.RecordCount);
        Assert.Null(empty.OvoMargin);
        Assert.Null(empty.Mer);
    }

    [Fact]
    public void Missing_period_detection_respects_contract_dates_and_does_not_guess_unknown_start()
    {
        var period = new ReportPeriod(2026, 9);
        var deal = new Deal { Name = "Dönem testi", Status = DealStatus.Active, StartDate = new DateOnly(2026, 9, 30) };
        Assert.True(PortfolioReporting.ExpectedInPeriod(deal, period));
        deal.StartDate = new DateOnly(2026, 10, 1);
        Assert.False(PortfolioReporting.ExpectedInPeriod(deal, period));
        deal.StartDate = new DateOnly(2026, 1, 1); deal.EndDate = new DateOnly(2026, 9, 1);
        Assert.True(PortfolioReporting.ExpectedInPeriod(deal, period));
        deal.EndDate = new DateOnly(2026, 8, 31);
        Assert.False(PortfolioReporting.ExpectedInPeriod(deal, period));
        deal.StartDate = null;
        Assert.False(PortfolioReporting.ExpectedInPeriod(deal, period));
    }

    private static MonthlyPerformance Row(MonthlyPerformanceStatus status, int year, int month, decimal fee) =>
        new() { Status = status, Year = year, Month = month, OvoFee = fee };
}
