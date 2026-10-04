using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class PreScreeningTests
{
    private static PreScreenInput Complete() => new(
        Guid.NewGuid(), "Aura Spor", "Ayşe Yılmaz", "ayse@aura.com", "aura.com", "Spor",
        LeadSource.Referral, "Demo planla", Guid.NewGuid(), true, 4, new DateOnly(2026, 10, 1));

    [Fact]
    public void Complete_lead_is_ready_with_no_issues()
    {
        var item = PreScreening.Evaluate(Complete());
        Assert.Equal("ready", item.State);
        Assert.Equal(0, item.IssueCount);
        Assert.Empty(item.Issues);
        Assert.Equal(4, item.StageDays);
    }

    [Fact]
    public void Missing_pieces_are_listed_as_turkish_issues()
    {
        var input = Complete() with
        {
            BrandName = "Boş Aday", ContactName = "", ContactEmail = "", Website = "", Industry = "",
            SourceChannel = LeadSource.Unspecified, NextStep = "", OwnerId = null, LastContactOn = null,
            StageDays = 45
        };
        var item = PreScreening.Evaluate(input);
        Assert.Equal("attention", item.State);
        var codes = item.Issues.Select(x => x.Code).ToHashSet();
        foreach (var expected in new[] { "contact", "website", "industry", "source", "nextStep", "owner", "noContact", "stale" })
            Assert.Contains(expected, codes);
        Assert.Contains(item.Issues, x => x.Label.Contains("45 günden uzun"));
    }

    [Fact]
    public void Stale_warning_requires_known_stage_entry()
    {
        var unknown = Complete() with { StageEntryKnown = false, StageDays = 90 };
        Assert.DoesNotContain(PreScreening.Evaluate(unknown).Issues, x => x.Code == "stale");
        var recent = Complete() with { StageDays = 30 };
        Assert.DoesNotContain(PreScreening.Evaluate(recent).Issues, x => x.Code == "stale");
    }

    [Fact]
    public void Report_puts_attention_first_and_explains_limits()
    {
        var ready = Complete() with { BrandId = Guid.NewGuid(), BrandName = "Hazır Marka" };
        var attention = Complete() with { BrandId = Guid.NewGuid(), BrandName = "Eksik Marka", Industry = "", OwnerId = null };
        var report = PreScreening.Build([ready, attention]);
        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.Ready);
        Assert.Equal(1, report.Attention);
        Assert.Equal("Eksik Marka", report.Items[0].BrandName);
        Assert.Equal("ready", report.Items[1].State);
        Assert.Contains(report.Notes, x => x.Contains("yalnız eksik bilgi"));
        Assert.Contains(report.Notes, x => x.Contains("tavsiyesi değildir"));
    }
}
