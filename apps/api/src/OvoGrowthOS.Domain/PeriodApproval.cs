namespace OvoGrowthOS.Domain;

public sealed class PeriodApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool Approved { get; set; }
    public string Reason { get; set; } = "";
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PeriodApprovals
{
    public const int MaxReasonLength = 1000;

    public static string? DecisionError(bool approved, string? reason, MonthlyPerformanceStatus status)
    {
        if (status is MonthlyPerformanceStatus.Draft or MonthlyPerformanceStatus.UnderReview)
            return "Dönem henüz kesinleşmedi; onaya kapalıdır. Ekip onayından sonra tekrar deneyin.";
        var text = reason?.Trim() ?? "";
        if (text.Length > MaxReasonLength) return $"Gerekçe en fazla {MaxReasonLength} karakter olabilir.";
        if (!approved && text.Length == 0) return "Red işlemi için gerekçe yazın.";
        return null;
    }
}
