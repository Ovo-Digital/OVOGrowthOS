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
}

// Explicit public allowlist: never serialize an EF entity or an internal management report.
public sealed record PortalReportSnapshot(string BrandName, string Currency, int Version, DateTimeOffset PublishedAt,
    BrandReportMetrics Metrics, IReadOnlyList<ReportExplanation> Explanations);
