namespace OvoGrowthOS.Domain;

// Answers "does this brand keep its payment promises" from recorded promises and
// payments. A promise counts as kept only when nothing remains expected from it,
// and as broken only while an amount is still overdue. Waiting promises and
// cancelled or review-state records never move the score; the score therefore
// describes recorded promise discipline, not a forecast or a guarantee.
public sealed record PromiseReliabilityRow(Guid BrandId, string BrandName, int Year, int Month,
    DateOnly PromisedOn, decimal PromisedAmount, decimal Remaining, string Outcome, int OverdueDays);

public sealed record BrandReliability(Guid BrandId, string BrandName, string Currency,
    int Kept, int Broken, int Waiting, decimal? Score, int? AvgOverdueDays,
    IReadOnlyList<PromiseReliabilityRow> Rows);

public static class PromiseReliability
{
    public static IReadOnlyList<BrandReliability> Rank(IEnumerable<(MonthlyPerformance Period, string BrandName)> rows, DateOnly today, string currency)
    {
        return rows
            .Select(x => (x.Period, x.BrandName, Balance: CollectionPromises.Balance(x.Period, today)))
            .Where(x => x.Balance is not null
                && string.Equals(x.Period.Collection?.Currency ?? x.Period.Deal?.Currency ?? "", currency, StringComparison.OrdinalIgnoreCase)
                && x.Balance.State is "Covered" or "Overdue" or "Waiting"
                && (x.Balance.State != "Overdue" || x.Balance.Remaining > 0))
            .GroupBy(x => x.Period.BrandId)
            .Select(g =>
            {
                var items = g.Select(x => new PromiseReliabilityRow(x.Period.BrandId,
                    x.BrandName, x.Period.Year, x.Period.Month, x.Balance!.PromisedOn,
                    x.Period.Collection!.Promise!.Amount, x.Balance.Remaining,
                    x.Balance.State == "Covered" ? "Kept" : x.Balance.State == "Overdue" ? "Broken" : "Waiting",
                    x.Balance.State == "Overdue" ? Math.Max(0, today.DayNumber - x.Balance.PromisedOn.DayNumber) : 0)).ToList();
                var kept = items.Count(x => x.Outcome == "Kept");
                var broken = items.Count(x => x.Outcome == "Broken");
                var overdue = items.Where(x => x.Outcome == "Broken").ToList();
                return new BrandReliability(g.Key, g.Select(x => x.BrandName).FirstOrDefault() ?? "", currency,
                    kept, broken, items.Count - kept - broken,
                    kept + broken == 0 ? null : (decimal)kept / (kept + broken),
                    overdue.Count == 0 ? null : (int)Math.Round(overdue.Average(x => x.OverdueDays)), items);
            })
            .OrderBy(x => x.Score ?? decimal.MaxValue).ThenByDescending(x => x.Broken).ToList();
    }
}
