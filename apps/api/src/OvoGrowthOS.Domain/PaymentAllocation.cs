namespace OvoGrowthOS.Domain;

// Suggests how a single received bank amount could be split across a brand's
// open periods, oldest due date first. Suggestion only: nothing is recorded,
// no period is changed, and leftover is reported instead of being invented.
public sealed record AllocationRow(Guid PerformanceId, int Year, int Month, DateOnly? DueOn,
    decimal Outstanding, decimal Suggested, decimal RemainingAfter);

public sealed record AllocationPlan(string Currency, decimal Received, decimal TotalOutstanding,
    IReadOnlyList<AllocationRow> Rows, decimal Allocated, decimal Leftover,
    int SkippedReview, IReadOnlyList<string> OtherCurrencies);

public static class PaymentAllocation
{
    public static AllocationPlan Build(IEnumerable<MonthlyPerformance> periods, DateOnly today, string currency, decimal received)
    {
        var open = periods
            .Where(x => PortfolioReporting.IsClosed(x.Status))
            .Select(x => (Period: x, Balance: Collections.Balance(x, today)))
            .Where(x => !x.Balance.NeedsReview && x.Balance.Outstanding > 0)
            .ToList();
        var skipped = periods.Count(x => PortfolioReporting.IsClosed(x.Status) && Collections.Balance(x, today).NeedsReview);
        var matching = open
            .Where(x => string.Equals(x.Period.Collection?.Currency ?? x.Period.Deal?.Currency ?? "", currency, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Period.Collection?.DueOn ?? DateOnly.MaxValue)
            .ThenBy(x => x.Period.Year).ThenBy(x => x.Period.Month)
            .ToList();
        var rest = received;
        var rows = new List<AllocationRow>();
        foreach (var (period, balance) in matching)
        {
            var suggested = Math.Min(balance.Outstanding, rest);
            rest -= suggested;
            rows.Add(new(period.Id, period.Year, period.Month, period.Collection?.DueOn,
                balance.Outstanding, suggested, balance.Outstanding - suggested));
        }
        var others = open
            .Select(x => x.Period.Collection?.Currency ?? x.Period.Deal?.Currency ?? "")
            .Where(x => x.Length == 3 && !string.Equals(x, currency, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        return new(currency, received, matching.Sum(x => x.Balance.Outstanding), rows,
            received - rest, rest, skipped, others);
    }
}
