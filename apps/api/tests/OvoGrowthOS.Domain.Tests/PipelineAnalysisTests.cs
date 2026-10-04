using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class PipelineAnalysisTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);
    private static readonly Guid BrandA = Guid.NewGuid();
    private static readonly Guid BrandB = Guid.NewGuid();
    private static readonly Guid BrandD = Guid.NewGuid();

    private static BrandStageHistory Row(Guid brand, LeadStage stage, DateTimeOffset entered, DateTimeOffset? exited = null) =>
        new() { BrandId = brand, Stage = stage, EntryKnown = true, EnteredAt = entered, ExitedAt = exited };

    private static PipelineAnalysisReport Build(int months = 6)
    {
        var followUps = new List<BrandFollowUp>
        {
            new() { BrandId = BrandD, Stage = LeadStage.New, SourceChannel = LeadSource.Referral,
                LostOn = new DateOnly(2026, 9, 10), LostReason = "Bütçe yetmedi" },
            new() { BrandId = Guid.NewGuid(), Stage = LeadStage.Contacted, SourceChannel = LeadSource.Event,
                LostOn = new DateOnly(2026, 4, 1), LostReason = "Pencere kapandı" } // pencere dışı
        };
        var histories = new List<BrandStageHistory>
        {
            Row(BrandA, LeadStage.New, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero)),
            Row(BrandA, LeadStage.Contacted, new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero)),
            Row(BrandB, LeadStage.New, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero)),
            Row(BrandD, LeadStage.New, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            Row(Guid.NewGuid(), LeadStage.New, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)) // pencere dışı giriş
        };
        return PipelineAnalysis.Build(Today, months, followUps, histories, [BrandB]);
    }

    [Fact]
    public void Losses_are_grouped_with_labels_shares_and_window()
    {
        var report = Build();
        Assert.Equal(1, report.TotalLosses); // pencere dışı kayıp sayılmaz
        Assert.Equal(6, report.WindowMonths);

        var stage = Assert.Single(report.ByStage);
        Assert.Equal("New", stage.Key);
        Assert.Equal("Yeni aday", stage.Label);
        Assert.Equal(1, stage.Count);
        Assert.Equal(100m, stage.Share);

        var source = Assert.Single(report.BySource);
        Assert.Equal("Referans / tavsiye", source.Label);
        Assert.Equal(100m, source.Share);

        Assert.Equal(6, report.ByMonth.Count);
        Assert.Equal(1, report.ByMonth.Single(x => x.Key == "2026-09").Count);
        Assert.Equal("Eylül 2026", report.ByMonth.Single(x => x.Key == "2026-09").Label);
        Assert.All(report.ByMonth.Where(x => x.Key != "2026-09"), x => Assert.Equal(0, x.Count));
    }

    [Fact]
    public void Funnel_counts_entered_converted_lost_and_open_per_stage()
    {
        var report = Build();
        var newStage = report.Funnel.Single(x => x.Stage == LeadStage.New);
        Assert.Equal(3, newStage.Entered); // A, B, D — pencere dışındaki giriş sayılmaz
        Assert.Equal(1, newStage.Converted); // B anlaşmaya döndü
        Assert.Equal(1, newStage.Lost); // D kayıp
        Assert.Equal(33.3m, newStage.ConversionRate);
        Assert.Equal(0, newStage.Open); // D kayıp, A ve B New'den çıkmış

        var contacted = report.Funnel.Single(x => x.Stage == LeadStage.Contacted);
        Assert.Equal(1, contacted.Entered);
        Assert.Equal(0, contacted.Converted);
        Assert.Equal(1, contacted.Open); // A hâlâ açık aşamada

        var meeting = report.Funnel.Single(x => x.Stage == LeadStage.MeetingPlanned);
        Assert.Equal(0, meeting.Entered);
        Assert.Null(meeting.ConversionRate); // veri yoksa oran uydurulmaz
    }

    [Fact]
    public void Report_explains_that_it_never_changes_records()
    {
        var report = Build();
        Assert.Equal(3, report.Notes.Count);
        Assert.Contains(report.Notes, x => x.Contains("yalnız bilgi verir"));
        Assert.Contains(report.Notes, x => x.Contains("birbirine bağlı değildir"));
        Assert.Contains(report.Notes, x => x.Contains("kayıp olarak sayılmaz"));
    }
}
