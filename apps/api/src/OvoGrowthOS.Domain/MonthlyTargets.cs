namespace OvoGrowthOS.Domain;

public enum TargetMetric { NetRevenue, AdSpend, ContributionMargin }

public sealed class MonthlyTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal NetRevenueGoal { get; set; }
    public decimal AdBudget { get; set; }
    public decimal ContributionMarginGoal { get; set; }
    public Guid OwnerId { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TargetAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TargetId { get; set; }
    public TargetMetric Metric { get; set; }
    public Guid TaskId { get; set; }
    public int TargetRevision { get; set; }
    public Guid PerformanceId { get; set; }
    public DateTimeOffset PerformanceUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record TargetMetricResult(TargetMetric Metric, decimal Target, decimal? Actual, decimal? Difference,
    decimal? RelativeDifference, decimal? PercentagePointDifference, bool? NeedsAttention);
public sealed record TargetComparison(Guid? PerformanceId, MonthlyPerformanceStatus? Status, DateTimeOffset? PerformanceUpdatedAt,
    bool IsClosed, string? MissingReason, IReadOnlyList<TargetMetricResult> Metrics);

public static class MonthlyTargetEngine
{
    public static TargetComparison Compare(MonthlyTarget target, MonthlyPerformance? period)
    {
        var matches = period is not null && period.BrandId == target.BrandId && period.Year == target.Year && period.Month == target.Month
            && string.Equals(period.Deal?.Currency, target.Currency, StringComparison.OrdinalIgnoreCase);
        var actual = matches ? period : null;
        return new(actual?.Id, actual?.Status, actual?.UpdatedAt,
            actual?.Status is MonthlyPerformanceStatus.Locked or MonthlyPerformanceStatus.Invoiced or MonthlyPerformanceStatus.Paid,
            period is null ? "Bu ay için gerçekleşen sonuç kaydı yok; sıfır kabul edilmedi." : !matches ? "Dönem veya para birimi eşleşmiyor; karşılaştırma yapılmadı." : null,
            [Amount(TargetMetric.NetRevenue, target.NetRevenueGoal, actual?.NetRevenue, false),
             Amount(TargetMetric.AdSpend, target.AdBudget, actual?.TotalAdSpend, true),
             Margin(target.ContributionMarginGoal, actual is { NetRevenue: > 0 } ? actual.BrandContributionProfit / actual.NetRevenue : null)]);
    }
    private static TargetMetricResult Amount(TargetMetric metric, decimal target, decimal? actual, bool upperLimit)
    {
        var difference = actual - target;
        return new(metric, target, actual, difference, target > 0 && actual >= 0 ? difference / target : null, null,
            actual.HasValue ? upperLimit ? actual > target : actual < target : null);
    }
    private static TargetMetricResult Margin(decimal target, decimal? actual) =>
        new(TargetMetric.ContributionMargin, target, actual, actual - target, null, (actual - target) * 100m, actual.HasValue ? actual < target : null);
}
