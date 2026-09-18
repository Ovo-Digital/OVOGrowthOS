namespace OvoGrowthOS.Domain;

// One external account belongs to one brand. Assignment is not editable in the first release.
public sealed class PortalAccess
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    public Guid BrandId { get; set; }
}

public sealed class PortalReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid PerformanceId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class PortalDocumentShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid DocumentId { get; set; }
    public DocumentAttachment Document { get; set; } = null!;
    public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class PortalQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid ReportId { get; set; }
    public Guid UserId { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AnsweredAt { get; set; }
    // Null preserves the meaning of legacy one-answer conversations without rewriting their history.
    public PortalConversationStatus? Status { get; set; }
    public Guid? OwnerId { get; set; }
    public int Revision { get; set; } = 1;
    public List<PortalMessage> Messages { get; set; } = [];
}

public enum PortalConversationStatus { Open, AwaitingCustomer, Resolved }
public enum PortalRequestStatus { Requested, Received, Cancelled }

public sealed class PortalMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public Guid AuthorId { get; set; }
    public bool FromStaff { get; set; }
    public string Text { get; set; } = "";
    public int Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PortalReportReading
{
    public Guid ReportId { get; set; }
    public Guid UserId { get; set; }
    public Guid BrandId { get; set; }
    public DateTimeOffset FirstViewedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastViewedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class PortalDataRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateOnly? DueOn { get; set; }
    public PortalRequestStatus Status { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// Explicit public allowlist: never serialize an EF entity or an internal management report.
public sealed record PortalReportSnapshot(string BrandName, string Currency, int Version, DateTimeOffset PublishedAt,
    BrandReportMetrics Metrics, IReadOnlyList<ReportExplanation> Explanations);
