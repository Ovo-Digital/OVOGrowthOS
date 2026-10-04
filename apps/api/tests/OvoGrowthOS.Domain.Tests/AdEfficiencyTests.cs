using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class AdEfficiencyTests
{
    [Fact]
    public void Mer_is_never_computed_without_ad_spend()
    {
        Assert.Null(AdEfficiency.Mer(50000m, 0m));
        Assert.Null(AdEfficiency.Mer(50000m, -1m));
        Assert.Equal(2.5m, AdEfficiency.Mer(50000m, 20000m));
    }

    [Theory]
    [InlineData(2.5, 2, "strong")]
    [InlineData(2.0, 2, "strong")]
    [InlineData(1.8, 2, "watch")]
    [InlineData(1.79, 2, "risk")]
    [InlineData(1.0, 2, "risk")]
    public void Bands_compare_mer_against_breakeven(decimal mer, decimal breakEven, string expected) =>
        Assert.Equal(expected, AdEfficiency.Band(mer, breakEven));

    [Fact]
    public void Unknown_band_when_target_or_data_is_missing()
    {
        Assert.Equal("unknown", AdEfficiency.Band(null, 2m));
        Assert.Equal("unknown", AdEfficiency.Band(1.5m, null));
        Assert.Equal("unknown", AdEfficiency.Band(1.5m, 0m));
        Assert.Equal("Güçlü", AdEfficiency.BandLabel("strong"));
        Assert.Equal("Riskli", AdEfficiency.BandLabel("risk"));
        Assert.Equal("Bilinmiyor", AdEfficiency.BandLabel("unknown"));
    }

    [Fact]
    public void Summary_uses_latest_point_and_states_read_only_scope()
    {
        var points = new List<AdEfficiencyPoint>
        {
            new(2026, 9, 20000m, 45000m, 2.25m),
            new(2026, 8, 15000m, 20000m, 1.33m)
        };
        var summary = AdEfficiency.Build(2m, points);
        Assert.Equal("strong", summary.BandCode);
        Assert.Equal(2.25m, summary.LatestMer);
        Assert.Equal(2m, summary.BreakEvenMer);
        Assert.Equal("09/2026", summary.PeriodLabel);
        Assert.Contains("kilitlenmiş", summary.Note);
        Assert.Contains("değiştirmez", summary.Note);
        Assert.Contains("2,25x", summary.BandDetail);
    }

    [Fact]
    public void Summary_explains_missing_data_instead_of_guessing()
    {
        var noData = AdEfficiency.Build(2m, []);
        Assert.Equal("unknown", noData.BandCode);
        Assert.Contains("yok", noData.BandDetail);
        Assert.Equal("-", noData.PeriodLabel);

        var noTarget = AdEfficiency.Build(null, [new AdEfficiencyPoint(2026, 9, 10000m, 12000m, 1.2m)]);
        Assert.Equal("unknown", noTarget.BandCode);
        Assert.Contains("değerlendirme analizi gerekli", noTarget.BandDetail);

        var noSpend = AdEfficiency.Build(2m, [new AdEfficiencyPoint(2026, 9, 0m, 12000m, null)]);
        Assert.Equal("unknown", noSpend.BandCode);
        Assert.Contains("reklam harcaması yok", noSpend.BandDetail);
    }
}
