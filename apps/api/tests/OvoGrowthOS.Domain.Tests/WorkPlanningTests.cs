using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class WorkPlanningTests
{
    [Fact]
    public void Missing_capacity_is_unknown_not_zero()
    {
        var result = WorkPlanning.Compare(null, 20, 5);
        Assert.Null(result.AvailableHours); Assert.Null(result.RemainingHours); Assert.Null(result.LoadRatio); Assert.Null(result.Overloaded);
        Assert.Equal(20, result.PlannedHours); Assert.Equal(5, result.CompletedTaskPlannedHours);
    }
    [Theory]
    [InlineData(31, 1, false)] [InlineData(32, 0, false)] [InlineData(33, -1, true)]
    public void Unavailable_hours_reduce_capacity_and_threshold_is_exact(int planned, int remaining, bool overloaded)
    {
        var result = WorkPlanning.Compare(new WeeklyCapacity { WorkingHours = 40, UnavailableHours = 8 }, planned, 4);
        Assert.Equal(32, result.AvailableHours); Assert.Equal(remaining, result.RemainingHours); Assert.Equal(overloaded, result.Overloaded);
        Assert.Equal(planned / 32m, result.LoadRatio);
    }
    [Fact]
    public void Explicit_zero_capacity_never_divides_and_preview_does_not_change_input()
    {
        var capacity = new WeeklyCapacity { WorkingHours = 8, UnavailableHours = 8 };
        var result = WorkPlanning.Compare(capacity, 0, 0, 3.25m);
        Assert.Equal(0, result.AvailableHours); Assert.Null(result.LoadRatio); Assert.True(result.Overloaded); Assert.Equal(-3.25m, result.RemainingHours);
        Assert.Equal(8, capacity.WorkingHours); Assert.Equal(8, capacity.UnavailableHours);
    }
    [Fact]
    public void Weeks_use_monday_and_templates_have_stable_unique_steps()
    {
        Assert.Equal(new DateOnly(2026, 9, 14), WorkPlanning.WeekStart(new DateOnly(2026, 9, 20)));
        Assert.True(WorkPlanning.ValidWeek(new DateOnly(2026, 9, 14))); Assert.False(WorkPlanning.ValidWeek(new DateOnly(2026, 9, 15)));
        foreach (var kind in Enum.GetValues<WorkTemplateKind>()) { var steps = WorkPlanning.Steps(kind); Assert.Equal(3, steps.Count); Assert.Equal(3, steps.Select(x => x.Key).Distinct().Count()); }
        Assert.Single(WorkPlanning.Steps(WorkTemplateKind.MonthlyClose), x => x.Kind == WorkKind.MonthlyClose);
        Assert.All(WorkPlanning.Steps(WorkTemplateKind.BrandStart), x => Assert.Equal(WorkKind.General, x.Kind));
    }
}
