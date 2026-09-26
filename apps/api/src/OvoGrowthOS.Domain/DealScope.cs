namespace OvoGrowthOS.Domain;

public enum ScopeRequestStatus { Pending, Approved, Rejected }

public sealed class DealScopeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DealId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RemovedAt { get; set; }
    public string RemovedBy { get; set; } = "";
    public string RemoveReason { get; set; } = "";
}

public sealed class DealScopeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DealId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ScopeRequestStatus Status { get; set; } = ScopeRequestStatus.Pending;
    public string RequestedBy { get; set; } = "";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string DecidedBy { get; set; } = "";
    public DateTimeOffset? DecidedAt { get; set; }
    public string DecisionNote { get; set; } = "";
    public Guid? ScopeItemId { get; set; }
}

public static class DealScope
{
    public static bool Editable(DealStatus status) => status is not (DealStatus.Rejected or DealStatus.Terminated);

    public static string EditError(DealStatus status) => status switch
    {
        DealStatus.Rejected => "Reddedilmiş anlaşmanın kapsamı değiştirilemez.",
        DealStatus.Terminated => "Sonlandırılmış anlaşmanın kapsamı değiştirilemez.",
        _ => "Bu anlaşma için kapsam değiştirilemez."
    };

    public static string? DecisionError(ScopeRequestStatus status) => status == ScopeRequestStatus.Pending
        ? null
        : "Bu paket dışı talep daha önce sonuçlandırılmış. Yeni bir talep açın.";

    public static DealScopeItem? Approve(DealScopeRequest request, Guid dealId, string actor, DateTimeOffset now) =>
        request.Status != ScopeRequestStatus.Pending || request.ScopeItemId.HasValue
            ? null
            : new DealScopeItem
            {
                DealId = dealId, Title = request.Title, Description = request.Description,
                CreatedBy = actor, CreatedAt = now
            };

    public static string StatusLabel(ScopeRequestStatus status) => status switch
    {
        ScopeRequestStatus.Pending => "Bekliyor",
        ScopeRequestStatus.Approved => "Onaylandı",
        ScopeRequestStatus.Rejected => "Reddedildi",
        _ => "Bilinmiyor"
    };
}
