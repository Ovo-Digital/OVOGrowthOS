namespace OvoGrowthOS.Domain;

public enum LeadStage { New, Contacted, WaitingForInformation, MeetingPlanned, ProposalFollowUp, OnHold }
public enum WorkPriority { Low, Normal, High }
public enum WorkKind { General, MonthlyClose, ContractRenewal }

public sealed class BrandFollowUp
{
    public Guid BrandId { get; set; }
    public Guid? OwnerId { get; set; }
    public LeadStage Stage { get; set; }
    public string WaitingReason { get; set; } = "";
    public DateOnly? NextContactOn { get; set; }
    public string NextStep { get; set; } = "";
    public int Revision { get; set; }
}

public sealed class BrandContactNote
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public DateOnly ContactOn { get; set; }
    public string Text { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkTask
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public Guid AssigneeId { get; set; }
    public UserAccount? Assignee { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public WorkPriority Priority { get; set; } = WorkPriority.Normal;
    public WorkKind Kind { get; set; }
    public Guid? DealId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public DateOnly DueOn { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string CompletedBy { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Revision { get; set; }
}

public static class TeamWork
{
    // Deadlines are calendar days for the Turkish team, not UTC midnight.
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
    public static bool IsOverdue(WorkTask task, DateOnly today) => task.CompletedAt is null && task.DueOn < today;
}
