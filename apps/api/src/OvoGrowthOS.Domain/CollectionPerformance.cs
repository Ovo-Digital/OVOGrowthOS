namespace OvoGrowthOS.Domain;

public sealed record CollectionPerformanceStats(
    decimal? OnTimeRate,
    decimal? AverageDays,
    int RecordCount,
    int PaymentCount,
    int OnTimePayments,
    decimal OnTimeAmount,
    decimal TotalAmount);

public static class CollectionPerformance
{
    // Settled collections with a known due date. Voided, non-positive and legacy undated
    // amounts are excluded; rates stay in 0-1 and are rounded to four decimals, days to one.
    public static CollectionPerformanceStats Calculate(IEnumerable<MonthlyPerformance> periods, DateOnly today, string? currency = null)
    {
        var dueDates = new List<(DateOnly DueOn, CollectionPayment Payment)>();
        var recordCount = 0;
        foreach (var period in periods)
        {
            var account = period.Collection;
            if (account is null || account.DueOn is not { } dueOn) continue;
            if (currency is not null && !string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase)) continue;
            if (Collections.Balance(period, today).State != "Settled") continue;
            var payments = account.Payments.Where(x => x.VoidedAt is null && x.Amount > 0).ToList();
            if (payments.Count == 0) continue;
            recordCount++;
            foreach (var payment in payments) dueDates.Add((dueOn, payment));
        }
        var total = dueDates.Sum(x => x.Payment.Amount);
        var onTime = dueDates.Where(x => x.Payment.PaidOn <= x.DueOn);
        var onTimeAmount = onTime.Sum(x => x.Payment.Amount);
        var rate = total == 0 ? null : (decimal?)decimal.Round(onTimeAmount / total, 4);
        var average = dueDates.Count == 0
            ? null
            : (decimal?)decimal.Round(dueDates.Sum(x => (decimal)(x.Payment.PaidOn.DayNumber - x.DueOn.DayNumber)) / dueDates.Count, 1);
        return new(rate, average, recordCount, dueDates.Count, onTime.Count(), onTimeAmount, total);
    }
}
