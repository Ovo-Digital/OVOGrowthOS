namespace OvoGrowthOS.Domain;

// Answers "which brand really earns OVO money" from closed periods: recorded
// OVO fees minus confirmed actual service costs. Unconfirmed cost drafts never
// reduce the contribution; they are counted separately so missing confirmations
// stay visible instead of silently flattering the ranking.
public sealed record BrandProfitRow(Guid BrandId, string BrandName, string Currency, int Periods,
    decimal TotalFee, decimal TotalRecordedCost, int ConfirmedPeriods, int UnconfirmedPeriods,
    decimal Contribution, decimal? Margin);

public static class BrandProfitability
{
    public static IReadOnlyList<BrandProfitRow> Rank(IEnumerable<(MonthlyPerformance Period, ServiceCostAccount? Account, string BrandName)> rows, string currency)
    {
        return rows
            .Where(x => PortfolioReporting.IsClosed(x.Period.Status))
            .Where(x => string.Equals(x.Period.Deal?.Currency ?? "", currency, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Period.BrandId)
            .Select(g =>
            {
                var fee = g.Sum(x => x.Period.OvoFee);
                var confirmed = g.Where(x => x.Account?.ConfirmedAt is not null).ToList();
                var cost = confirmed.SelectMany(x => x.Account!.Entries.Where(e => e.VoidedAt is null)).Sum(e => e.Amount);
                var contribution = fee - cost;
                return new BrandProfitRow(g.Key, g.Select(x => x.BrandName).FirstOrDefault() ?? "",
                    currency, g.Count(), fee, cost, confirmed.Count,
                    g.Count(x => x.Account is not null && x.Account.ConfirmedAt is null),
                    contribution, fee > 0 ? contribution / fee : null);
            })
            .OrderByDescending(x => x.Contribution).ToList();
    }
}
