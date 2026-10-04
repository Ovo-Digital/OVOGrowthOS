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

    private static (MonthlyPerformance Period, ServiceCostAccount? Account, string BrandName) Profitable(
        Guid brandId, string brand, int month, decimal fee, decimal? confirmedCost, decimal? draftCost = null)
    {
        var period = new MonthlyPerformance { BrandId = brandId, Status = MonthlyPerformanceStatus.Paid, Year = 2026, Month = month,
            OvoFee = fee, Deal = new Deal { Name = "Kârlılık anlaşması", Currency = "TRY" } };
        ServiceCostAccount? account = null;
        if (confirmedCost.HasValue || draftCost.HasValue)
        {
            account = new ServiceCostAccount();
            if (confirmedCost.HasValue)
            {
                account.ConfirmedAt = DateTimeOffset.UtcNow;
                account.Entries.Add(new() { Kind = ServiceCostKind.DirectExpense, Amount = confirmedCost.Value });
            }
            if (draftCost.HasValue)
                account.Entries.Add(new() { Kind = ServiceCostKind.DirectExpense, Amount = draftCost.Value });
        }
        return (period, account, brand);
    }

    [Fact]
    public void Profitability_ranks_confirmed_costs_and_never_hides_drafts()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var rows = new[]
        {
            Profitable(a, "Kârlı Marka", 8, 100_000, 30_000),
            Profitable(a, "Kârlı Marka", 9, 100_000, null, draftCost: 90_000),
            Profitable(b, "Zararlı Marka", 8, 50_000, 60_000),
        };
        var rank = BrandProfitability.Rank(rows, "TRY");
        Assert.Equal(2, rank.Count);
        Assert.Equal(a, rank[0].BrandId);
        Assert.Equal(200_000, rank[0].TotalFee);
        Assert.Equal(30_000, rank[0].TotalRecordedCost);
        Assert.Equal(170_000, rank[0].Contribution);
        Assert.Equal(0.85m, rank[0].Margin);
        Assert.Equal(1, rank[0].ConfirmedPeriods);
        Assert.Equal(1, rank[0].UnconfirmedPeriods);
        Assert.Equal(b, rank[1].BrandId);
        Assert.Equal(-10_000, rank[1].Contribution);
    }

    [Fact]
    public void Profitability_ignores_draft_other_currency_and_zero_fee_margin()
    {
        var a = Guid.NewGuid();
        var draft = Profitable(a, "Taslak Marka", 8, 100_000, 10_000);
        draft.Period.Status = MonthlyPerformanceStatus.Draft;
        var foreign = Profitable(a, "Taslak Marka", 8, 100_000, 10_000);
        foreign.Period.Deal = new Deal { Name = "Kârlılık anlaşması", Currency = "USD" };
        var zero = Profitable(a, "Sıfır Marka", 8, 0, 5_000);
        Assert.Empty(BrandProfitability.Rank([draft, foreign], "TRY"));
        var rank = BrandProfitability.Rank([zero], "TRY");
        Assert.Null(rank[0].Margin);
        Assert.Equal(-5_000, rank[0].Contribution);
    }
}
