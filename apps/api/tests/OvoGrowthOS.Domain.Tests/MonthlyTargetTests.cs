using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class MonthlyTargetTests
{
    private static readonly Guid BrandId = Guid.NewGuid();
    private static MonthlyTarget Target() => new() { BrandId = BrandId, Year = 2026, Month = 8, NetRevenueGoal = 1000000, AdBudget = 100000, ContributionMarginGoal = .3m };
    private static MonthlyPerformance Period() => new() { BrandId = BrandId, Year = 2026, Month = 8, Deal = new Deal { Name = "Test anlaşması", Currency = "TRY" }, NetRevenue = 850000, TotalAdSpend = 125000, BrandContributionProfit = 170000 };

    [Fact]
    public void Comparison_has_explicit_money_relative_and_percentage_point_differences()
    {
        var result = MonthlyTargetEngine.Compare(Target(), Period());
        Assert.False(result.IsClosed);
        Assert.Equal(-150000m, result.Metrics[0].Difference); Assert.Equal(-.15m, result.Metrics[0].RelativeDifference);
        Assert.Equal(25000m, result.Metrics[1].Difference); Assert.Equal(.25m, result.Metrics[1].RelativeDifference);
        Assert.Equal(-10m, result.Metrics[2].PercentagePointDifference); Assert.Null(result.Metrics[2].RelativeDifference);
        Assert.All(result.Metrics, m => Assert.True(m.NeedsAttention));
    }
    [Fact]
    public void Missing_mismatched_and_zero_revenue_are_not_invented_results()
    {
        var target = Target(); var period = Period();
        Assert.All(MonthlyTargetEngine.Compare(target, null).Metrics, m => { Assert.Null(m.Actual); Assert.Null(m.NeedsAttention); });
        period.Deal!.Currency = "USD";
        Assert.All(MonthlyTargetEngine.Compare(target, period).Metrics, m => Assert.Null(m.Actual));
        period.Deal.Currency = "TRY"; period.Month = 9;
        Assert.All(MonthlyTargetEngine.Compare(target, period).Metrics, m => Assert.Null(m.Actual));
        period.Month = 8; period.NetRevenue = 0; target.NetRevenueGoal = 0; target.AdBudget = 0;
        var zero = MonthlyTargetEngine.Compare(target, period);
        Assert.Null(zero.Metrics[0].RelativeDifference); Assert.Null(zero.Metrics[1].RelativeDifference); Assert.Null(zero.Metrics[2].Actual);
        period.NetRevenue = -100; target.NetRevenueGoal = 100;
        Assert.Null(MonthlyTargetEngine.Compare(target, period).Metrics[0].RelativeDifference);
    }
    [Theory]
    [InlineData(999, true, false)] [InlineData(1000, false, false)] [InlineData(1001, false, true)]
    public void Thresholds_respect_goal_floor_and_budget_ceiling(int amount, bool revenueAlert, bool budgetAlert)
    {
        var target = Target(); target.NetRevenueGoal = target.AdBudget = 1000; target.ContributionMarginGoal = .3m;
        var period = Period(); period.NetRevenue = period.TotalAdSpend = amount; period.BrandContributionProfit = amount * .3m; period.Status = MonthlyPerformanceStatus.Paid;
        var result = MonthlyTargetEngine.Compare(target, period);
        Assert.True(result.IsClosed); Assert.Equal(revenueAlert, result.Metrics[0].NeedsAttention); Assert.Equal(budgetAlert, result.Metrics[1].NeedsAttention);
        Assert.False(result.Metrics[2].NeedsAttention); Assert.Equal(0m, result.Metrics[2].PercentagePointDifference);
    }
}
