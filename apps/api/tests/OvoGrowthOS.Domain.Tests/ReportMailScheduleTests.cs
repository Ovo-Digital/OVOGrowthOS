using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class ReportMailScheduleTests
{
    private static DateTimeOffset At(int y, int m, int d, int h) => new(y, m, d, h, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Occurrence_uses_turkish_wall_clock_and_clamps_short_months()
    {
        Assert.Equal(At(2026, 11, 5, 9), ReportMailSchedule.ToInstant(2026, 11, 5, 9));
        Assert.Equal(28, ReportMailSchedule.ToInstant(2026, 2, 31, 9).Day);
        Assert.Equal(9, ReportMailSchedule.ToInstant(2026, 2, 31, 9).Hour);
        Assert.Equal(23, ReportMailSchedule.ToInstant(2026, 11, 5, 99).Hour);
        Assert.Equal(0, ReportMailSchedule.ToInstant(2026, 11, 5, -4).Hour);
        Assert.Equal(TimeSpan.FromHours(3), ReportMailSchedule.ToInstant(2026, 11, 5, 9).Offset);
    }

    [Fact]
    public void Occurrence_for_period_points_to_the_following_month_and_wraps_the_year()
    {
        Assert.Equal(At(2026, 11, 5, 9), ReportMailSchedule.OccurrenceFor(2026, 10, 5, 9));
        Assert.Equal(At(2027, 1, 5, 9), ReportMailSchedule.OccurrenceFor(2026, 12, 5, 9));
        Assert.Equal((2026, 12), ReportMailSchedule.Previous(2027, 1));
        Assert.Equal((2027, 1), ReportMailSchedule.Following(2026, 12));
        Assert.Equal((2026, 10), ReportMailSchedule.TargetOf(At(2026, 11, 5, 9)));
    }

    [Theory]
    [InlineData(2026, 11, 4, 8, 0, 0)]
    [InlineData(2026, 11, 5, 8, 0, 0)]
    [InlineData(2026, 11, 5, 9, 10, 2026)]
    [InlineData(2026, 11, 7, 23, 10, 2026)]
    [InlineData(2026, 11, 8, 8, 10, 2026)]
    [InlineData(2026, 11, 8, 9, 0, 0)]
    [InlineData(2026, 11, 8, 10, 0, 0)]
    public void Current_window_opens_on_the_configured_day_and_closes_after_three_days(int y, int m, int d, int h, int expectedMonth, int expectedYear)
    {
        var window = ReportMailSchedule.CurrentWindow(At(y, m, d, h).AddMinutes(15), 5, 9);
        if (expectedMonth == 0) { Assert.Null(window); return; }
        Assert.NotNull(window);
        Assert.Equal(expectedMonth, window.TargetMonth);
        Assert.Equal(expectedYear, window.TargetYear);
        Assert.Equal(ReportMailSchedule.CatchUpDays, (int)(window.EndsAt - window.OccurrenceAt).TotalDays);
    }

    [Fact]
    public void Short_month_schedule_that_spills_into_the_next_month_is_still_the_previous_period()
    {
        var window = ReportMailSchedule.CurrentWindow(At(2026, 4, 2, 12), 31, 9);
        Assert.NotNull(window);
        Assert.Equal(2026, window.TargetYear);
        Assert.Equal(2, window.TargetMonth);
        Assert.Equal(31, window.OccurrenceAt.Day);
        Assert.Equal(3, window.OccurrenceAt.Month);
    }

    [Fact]
    public void Owns_email_only_between_the_lead_time_and_the_end_of_the_window()
    {
        var at = ReportMailSchedule.OccurrenceFor(2026, 10, 5, 9);
        Assert.False(ReportMailSchedule.OwnsEmail(false, 5, 9, at, 2026, 10));
        Assert.False(ReportMailSchedule.OwnsEmail(true, 5, 9, at.AddDays(-8), 2026, 10));
        Assert.True(ReportMailSchedule.OwnsEmail(true, 5, 9, at.AddDays(-7), 2026, 10));
        Assert.True(ReportMailSchedule.OwnsEmail(true, 5, 9, at, 2026, 10));
        Assert.True(ReportMailSchedule.OwnsEmail(true, 5, 9, at.AddDays(3), 2026, 10));
        Assert.False(ReportMailSchedule.OwnsEmail(true, 5, 9, at.AddDays(3).AddSeconds(1), 2026, 10));
        Assert.False(ReportMailSchedule.OwnsEmail(true, 5, 9, at, 2026, 9));
    }

    [Fact]
    public void Next_occurrence_is_never_in_the_past()
    {
        Assert.Equal(At(2026, 11, 5, 9), ReportMailSchedule.NextOccurrence(At(2026, 11, 4, 8), 5, 9));
        var after = ReportMailSchedule.NextOccurrence(At(2026, 11, 5, 10), 5, 9);
        Assert.Equal(At(2026, 12, 5, 9), after);
        Assert.True(after > At(2026, 11, 5, 10));
    }
}
