namespace OvoGrowthOS.Domain;

public enum NotificationKind { DailyTasks, TaskDue, PortalQuestion, PortalReply, PortalReport }

public sealed class NotificationPreference
{
    public Guid UserId { get; set; }
    public bool DailyTasksEmail { get; set; }
    public bool TaskDueEmail { get; set; }
    public bool PortalMessagesEmail { get; set; }
    public bool PortalReportsEmail { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Revision { get; set; } = 1;

    public bool WantsEmail(NotificationKind kind) => kind switch
    {
        NotificationKind.DailyTasks => DailyTasksEmail,
        NotificationKind.TaskDue => TaskDueEmail,
        NotificationKind.PortalReport => PortalReportsEmail,
        _ => PortalMessagesEmail
    };
}

public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public Guid? SourceId { get; set; }
    public string EventKey { get; set; } = "";
    public DateOnly? Day { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    // Null means email was not requested when this event was captured.
    public MailDeliveryStatus? EmailStatus { get; set; }
    public string Email { get; set; } = "";
    public int AccountVersion { get; set; }
    public DateTimeOffset? AttemptedAt { get; set; }
    public string ErrorCode { get; set; } = "";
    public int Revision { get; set; } = 1;
}
