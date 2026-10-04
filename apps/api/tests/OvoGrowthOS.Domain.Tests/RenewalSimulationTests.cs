using System.Text.Json;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class RenewalSimulationTests
{
    private static Deal DealOf(DealType type, decimal retainer = 0, decimal share = 0, decimal minimum = 0,
        decimal incremental = 0, decimal profitShare = 0, string tiers = "[]") => new()
    {
        BrandId = Guid.NewGuid(), EvaluationId = Guid.NewGuid(), Name = "Yenileme Anlaşması", DealType = type,
        Currency = "TRY", MonthlyRetainer = retainer, RevenueShareRate = share, MinimumMonthlyFee = minimum,
        IncrementalRate = incremental, ProfitShareRate = profitShare, CommissionTiersJson = tiers, StartDate = new DateOnly(2026, 1, 1)
    };

    private static string Tiers(params (decimal Lower, decimal? Upper, decimal Rate)[] rows) =>
        JsonSerializer.Serialize(rows.Select(x => new { lowerBound = x.Lower, upperBound = x.Upper, rate = x.Rate }).ToList(), DealCommissionCalculator.JsonOptions);

    [Fact]
    public void Zero_basis_keeps_every_side_at_zero()
    {
        var result = RenewalSimulation.Simulate(DealOf(DealType.FlatRevenueShare, share: .10m), 0, 0, .20m, 1, 500);
        Assert.Equal(0, result.BaseRevenue); Assert.Equal(0, result.BaseOvoFee);
        Assert.Equal(0, result.SimRevenue); Assert.Equal(0, result.SimOvoFee);
        Assert.Equal(0, result.FeeDelta); Assert.Equal(0, result.BaseEffectiveRate); Assert.Equal(0, result.SimEffectiveRate);
    }

    [Fact]
    public void Flat_share_fee_and_brand_side_move_with_revenue_change()
    {
        var result = RenewalSimulation.Simulate(DealOf(DealType.FlatRevenueShare, share: .10m), 10000, 4000, -.50m, 0, 0);
        Assert.Equal(5000, result.SimRevenue); Assert.Equal(2000, result.SimContribution);
        Assert.Equal(1000, result.BaseOvoFee); Assert.Equal(500, result.SimOvoFee);
        Assert.Equal(-500, result.FeeDelta);
        Assert.Equal(3000, result.BaseBrandContribution); Assert.Equal(1500, result.SimBrandContribution);
        Assert.Equal(-1500, result.BrandContributionDelta);
        Assert.Equal(.10m, result.BaseEffectiveRate); Assert.Equal(.10m, result.SimEffectiveRate);
        Assert.True(result.ShareApplied); Assert.False(result.RetainerApplied);
    }

    [Fact]
    public void Share_points_are_added_to_the_rate_the_deal_actually_uses()
    {
        var flat = RenewalSimulation.Simulate(DealOf(DealType.FlatRevenueShare, share: .10m), 10000, 4000, 0, 2, 0);
        Assert.Equal(.12m, flat.SimulatedShareRate); Assert.Equal(1200, flat.SimOvoFee);

        var retainer = RenewalSimulation.Simulate(DealOf(DealType.RetainerPlusRevenueShare, retainer: 2000, share: .05m), 10000, 4000, 0, 0, 1000);
        Assert.Equal(2500, retainer.BaseOvoFee); Assert.Equal(3500, retainer.SimOvoFee);
        Assert.True(retainer.RetainerApplied); Assert.Equal(3000, retainer.SimulatedRetainer);

        var incremental = RenewalSimulation.Simulate(DealOf(DealType.IncrementalRevenueShare, incremental: .05m), 10000, 4000, 0, 3, 0);
        Assert.Equal(.05m * 10000, incremental.BaseOvoFee); Assert.Equal(.08m * 10000, incremental.SimOvoFee);
        Assert.Equal(.08m, incremental.SimulatedShareRate);
    }

    [Fact]
    public void Negative_share_points_stop_at_zero_and_say_so()
    {
        var result = RenewalSimulation.Simulate(DealOf(DealType.FlatRevenueShare, share: .05m), 10000, 4000, 0, -10, 0);
        Assert.Equal(0, result.SimulatedShareRate); Assert.Equal(0, result.SimOvoFee);
        Assert.Contains(result.Notes, n => n.Contains("sıfıra sabitlendi"));
    }

    [Fact]
    public void Minimum_fee_floor_survives_a_deep_revenue_drop()
    {
        var deal = DealOf(DealType.MinimumFeePlusRevenueShare, share: .05m, minimum: 800);
        var result = RenewalSimulation.Simulate(deal, 10000, 4000, -.90m, 0, 0);
        Assert.Equal(800, result.BaseOvoFee);
        Assert.Equal(1000, result.SimRevenue);
        Assert.Equal(800, result.SimOvoFee);
        Assert.Contains(result.Notes, n => n.Contains("Asgari aylık ücret"));
    }

    [Fact]
    public void Tiered_model_keeps_its_brackets_and_ignores_share_points()
    {
        var tiers = Tiers((0m, 1000m, .10m), (1000m, null, .20m));
        var deal = DealOf(DealType.TieredRevenueShare, tiers: tiers);
        var result = RenewalSimulation.Simulate(deal, 1000, 4000, .10m, 5, 0);
        Assert.False(result.ShareApplied);
        Assert.Contains(result.Notes, n => n.Contains("Kademeli pay modelinde"));
        Assert.Equal(100, result.BaseOvoFee);
        Assert.Equal(1100, result.SimRevenue);
        Assert.Equal(120, result.SimOvoFee);
        Assert.Equal(0, result.SimulatedShareRate);
    }

    [Fact]
    public void Contribution_profit_share_uses_contribution_and_never_reaches_full_share()
    {
        var deal = DealOf(DealType.ContributionProfitShare, retainer: 1000, profitShare: .20m);
        var result = RenewalSimulation.Simulate(deal, 10000, 5000, 0, 0, 0);
        Assert.Equal(2000, result.BaseOvoFee);
        var raised = RenewalSimulation.Simulate(deal, 10000, 5000, 0, 20, 0);
        Assert.Equal(.40m, raised.SimulatedShareRate);
        Assert.Equal(1000 + .40m * 5000, raised.SimOvoFee);
        var capped = RenewalSimulation.Simulate(DealOf(DealType.ContributionProfitShare, retainer: 1000, profitShare: .85m), 10000, 5000, 0, 20, 0);
        Assert.Equal(.99m, capped.SimulatedShareRate);
        Assert.Contains(capped.Notes, n => n.Contains("%99"));
    }

    [Fact]
    public void Fixed_retainer_ignores_revenue_and_share_but_follows_the_retainer()
    {
        var deal = DealOf(DealType.FixedRetainer, retainer: 3000);
        var result = RenewalSimulation.Simulate(deal, 10000, 4000, .50m, 5, 500);
        Assert.Equal(3000, result.BaseOvoFee); Assert.Equal(3500, result.SimOvoFee);
        Assert.False(result.ShareApplied); Assert.Contains(result.Notes, n => n.Contains("Sabit ücretli anlaşmada pay değişimi yoktur"));
    }

    [Fact]
    public void Out_of_range_inputs_are_rejected()
    {
        var deal = DealOf(DealType.FlatRevenueShare, share: .10m, retainer: 1000);
        Assert.Throws<ArgumentOutOfRangeException>(() => RenewalSimulation.Simulate(deal, 10000, 4000, -1m, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenewalSimulation.Simulate(deal, 10000, 4000, 4m, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenewalSimulation.Simulate(deal, 10000, 4000, 0, -11, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenewalSimulation.Simulate(deal, 10000, 4000, 0, 21, 0));
        var withRetainer = DealOf(DealType.RetainerPlusRevenueShare, retainer: 1000, share: .10m);
        Assert.Throws<ArgumentOutOfRangeException>(() => RenewalSimulation.Simulate(withRetainer, 10000, 4000, 0, 0, -5000));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenewalSimulation.Simulate(deal, -1, 4000, 0, 0, 0));
    }

    [Fact]
    public void Boundary_values_are_accepted_exactly()
    {
        var deal = DealOf(DealType.RetainerPlusRevenueShare, retainer: 1000, share: .10m);
        var min = RenewalSimulation.Simulate(deal, 10000, 4000, RenewalSimulation.MinRevenueChange, RenewalSimulation.MinSharePoints, -1000);
        Assert.Equal(1000, min.SimRevenue); Assert.Equal(0, min.SimOvoFee);
        var max = RenewalSimulation.Simulate(deal, 10000, 4000, RenewalSimulation.MaxRevenueChange, RenewalSimulation.MaxSharePoints, RenewalSimulation.MaxRetainerChange);
        Assert.Equal(40000, max.SimRevenue);
        Assert.Equal((.10m + .20m) * 40000 + (1000 + RenewalSimulation.MaxRetainerChange), max.SimOvoFee);
        var zero = RenewalSimulation.Simulate(deal, 10000, 4000, 0, 0, 0);
        Assert.Equal(0, zero.FeeDelta);
        Assert.Equal(zero.BaseOvoFee, zero.SimOvoFee);
        Assert.Equal(zero.BaseBrandContribution, zero.SimBrandContribution);
    }

    [Fact]
    public void Simulation_never_mutates_the_source_deal()
    {
        var deal = DealOf(DealType.RetainerPlusRevenueShare, retainer: 1000, share: .10m);
        RenewalSimulation.Simulate(deal, 10000, 4000, .30m, 5, 750);
        Assert.Equal(1000, deal.MonthlyRetainer);
        Assert.Equal(.10m, deal.RevenueShareRate);
        Assert.Equal(DealType.RetainerPlusRevenueShare, deal.DealType);
    }
}
