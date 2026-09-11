using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class OperatingCostTests
{
    [Fact]
    public void Actual_cost_replaces_plan_once_and_incomplete_cost_never_claims_profit()
    {
        var p = new MonthlyPerformance { OvoFee = 100_000, OvoInternalCost = 30_000, OvoGrossProfit = 70_000, Status = MonthlyPerformanceStatus.Paid };
        var a = new ServiceCostAccount { Entries = [new() { Kind = ServiceCostKind.DirectExpense, Amount = 10_000 },
            new() { Kind = ServiceCostKind.TeamWork, Hours = 20, HourlyCost = 1000, Amount = 20_000 }] };
        Assert.Null(OperatingCosts.Summary(p, a).ContributionAfterRecordedCosts);
        a.ConfirmedAt = DateTimeOffset.UtcNow; var s = OperatingCosts.Summary(p, a);
        Assert.Equal(30_000, s.RecordedCost); Assert.Equal(70_000, s.ContributionAfterRecordedCosts); Assert.Equal(.7m, s.MarginAfterRecordedCosts); Assert.Equal(0, s.CostDifference); Assert.Equal(20, s.Hours);
        a.Entries[0].VoidedAt = DateTimeOffset.UtcNow; s = OperatingCosts.Summary(p, a);
        Assert.Equal(20_000, s.RecordedCost); Assert.Equal(80_000, s.ContributionAfterRecordedCosts); Assert.Equal(-10_000, s.CostDifference);
        Assert.Equal(70_000, p.OvoGrossProfit); Assert.Equal(100_000, p.OvoFee); Assert.Equal(30_000, p.OvoInternalCost);
    }
    [Fact]
    public void Zero_unknown_and_negative_contribution_are_explained_not_hidden()
    {
        var p = new MonthlyPerformance { OvoFee = 0 };
        Assert.False(OperatingCosts.Summary(p, null).Complete);
        var a = new ServiceCostAccount { ConfirmedAt = DateTimeOffset.UtcNow, Entries = [new() { Amount = 500 }] };
        Assert.Equal(-500, OperatingCosts.Summary(p, a).ContributionAfterRecordedCosts); Assert.Null(OperatingCosts.Summary(p, a).MarginAfterRecordedCosts);
    }
    [Theory]
    [InlineData(10, 500, 5000)] [InlineData(1.1111, 0.5, .5556)] [InlineData(0, 500, 0)]
    public void Team_cost_is_decimal_and_rounds_once(decimal hours, decimal cost, decimal expected) => Assert.Equal(expected, OperatingCosts.TeamAmount(hours, cost));
    [Fact]
    public void Investment_is_only_explicit_entries_not_budget_fees_or_service_costs()
    {
        var deal = new Deal { Name = "Test", SetupInvestment = 200_000 };
        var empty = OperatingCosts.InvestmentSummary(deal, null); Assert.Equal(200_000, empty.PlannedInvestment); Assert.Equal(0, empty.RecordedInvestment); Assert.False(empty.HasRecords);
        var a = new InvestmentAccount { Entries = [new() { Kind = InvestmentEntryKind.Investment, Amount = 100_000 }, new() { Kind = InvestmentEntryKind.Recovery, Amount = 40_000 }] };
        var s = OperatingCosts.InvestmentSummary(deal, a); Assert.Equal(100_000, s.RecordedInvestment); Assert.Equal(40_000, s.RecordedRecovery); Assert.Equal(60_000, s.Remaining);
    }
}
