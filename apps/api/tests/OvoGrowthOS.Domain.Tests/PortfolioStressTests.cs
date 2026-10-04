using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class PortfolioStressTests
{
    private static Deal DealOf(DealType type, decimal share = 0, decimal retainer = 0, decimal minimum = 0) => new()
    {
        BrandId = Guid.NewGuid(), Name = "Stres anlaşması", DealType = type, Currency = "TRY",
        RevenueShareRate = share, MonthlyRetainer = retainer, MinimumMonthlyFee = minimum,
        CommissionTiersJson = "[]", StartDate = new DateOnly(2026, 1, 1)
    };

    private static StressRow Row(string name, decimal net, decimal comm = 0, decimal contribution = 0,
        decimal fee = 0, decimal profit = 0, Deal? deal = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, net, comm, contribution, fee, profit, deal);

    [Fact]
    public void Empty_period_returns_zeroes_and_says_nothing_to_calculate()
    {
        var result = PortfolioStress.Calculate([], -0.2m);
        Assert.Equal(0, result.RecordCount);
        Assert.Equal(0, result.BaseRevenue); Assert.Equal(0, result.FeeDelta); Assert.Equal(0, result.ProfitDelta);
        Assert.Contains(result.Notes, n => n.Contains("kayıt yok"));
    }

    [Fact]
    public void Shock_hits_only_the_largest_brand_and_keeps_every_total_consistent()
    {
        var big = DealOf(DealType.FlatRevenueShare, share: .10m);
        var small = DealOf(DealType.FlatRevenueShare, share: .10m);
        var rows = new List<StressRow>
        {
            Row("Büyük", 300_000, 300_000, 100_000, 30_000, 20_000, big),
            Row("Küçük", 100_000, 100_000, 50_000, 10_000, 8_000, small)
        };
        var result = PortfolioStress.Calculate(rows, -.20m);

        Assert.Equal("Büyük", result.BrandName); Assert.True(result.BrandHasAgreement);
        Assert.Equal(400_000m, result.BaseRevenue);
        Assert.Equal(-60_000m, result.RevenueDelta);
        Assert.Equal(340_000m, result.SimRevenue);
        Assert.Equal(40_000m, result.BaseFee);
        Assert.Equal(-6_000m, result.FeeDelta);
        Assert.Equal(34_000m, result.SimFee);
        Assert.Equal(28_000m, result.BaseProfit);
        Assert.Equal(22_000m, result.SimProfit);
        Assert.Equal(-6_000m, result.ProfitDelta);
        Assert.Equal(240_000m, result.BrandSimRevenue);
        Assert.Equal(30_000m, result.BrandBaseFee); Assert.Equal(24_000m, result.BrandSimFee);
        Assert.Equal(result.FeeDelta, result.ProfitDelta);
        Assert.Contains(result.Notes, n => n.Contains("yalnız simülasyondur"));
        Assert.Contains(result.Notes, n => n.Contains("yeniden hesaplandı"));
    }

    [Fact]
    public void Fixed_retainer_model_absorbs_the_whole_shock_in_hakedis()
    {
        var result = PortfolioStress.Calculate([Row("Sabit", 200_000, 200_000, 60_000, 25_000, 15_000,
            DealOf(DealType.FixedRetainer, retainer: 25_000))], -.50m);
        Assert.Equal(-100_000m, result.RevenueDelta);
        Assert.Equal(0, result.FeeDelta);
        Assert.Equal(0, result.ProfitDelta);
        Assert.Contains(result.Notes, n => n.Contains("hakedişi etkilemiyor"));
    }

    [Fact]
    public void Minimum_fee_floor_can_swallow_a_deep_drop()
    {
        var result = PortfolioStress.Calculate([Row("Güvenceli", 100_000, 100_000, 40_000, 8_000, 5_000,
            DealOf(DealType.MinimumFeePlusRevenueShare, share: .05m, minimum: 8_000))], -.90m);
        Assert.Equal(10_000m, result.SimRevenue);
        Assert.Equal(0, result.FeeDelta);
        Assert.Contains(result.Notes, n => n.Contains("hakedişi etkilemiyor"));
    }

    [Fact]
    public void Brand_without_an_agreement_keeps_the_revenue_effect_only()
    {
        var result = PortfolioStress.Calculate([Row("Anlaşmasız", 150_000, 0, 0, 0, 0)], -.20m);
        Assert.False(result.BrandHasAgreement);
        Assert.Equal(-30_000m, result.RevenueDelta);
        Assert.Equal(0, result.FeeDelta);
        Assert.Contains(result.Notes, n => n.Contains("anlaşma kaydı yok"));
    }

    [Fact]
    public void Missing_commissionable_revenue_blocks_the_fee_recalculation()
    {
        var result = PortfolioStress.Calculate([Row("Boş ciro", 120_000, 0, 0, 0, 0, DealOf(DealType.FlatRevenueShare, share: .10m))], -.10m);
        Assert.Equal(0, result.FeeDelta);
        Assert.Contains(result.Notes, n => n.Contains("hesaplanabilir ciro boş"));
    }

    [Fact]
    public void Ties_break_on_the_smallest_brand_id()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var rows = new List<StressRow> { Row("B", 100_000, id: second), Row("A", 100_000, id: first) };
        var result = PortfolioStress.Calculate(rows, -.20m);
        Assert.Equal(new[] { first, second }.Min(), result.BrandId);
    }

    [Fact]
    public void Boundary_shocks_are_accepted_and_zero_means_no_change()
    {
        var deal = DealOf(DealType.FlatRevenueShare, share: .10m);
        var rows = new List<StressRow> { Row("Marka", 100_000, 100_000, 40_000, 10_000, 6_000, deal) };
        var extreme = PortfolioStress.Calculate(rows, PortfolioStress.MinShock);
        Assert.Equal(10_000m, extreme.SimRevenue);
        Assert.Equal(-9_000m, extreme.FeeDelta);
        var none = PortfolioStress.Calculate(rows, 0m);
        Assert.Equal(0, none.RevenueDelta); Assert.Equal(0, none.FeeDelta); Assert.Equal(0, none.ProfitDelta);
        Assert.Equal(none.BaseRevenue, none.SimRevenue); Assert.Equal(none.BaseFee, none.SimFee);
        Assert.Equal(none.BaseProfit, none.SimProfit);
    }

    [Fact]
    public void Out_of_range_shocks_are_rejected()
    {
        var rows = new List<StressRow> { Row("Marka", 100_000) };
        Assert.Throws<ArgumentOutOfRangeException>(() => PortfolioStress.Calculate(rows, -0.95m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PortfolioStress.Calculate(rows, 0.10m));
    }
}
