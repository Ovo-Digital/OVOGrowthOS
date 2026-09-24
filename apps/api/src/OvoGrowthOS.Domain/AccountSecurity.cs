namespace OvoGrowthOS.Domain;

public sealed class AccountSecurity
{
    public Guid UserId { get; set; }
    public bool Enabled { get; set; }
    public string ProtectedSecret { get; set; } = "";
    public DateTimeOffset? SetupExpiresAt { get; set; }
    public string[] RecoveryHashes { get; set; } = [];
    public long LastTimeStep { get; set; } = -1;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string ChallengeHash { get; set; } = "";
    public DateTimeOffset? ChallengeExpiresAt { get; set; }
    public int ChallengeAccountVersion { get; set; }
    public int Revision { get; set; } = 1;
}
