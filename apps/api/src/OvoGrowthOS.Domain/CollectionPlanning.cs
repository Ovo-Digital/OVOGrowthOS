namespace OvoGrowthOS.Domain;

public enum ReceivableAge { Unknown, NotDue, Days1To30, Days31To60, Days61To90, Over90 }
public sealed record ReceivableRow(Guid Id, Guid BrandId, string BrandName, int Year, int Month, string InvoiceReference,
    DateOnly? DueOn, CollectionBalance Balance, ReceivableAge Age, PromiseBalance? Promise);
public sealed record ReceivableBucket(ReceivableAge Age, int Count, decimal Amount);
public sealed record CollectionWeek(DateOnly From, DateOnly Through, decimal Amount, int Count);
public sealed record CollectionPlan(DateOnly Today, string Currency, int Weeks, int ClosedPeriods,
    IReadOnlyList<ReceivableBucket> Aging, IReadOnlyList<CollectionWeek> Upcoming, IReadOnlyList<CollectionWeek> Received,
    decimal Outstanding, decimal Overdue, decimal UnknownDue, decimal AfterHorizon, decimal LegacyPaid,
    IReadOnlyList<ReceivableRow> Items, IReadOnlyList<ReceivableRow> ReviewItems,
    IReadOnlyList<CollectionWeek> Promised, decimal OverduePromises, decimal AfterHorizonPromises, decimal Unpromised);

public static class CollectionPlanning
{
    public static ReceivableAge Age(DateOnly? due, DateOnly today) => due is null ? ReceivableAge.Unknown : (today.DayNumber - due.Value.DayNumber) switch
    {
        <= 0 => ReceivableAge.NotDue, <= 30 => ReceivableAge.Days1To30, <= 60 => ReceivableAge.Days31To60,
        <= 90 => ReceivableAge.Days61To90, _ => ReceivableAge.Over90
    };

    // Read-only: all monetary values come from the existing settlement ledger, never a fresh fee calculation.
    public static CollectionPlan Build(IEnumerable<MonthlyPerformance> source, DateOnly today, string currency, int weeks)
    {
        if (weeks is not (4 or 8 or 12)) throw new ArgumentOutOfRangeException(nameof(weeks));
        var periods = source.Where(p => PortfolioReporting.IsClosed(p.Status) && (p.Collection?.Currency ?? p.Deal?.Currency) == currency).ToArray();
        var all = periods.Select(p => new ReceivableRow(p.Id, p.BrandId, p.Brand?.Name ?? "Marka bilgisi yok", p.Year, p.Month,
            p.Collection?.InvoiceReference ?? "", p.Collection?.DueOn, Collections.Balance(p, today), Age(p.Collection?.DueOn, today), CollectionPromises.Balance(p, today))).ToArray();
        var items = all.Where(x => !x.Balance.NeedsReview && x.Balance.Outstanding > 0)
            .OrderByDescending(x => x.Balance.OverdueDays).ThenBy(x => x.DueOn).ThenBy(x => x.BrandName).ThenBy(x => x.Id).ToArray();
        var validIds = all.Where(x => !x.Balance.NeedsReview).Select(x => x.Id).ToHashSet();
        var payments = periods.Where(p => validIds.Contains(p.Id)).SelectMany(p => p.Collection?.Payments ?? [])
            .Where(x => x.VoidedAt is null).ToArray();
        var horizon = today.AddDays(weeks * 7 - 1);
        var upcoming = Enumerable.Range(0, weeks).Select(i =>
        {
            var from = today.AddDays(i * 7); var through = from.AddDays(6);
            var rows = items.Where(x => x.DueOn >= from && x.DueOn <= through).ToArray();
            return new CollectionWeek(from, through, rows.Sum(x => x.Balance.Outstanding), rows.Length);
        }).ToArray();
        var received = Enumerable.Range(0, weeks).Select(i =>
        {
            var from = today.AddDays(-(weeks * 7 - 1) + i * 7); var through = from.AddDays(6);
            var rows = payments.Where(x => x.PaidOn >= from && x.PaidOn <= through).ToArray();
            return new CollectionWeek(from, through, rows.Sum(x => x.Amount), rows.Length);
        }).ToArray();
        var promises = items.Select(x => x.Promise).OfType<PromiseBalance>().Where(x => x.Remaining > 0).ToArray();
        var promised = Enumerable.Range(0, weeks).Select(i =>
        {
            var from = today.AddDays(i * 7); var through = from.AddDays(6);
            var rows = promises.Where(x => x.PromisedOn >= from && x.PromisedOn <= through).ToArray();
            return new CollectionWeek(from, through, rows.Sum(x => x.Remaining), rows.Length);
        }).ToArray();
        return new(today, currency, weeks, periods.Length,
            Enum.GetValues<ReceivableAge>().Select(age => new ReceivableBucket(age, items.Count(x => x.Age == age), items.Where(x => x.Age == age).Sum(x => x.Balance.Outstanding))).ToArray(),
            upcoming, received, items.Sum(x => x.Balance.Outstanding), items.Where(x => x.Balance.OverdueDays > 0).Sum(x => x.Balance.Outstanding),
            items.Where(x => x.DueOn is null).Sum(x => x.Balance.Outstanding), items.Where(x => x.DueOn > horizon).Sum(x => x.Balance.Outstanding),
            all.Where(x => !x.Balance.NeedsReview).Sum(x => x.Balance.LegacyPaid), items, all.Where(x => x.Balance.NeedsReview).ToArray(),
            promised, promises.Where(x => x.PromisedOn < today).Sum(x => x.Remaining), promises.Where(x => x.PromisedOn > horizon).Sum(x => x.Remaining),
            items.Sum(x => x.Balance.Outstanding) - promises.Sum(x => x.Remaining));
    }
}
