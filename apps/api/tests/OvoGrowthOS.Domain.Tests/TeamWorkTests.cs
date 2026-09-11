using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class TeamWorkTests
{
    [Fact]
    public void Due_today_is_not_overdue_and_completion_removes_overdue_status()
    {
        var today = new DateOnly(2026, 9, 8);
        var task = new WorkTask { DueOn = today };
        Assert.False(TeamWork.IsOverdue(task, today));
        task.DueOn = today.AddDays(-1);
        Assert.True(TeamWork.IsOverdue(task, today));
        task.CompletedAt = DateTimeOffset.UtcNow;
        Assert.False(TeamWork.IsOverdue(task, today));
    }

    [Fact]
    public void Calendar_boundary_uses_Istanbul_not_UTC()
    {
        Assert.Equal(new DateOnly(2026, 9, 9), TeamWork.Today(new DateTimeOffset(2026, 9, 8, 21, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 9, 8), TeamWork.Today(new DateTimeOffset(2026, 9, 8, 20, 59, 59, TimeSpan.Zero)));
    }
}
