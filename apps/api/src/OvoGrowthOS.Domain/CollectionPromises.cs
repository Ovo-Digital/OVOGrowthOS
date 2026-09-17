namespace OvoGrowthOS.Domain;

public sealed class CollectionPromise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MonthlyPerformanceId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PromisedOn { get; set; }
    public Guid ContactNoteId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TaskId { get; set; }
    public bool IsCancelled { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    // An explicit baseline survives timestamp rounding and payments entered later with an earlier paid date.
    public Guid[] PaymentIdsAtRecording { get; set; } = [];
}

public sealed record PromiseBalance(decimal Remaining, string State, DateOnly PromisedOn);

public static class CollectionPromises
{
    public static PromiseBalance? Balance(MonthlyPerformance period, DateOnly today)
    {
        var promise = period.Collection?.Promise;
        if (promise is null) return null;
        var balance = Collections.Balance(period, today);
        if (promise.IsCancelled) return new(0, "Cancelled", promise.PromisedOn);
        if (balance.NeedsReview || !PortfolioReporting.IsClosed(period.Status)) return new(0, "NeedsReview", promise.PromisedOn);
        var subsequentPaid = period.Collection!.Payments.Where(x => x.VoidedAt is null && !promise.PaymentIdsAtRecording.Contains(x.Id)).Sum(x => x.Amount);
        var remaining = Math.Min(balance.Outstanding, Math.Max(0, promise.Amount - subsequentPaid));
        return new(remaining, remaining == 0 ? "Covered" : promise.PromisedOn < today ? "Overdue" : "Waiting", promise.PromisedOn);
    }

    public static bool CanRecord(MonthlyPerformance period, DateOnly today) => period.Collection is not null
        && period.Status is MonthlyPerformanceStatus.Invoiced or MonthlyPerformanceStatus.Paid
        && Collections.Balance(period, today) is { NeedsReview: false, Outstanding: > 0 };
}
