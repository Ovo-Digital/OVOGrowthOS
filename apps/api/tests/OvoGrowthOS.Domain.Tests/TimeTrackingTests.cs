using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class TimeTrackingTests
{
    private static readonly DateOnly Week = new(2026, 8, 10);

    [Fact]
    public void Hours_must_be_positive_whole_week_and_four_decimals()
    {
        Assert.NotNull(TimeTracking.HoursError(0));
        Assert.NotNull(TimeTracking.HoursError(-2));
        Assert.NotNull(TimeTracking.HoursError(169));
        Assert.NotNull(TimeTracking.HoursError(1.00001m));
        Assert.Null(TimeTracking.HoursError(0.25m));
        Assert.Null(TimeTracking.HoursError(TimeTracking.MaxWeekHours));
    }

    [Fact]
    public void Planned_and_actual_hours_are_reported_separately_and_voided_hours_are_excluded()
    {
        var plans = new List<TaskHourPlan>
        {
            new() { TaskId = Guid.NewGuid(), WeekStart = Week, Hours = 10 },
            new() { TaskId = Guid.NewGuid(), WeekStart = Week.AddDays(7), Hours = 4 }
        };
        var entries = new List<TaskTimeEntry>
        {
            new() { TaskId = Guid.NewGuid(), UserId = Guid.NewGuid(), WeekStart = Week, Hours = 6 },
            new() { TaskId = Guid.NewGuid(), UserId = Guid.NewGuid(), WeekStart = Week, Hours = 3, VoidedAt = DateTimeOffset.UtcNow }
        };
        var summary = TimeTracking.Summary(plans, entries);
        Assert.Equal(14m, summary.PlannedHours);
        Assert.Equal(6m, summary.ActualHours);
        Assert.Equal(3m, summary.VoidedHours);
        Assert.Equal(8m, summary.RemainingHours);
        Assert.True(summary.Complete);
    }

    [Fact]
    public void Missing_plan_or_missing_work_is_reported_as_incomplete_not_as_zero_reality()
    {
        var summary = TimeTracking.Summary([], []);
        Assert.Equal(0m, summary.PlannedHours);
        Assert.Equal(0m, summary.ActualHours);
        Assert.False(summary.Complete);

        var onlyPlan = TimeTracking.Summary([new TaskHourPlan { TaskId = Guid.NewGuid(), WeekStart = Week, Hours = 8 }], []);
        Assert.Equal(8m, onlyPlan.RemainingHours);
        Assert.False(onlyPlan.Complete);
    }
}
