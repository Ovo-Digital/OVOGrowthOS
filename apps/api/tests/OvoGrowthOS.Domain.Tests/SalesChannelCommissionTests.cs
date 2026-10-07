using System.Text.Json;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class SalesChannelCommissionTests
{
    private static readonly Guid Web = Guid.NewGuid();
    private static readonly Guid Trendyol = Guid.NewGuid();

    [Fact] public void Channel_rates_apply_per_channel()
    {
        var deal = Deal(DealType.FlatRevenueShare, rate: .05m);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 600_000m), new ChannelRevenue(Trendyol, "Trendyol", 400_000m) };
        var overrides = new Dictionary<Guid, decimal> { [Trendyol] = .03m };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, overrides);
        Assert.Equal(42_000m, x.Result.FinalFee);
        Assert.Equal(30_000m, x.Shares.Single(s => s.SalesChannelId == Web).Fee);
        Assert.Equal(12_000m, x.Shares.Single(s => s.SalesChannelId == Trendyol).Fee);
        Assert.Equal(.042m, x.Result.EffectiveRate);
    }

    [Fact] public void Missing_override_falls_back_to_deal_rate()
    {
        var deal = Deal(DealType.FlatRevenueShare, rate: .05m);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 600_000m), new ChannelRevenue(Trendyol, "Trendyol", 400_000m) };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, new Dictionary<Guid, decimal>());
        Assert.Equal(50_000m, x.Result.FinalFee);
    }

    [Fact] public void Minimum_fee_is_shared_by_revenue_weight()
    {
        var deal = Deal(DealType.MinimumFeePlusRevenueShare, rate: .05m, minimum: 45_000m);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 480_000m), new ChannelRevenue(Trendyol, "Trendyol", 320_000m) };
        var overrides = new Dictionary<Guid, decimal> { [Trendyol] = .03m };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, overrides);
        // 480.000 x %5 = 24.000, 320.000 x %3 = 9.600; toplam 33.600 < 45.000 asgari ücret.
        Assert.Equal(45_000m, x.Result.FinalFee);
        Assert.Equal(27_000m, x.Shares.Single(s => s.SalesChannelId == Web).Fee);
        Assert.Equal(18_000m, x.Shares.Single(s => s.SalesChannelId == Trendyol).Fee);
        Assert.Equal(45_000m, x.Shares.Sum(s => s.Fee));
    }

    [Fact] public void Retainer_is_distributed_and_sums_to_final_fee()
    {
        var deal = Deal(DealType.RetainerPlusRevenueShare, rate: .05m, retainer: 20_000m);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 600_000m), new ChannelRevenue(Trendyol, "Trendyol", 300_000m) };
        var overrides = new Dictionary<Guid, decimal> { [Trendyol] = .02m };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, overrides);
        // 30.000 + 6.000 + 20.000 sabit ücret = 56.000.
        Assert.Equal(56_000m, x.Result.FinalFee);
        Assert.Equal(56_000m, x.Shares.Sum(s => s.Fee));
    }

    [Fact] public void Tiered_model_uses_total_and_distributes_for_display()
    {
        var deal = Deal(DealType.TieredRevenueShare, withTiers: true);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 1_200_000m), new ChannelRevenue(Trendyol, "Trendyol", 800_000m) };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, new Dictionary<Guid, decimal> { [Web] = .10m });
        var expected = DealCommissionCalculator.Calculate(deal, 2_000_000m, 0);
        Assert.Equal(expected.FinalFee, x.Result.FinalFee);
        Assert.Equal(117_500m, x.Result.FinalFee);
        Assert.Equal(x.Result.FinalFee, x.Shares.Sum(s => s.Fee));
    }

    [Fact] public void Minimum_with_tiers_keeps_tiered_total()
    {
        var deal = Deal(DealType.MinimumFeePlusRevenueShare, rate: .05m, minimum: 45_000m, withTiers: true);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 480_000m), new ChannelRevenue(Trendyol, "Trendyol", 320_000m) };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, new Dictionary<Guid, decimal> { [Trendyol] = .03m });
        // Kademeli oran tanımlıyken toplam üzerinden kademeli hesap korunur: 40.000 + 18.000 = 58.000.
        Assert.Equal(58_000m, x.Result.FinalFee);
        Assert.Equal(58_000m, x.Shares.Sum(s => s.Fee));
    }

    [Fact] public void Zero_revenue_gives_zero_fee()
    {
        var deal = Deal(DealType.FlatRevenueShare, rate: .05m);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 0m), new ChannelRevenue(Trendyol, "Trendyol", 0m) };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, new Dictionary<Guid, decimal>());
        Assert.Equal(0m, x.Result.FinalFee);
        Assert.All(x.Shares, s => Assert.Equal(0m, s.Fee));
    }

    [Fact] public void Rounding_remainder_keeps_sum_exact()
    {
        var deal = Deal(DealType.RetainerPlusRevenueShare, rate: .05m, retainer: 10m);
        var channels = new[] { new ChannelRevenue(Web, "Web sitesi", 1m), new ChannelRevenue(Trendyol, "Trendyol", 2m) };
        var x = DealCommissionCalculator.CalculateWithChannels(deal, channels, 0, new Dictionary<Guid, decimal>());
        Assert.Equal(x.Result.FinalFee, x.Shares.Sum(s => s.Fee));
    }

    [Fact] public void Performance_totals_come_from_channel_lines()
    {
        var p = new MonthlyPerformance { BrandId = Guid.NewGuid(), DealId = Guid.NewGuid(), Year = 2026, Month = 8 };
        var lines = new[]
        {
            Line(Web, gross: 700_000m, vat: 100_000m, refunds: 20_000m),
            Line(Trendyol, gross: 500_000m, vat: 70_000m, refunds: 30_000m),
        };
        var names = new Dictionary<Guid, string> { [Web] = "Web sitesi", [Trendyol] = "Trendyol" };
        MonthlyPerformanceCalculator.CalculateWithChannels(p, Deal(DealType.FlatRevenueShare, rate: .05m), lines,
            new Dictionary<Guid, decimal> { [Trendyol] = .03m }, names);
        Assert.Equal(1_200_000m, p.GrossSales);
        Assert.Equal(170_000m, p.Vat);
        Assert.Equal(1_200_000m - 170_000m - 50_000m, p.NetRevenue);
        Assert.Equal(p.CommissionableRevenue, lines.Sum(x => x.CommissionableRevenue));
        // Web: 580.000 x %5 = 29.000; Trendyol: 400.000 x %3 = 12.000.
        Assert.Equal(41_000m, p.OvoFee);
        var doc = JsonDocument.Parse(p.CommissionBreakdownJson);
        Assert.Equal(2, doc.RootElement.GetProperty("channels").GetArrayLength());
    }

    [Fact] public void Empty_channels_behave_like_single_total()
    {
        var p = Performance();
        var single = Performance();
        var deal = Deal(DealType.FlatRevenueShare, rate: .05m);
        var a = MonthlyPerformanceCalculator.CalculateWithChannels(p, deal, []);
        var b = MonthlyPerformanceCalculator.Calculate(single, deal);
        Assert.Equal(b.FinalFee, a.FinalFee);
        Assert.Equal(single.OvoFee, p.OvoFee);
    }

    private static MonthlyPerformanceChannel Line(Guid channelId, decimal gross, decimal vat, decimal refunds) =>
        new() { SalesChannelId = channelId, GrossSales = gross, Vat = vat, Refunds = refunds };

    private static Deal Deal(DealType type, decimal rate = 0, decimal minimum = 0, decimal retainer = 0, bool withTiers = false) => new()
    {
        BrandId = Guid.NewGuid(), EvaluationId = Guid.NewGuid(), Name = "Option", DealType = type,
        RevenueShareRate = rate, MinimumMonthlyFee = minimum, MonthlyRetainer = retainer,
        EstimatedMonthlyInternalCost = 25_000, CommissionTiersJson = withTiers ? JsonSerializer.Serialize(
            new[] { new CommissionTier(0, 500_000, .08m), new CommissionTier(500_000, 1_500_000, .06m), new CommissionTier(1_500_000, null, .035m) }) : "[]",
    };

    private static MonthlyPerformance Performance() => new()
    {
        BrandId = Guid.NewGuid(), DealId = Guid.NewGuid(), Year = 2026, Month = 8,
        GrossSales = 1_000_000, Vat = 150_000, Refunds = 50_000, Orders = 600, Sessions = 30_000, NewCustomers = 300,
        Cogs = 300_000, PaymentFees = 20_000, FulfillmentCosts = 30_000, MetaSpend = 120_000, GoogleSpend = 80_000,
    };
}
