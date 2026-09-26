using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class PipelineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Measurement_start_never_reports_waiting_time()
    {
        var row = new BrandStageHistory { Stage = LeadStage.New, EntryKnown = false, EnteredAt = Now.AddDays(-30) };
        Assert.Null(Pipeline.StageDays(row, Now));
        row.EntryKnown = true;
        Assert.Equal(30, Pipeline.StageDays(row, Now));
    }

    [Fact]
    public void Durations_use_whole_days_and_never_go_negative()
    {
        var closed = new BrandStageHistory { EntryKnown = true, EnteredAt = Now.AddDays(-2.5), ExitedAt = Now };
        Assert.Equal(2, Pipeline.StageDays(closed, Now));
        var open = new BrandStageHistory { EntryKnown = true, EnteredAt = Now.AddDays(-0.25) };
        Assert.Equal(0, Pipeline.StageDays(open, Now));
        var clockAhead = new BrandStageHistory { EntryKnown = true, EnteredAt = Now.AddDays(3), ExitedAt = Now };
        Assert.Equal(0, Pipeline.StageDays(clockAhead, Now));
    }

    [Fact]
    public void Loss_is_refused_without_reason_twice_or_when_a_deal_exists()
    {
        Assert.NotNull(Pipeline.LossError(false, false, " "));
        Assert.NotNull(Pipeline.LossError(false, false, new string('x', 1001)));
        Assert.NotNull(Pipeline.LossError(true, false, "Bütçe yetmedi"));
        Assert.NotNull(Pipeline.LossError(false, true, "Bütçe yetmedi"));
        Assert.Null(Pipeline.LossError(false, false, "Bütçe yetmedi"));
        Assert.Null(Pipeline.CancelLossError(true));
        Assert.NotNull(Pipeline.CancelLossError(false));
    }

    [Fact]
    public void Conversion_rate_is_null_until_something_is_reachable()
    {
        Assert.Null(Pipeline.ConversionRate(0, 0));
        Assert.Null(Pipeline.ConversionRate(0, 3));
        Assert.Equal(50.0m, Pipeline.ConversionRate(2, 1));
        Assert.Equal(100.0m, Pipeline.ConversionRate(3, 3));
    }

    [Fact]
    public void Stage_wait_separates_measured_days_from_unknown_ones()
    {
        var current = new List<(LeadStage Stage, int? Days)>
        {
            (LeadStage.New, 3), (LeadStage.New, null), (LeadStage.New, 7), (LeadStage.Contacted, null)
        };
        var wait = Pipeline.Wait(current, LeadStage.New);
        Assert.Equal(3, wait.Open);
        Assert.Equal(2, wait.Known);
        Assert.Equal(1, wait.Unknown);
        Assert.Equal(5.0m, wait.AverageDays);
        Assert.Equal(7, wait.LongestDays);
        var empty = Pipeline.Wait(current, LeadStage.OnHold);
        Assert.Equal(0, empty.Open);
        Assert.Equal(0, empty.Unknown);
        Assert.Null(empty.AverageDays);
        Assert.Equal(0, empty.LongestDays);
    }

    [Fact]
    public void Source_channels_have_plain_turkish_labels()
    {
        Assert.Equal("Belirtilmedi", Pipeline.SourceLabel(LeadSource.Unspecified));
        Assert.Equal("Referans / tavsiye", Pipeline.SourceLabel(LeadSource.Referral));
        Assert.Equal("Web sitesinden gelen talep", Pipeline.SourceLabel(LeadSource.InboundWebsite));
        Assert.Equal("Diğer", Pipeline.SourceLabel(LeadSource.Other));
    }
}
