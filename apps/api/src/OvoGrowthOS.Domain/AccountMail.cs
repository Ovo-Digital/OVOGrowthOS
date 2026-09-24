namespace OvoGrowthOS.Domain;

public enum AccountLinkPurpose { Invitation, PasswordReset }
public enum MailDeliveryStatus { Pending, Sending, Sent, Uncertain, Cancelled }

public sealed class AccountLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public int AccountVersion { get; set; }
    public AccountLinkPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class MailDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AccountLinkId { get; set; }
    public string ProtectedBody { get; set; } = "";
    public MailDeliveryStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AttemptedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string ErrorCode { get; set; } = "";
    public int Revision { get; set; } = 1;
}
