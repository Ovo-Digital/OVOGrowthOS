using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class TargetProjectionTests
{
    private static MonthlyTarget Goal(decimal revenue, decimal marginGoal = 0) =>
        new() { Year = 2026, Month = 9, Currency = "TRY", NetRevenueGoal = revenue, ContributionMarginGoal = marginGoal };

    [Fact]
    public void Revenue_share_target_projects_fee_rate_and_contribution_from_the_goal()
    {
        var deal = new Deal { Name = "Pay anlaşması", DealType = DealType.FlatRevenueShare, RevenueShareRate = .08m, EstimatedMonthlyInternalCost = 50_000 };
        var p = TargetProjectionEngine.Project(Goal(1_000_000, .25m), deal);
        Assert.True(p.Calculable); Assert.Null(p.NotReason);
        Assert.Equal(80_000m, p.OvoFee);
        Assert.Equal(.08m, p.EffectiveRate);
        Assert.Equal(30_000m, p.OvoGrossProfit);
        Assert.Equal(250_000m, p.BrandContributionProfit);
        Assert.Equal(.25m, p.BrandContributionMarginGoal);
        Assert.Equal(1_000_000m, p.NetRevenueGoal); Assert.Equal("TRY", p.Currency);
        Assert.Equal("Pay anlaşması", p.DealName); Assert.Equal("FlatRevenueShare", p.DealType);
    }

    [Fact]
    public void Retainer_minimum_and_fixed_models_use_the_same_agreement_rates()
    {
        var retainer = TargetProjectionEngine.Project(Goal(1_000_000),
            new Deal { Name = "Anlaşma", DealType = DealType.RetainerPlusRevenueShare, MonthlyRetainer = 40_000, RevenueShareRate = .05m });
        Assert.Equal(90_000m, retainer.OvoFee);

        var minimum = TargetProjectionEngine.Project(Goal(100_000),
            new Deal { Name = "Anlaşma", DealType = DealType.MinimumFeePlusRevenueShare, RevenueShareRate = .10m, MinimumMonthlyFee = 50_000 });
        Assert.Equal(50_000m, minimum.OvoFee);
        Assert.Equal(.5m, minimum.EffectiveRate);

        var fixedRetainer = TargetProjectionEngine.Project(Goal(1_000_000),
            new Deal { Name = "Anlaşma", DealType = DealType.FixedRetainer, MonthlyRetainer = 75_000 });
        Assert.Equal(75_000m, fixedRetainer.OvoFee);
        Assert.Equal(.075m, fixedRetainer.EffectiveRate);
    }

    [Fact]
    public void Contribution_profit_share_is_solved_from_the_margin_goal_instead_of_guessed()
    {
        var deal = new Deal { Name = "Anlaşma", DealType = DealType.ContributionProfitShare, MonthlyRetainer = 10_000, ProfitShareRate = .2m };
        var p = TargetProjectionEngine.Project(Goal(1_000_000, .2m), deal);
        Assert.True(p.Calculable);
        Assert.Equal(62_500m, p.OvoFee);
        Assert.Equal(.0625m, p.EffectiveRate);

        var withoutMargin = TargetProjectionEngine.Project(Goal(1_000_000), deal);
        Assert.False(withoutMargin.Calculable);
        Assert.Contains("katkı marjı hedefi", withoutMargin.NotReason);
        Assert.Null(withoutMargin.OvoFee);

        var impossible = TargetProjectionEngine.Project(Goal(1_000_000, .2m),
            new Deal { Name = "Anlaşma", DealType = DealType.ContributionProfitShare, MonthlyRetainer = 10_000, ProfitShareRate = 1m });
        Assert.False(impossible.Calculable);
        Assert.Contains("%100", impossible.NotReason);
    }

    [Fact]
    public void Missing_deal_or_empty_goal_is_not_calculable_and_invents_no_numbers()
    {
        var noDeal = TargetProjectionEngine.Project(Goal(1_000_000), null);
        Assert.False(noDeal.Calculable);
        Assert.Contains("etkin anlaşma", noDeal.NotReason);
        Assert.Null(noDeal.OvoFee); Assert.Null(noDeal.OvoGrossProfit); Assert.Null(noDeal.BrandContributionProfit);
        Assert.Null(noDeal.DealName);

        var zero = TargetProjectionEngine.Project(Goal(0), new Deal { Name = "Anlaşma", DealType = DealType.FixedRetainer, MonthlyRetainer = 10_000 });
        Assert.False(zero.Calculable);
        Assert.Contains("sıfırdan büyük", zero.NotReason);
        Assert.Null(zero.OvoFee);
    }

    [Fact]
    public void Margin_goal_is_reported_only_when_it_was_actually_entered()
    {
        var withGoal = TargetProjectionEngine.Project(Goal(500_000, .3m), new Deal { Name = "Anlaşma", DealType = DealType.FixedRetainer, MonthlyRetainer = 10_000 });
        Assert.Equal(150_000m, withGoal.BrandContributionProfit);
        Assert.Equal(.3m, withGoal.BrandContributionMarginGoal);

        var withoutGoal = TargetProjectionEngine.Project(Goal(500_000), new Deal { Name = "Anlaşma", DealType = DealType.FixedRetainer, MonthlyRetainer = 10_000 });
        Assert.True(withoutGoal.Calculable);
        Assert.Equal(10_000m, withoutGoal.OvoFee);
        Assert.Null(withoutGoal.BrandContributionProfit);
        Assert.Null(withoutGoal.BrandContributionMarginGoal);
    }
}
