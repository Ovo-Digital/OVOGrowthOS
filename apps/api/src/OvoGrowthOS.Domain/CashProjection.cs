namespace OvoGrowthOS.Domain;

public sealed record CashWeek(DateOnly From, DateOnly Through, decimal Due, int DueCount, decimal Promised, int PromiseCount);
public sealed record CashProjectionReport(DateOnly Today, DateOnly Horizon, string Currency, int Weeks, int ClosedPeriods,
    IReadOnlyList<CashWeek> Rows, decimal DueTotal, decimal PromisedTotal, decimal Overdue, decimal UnknownDue,
    decimal BeyondHorizon, decimal OverduePromises, decimal BeyondHorizonPromises, IReadOnlyList<string> Notes);

// Read-only 13-week cash inflow view. It only re-buckets money that is already recorded in
// the settlement ledger and payment promises; it never creates a forecast or a due date.
public static class CashProjection
{
    public const int HorizonWeeks = 13;

    public static CashProjectionReport Build(IEnumerable<MonthlyPerformance> source, DateOnly today, string currency)
    {
        var periods = source.Where(p => PortfolioReporting.IsClosed(p.Status) && (p.Collection?.Currency ?? p.Deal?.Currency) == currency).ToArray();
        var receivables = periods
            .Select(p => (Balance: Collections.Balance(p, today), DueOn: p.Collection?.DueOn))
            .Where(x => !x.Balance.NeedsReview && x.Balance.Outstanding > 0).ToArray();
        var promises = periods
            .Select(p => (Promise: CollectionPromises.Balance(p, today), Balance: Collections.Balance(p, today)))
            .Where(x => !x.Balance.NeedsReview && x.Promise is { Remaining: > 0 })
            .Select(x => x.Promise!).ToArray();

        var horizon = today.AddDays(HorizonWeeks * 7 - 1);
        var rows = Enumerable.Range(0, HorizonWeeks).Select(i =>
        {
            var from = today.AddDays(i * 7); var through = from.AddDays(6);
            var dueRows = receivables.Where(x => x.DueOn >= from && x.DueOn <= through).ToArray();
            var promiseRows = promises.Where(x => x.PromisedOn >= from && x.PromisedOn <= through).ToArray();
            return new CashWeek(from, through, dueRows.Sum(x => x.Balance.Outstanding), dueRows.Length,
                promiseRows.Sum(x => x.Remaining), promiseRows.Length);
        }).ToArray();

        return new CashProjectionReport(today, horizon, currency, HorizonWeeks, periods.Length, rows,
            rows.Sum(x => x.Due), rows.Sum(x => x.Promised),
            receivables.Where(x => x.Balance.OverdueDays > 0).Sum(x => x.Balance.Outstanding),
            receivables.Where(x => x.DueOn is null).Sum(x => x.Balance.Outstanding),
            receivables.Where(x => x.DueOn > horizon).Sum(x => x.Balance.Outstanding),
            promises.Where(x => x.PromisedOn < today).Sum(x => x.Remaining),
            promises.Where(x => x.PromisedOn > horizon).Sum(x => x.Remaining),
            [
                "Tutarlar yalnız kayıtlı vadelere ve ödeme sözlerine dayanır; tahsil garantisi değildir.",
                "Vade sütunu ile ödeme sözü sütunu aynı alacağın iki ayrı görünümüdür, toplanmaz.",
                "Vadesi bilinmeyen ve gecikmiş alacaklar haftalara bölünmez; ayrı toplamlarda gösterilir.",
                "Yalnız kilitlenmiş, faturalanmış veya ödenmiş dönemler ile seçili para birimi hesaba girer."
            ]);
    }
}
