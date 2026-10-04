using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class AdCampaignTests
{
    [Fact]
    public void Normalize_merges_repeated_names_fixes_currency_and_drops_empty_rows()
    {
        var rows = AdCampaigns.Normalize(new[]
        {
            new AdCampaignResult("Yaz kampanyası", 100m, "try"),
            new AdCampaignResult("Yaz kampanyası", 50m, "TRY"),
            new AdCampaignResult("  ", 90m, "USD"),
            new AdCampaignResult("Yeni ürün", -10m, "")
        }, "USD");

        Assert.Equal(2, rows.Count);
        Assert.Equal("Yaz kampanyası", rows[0].CampaignName);
        Assert.Equal(150m, rows[0].Spend);
        Assert.Equal("TRY", rows[0].Currency);
        Assert.Equal("Yeni ürün", rows[1].CampaignName);
        Assert.Equal(0m, rows[1].Spend);
        Assert.Equal("USD", rows[1].Currency);
    }

    [Fact]
    public void Normalize_caps_very_long_campaign_names_at_the_database_limit()
    {
        var rows = AdCampaigns.Normalize(new[] { new AdCampaignResult(new string('a', 340), 10m, "TRY") }, "TRY");
        Assert.Single(rows);
        Assert.Equal(AdCampaigns.MaxCampaignNameLength, rows[0].CampaignName.Length);
    }

    [Fact]
    public void Normalize_handles_a_missing_result_list()
    {
        Assert.Empty(AdCampaigns.Normalize(null, "TRY"));
    }

    [Fact]
    public void Groups_are_ordered_by_spend_and_totals_are_split_by_platform_and_currency()
    {
        Guid brandId = Guid.NewGuid();
        var rows = new List<AdCampaignSpend>
        {
            new() { BrandId = brandId, Year = 2026, Month = 9, Platform = AdPlatform.Meta, CampaignName = "Küçük", Spend = 10m, Currency = "TRY" },
            new() { BrandId = brandId, Year = 2026, Month = 9, Platform = AdPlatform.Meta, CampaignName = "Büyük", Spend = 90m, Currency = "TRY" },
            new() { BrandId = brandId, Year = 2026, Month = 9, Platform = AdPlatform.Google, CampaignName = "Arama", Spend = 40m, Currency = "USD" }
        };

        var group = AdCampaigns.Group(rows);
        Assert.Equal(new[] { "Büyük", "Arama", "Küçük" }, group.Select(x => x.CampaignName).ToArray());
        Assert.Equal("Meta", group[0].Platform);

        var totals = AdCampaigns.Totals(rows);
        Assert.Equal(2, totals.Count);
        Assert.Equal(100m, totals[0].Spend);
        Assert.Equal("Meta", totals[0].Platform);
        Assert.Equal("TRY", totals[0].Currency);
        Assert.Equal("Google", totals[1].Platform);
        Assert.Equal(40m, totals[1].Spend);
    }
}
