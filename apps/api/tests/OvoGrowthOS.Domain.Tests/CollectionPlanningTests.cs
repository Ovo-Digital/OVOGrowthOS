using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class CollectionPlanningTests
{
    private static readonly DateOnly Today = new(2026, 9, 17);
    private static MonthlyPerformance Period(DateOnly? due, decimal fee = 100, string currency = "TRY") => new()
    {
        Id = Guid.NewGuid(), Brand = new Brand { Name = "Deneme" }, Deal = new Deal { Name = "Deneme anlaşması", Currency = currency }, Status = MonthlyPerformanceStatus.Invoiced,
        OvoFee = fee, NetRevenue = 1000, CommissionBreakdownJson = "korunan hesap", Collection = new() { ReceivableAmount = fee, Currency = currency, DueOn = due }
    };
    [Theory]
    [InlineData(-1, ReceivableAge.NotDue)] [InlineData(0, ReceivableAge.NotDue)]
    [InlineData(1, ReceivableAge.Days1To30)] [InlineData(30, ReceivableAge.Days1To30)]
    [InlineData(31, ReceivableAge.Days31To60)] [InlineData(60, ReceivableAge.Days31To60)]
    [InlineData(61, ReceivableAge.Days61To90)] [InlineData(90, ReceivableAge.Days61To90)] [InlineData(91, ReceivableAge.Over90)]
    public void Aging_boundaries_are_exact(int days, ReceivableAge expected) => Assert.Equal(expected, CollectionPlanning.Age(Today.AddDays(-days), Today));

    [Fact]
    public void All_outstanding_is_partitioned_once_without_inventing_due_dates()
    {
        var data = new[] { Period(null, 10), Period(Today.AddDays(-1), 20), Period(Today, 30), Period(Today.AddDays(6), 40),
            Period(Today.AddDays(7), 50), Period(Today.AddDays(27), 60), Period(Today.AddDays(28), 70) };
        var plan = CollectionPlanning.Build(data, Today, "TRY", 4);
        Assert.Equal(280, plan.Outstanding); Assert.Equal(10, plan.UnknownDue); Assert.Equal(20, plan.Overdue); Assert.Equal(70, plan.AfterHorizon);
        Assert.Equal(new decimal[] { 70, 50, 0, 60 }, plan.Upcoming.Select(x => x.Amount));
        Assert.Equal(plan.Outstanding, plan.Overdue + plan.UnknownDue + plan.AfterHorizon + plan.Upcoming.Sum(x => x.Amount));
        Assert.Equal(plan.Outstanding, plan.Aging.Sum(x => x.Amount)); Assert.Equal(7, plan.Aging.Sum(x => x.Count));
        Assert.Equal(ReceivableAge.Unknown, plan.Items.Single(x => x.DueOn is null).Age);
        Assert.All(data, p => Assert.Equal("korunan hesap", p.CommissionBreakdownJson));
    }
    [Fact]
    public void Real_payments_reduce_forecast_voids_restore_it_and_legacy_dates_remain_unknown()
    {
        var p = Period(Today, 100.1234m); var payment = new CollectionPayment { Amount = 40.0001m, PaidOn = Today };
        p.Collection!.Payments.Add(payment);
        var legacy = Period(null, 80); legacy.Collection = null; legacy.Status = MonthlyPerformanceStatus.Paid;
        var first = CollectionPlanning.Build([p, legacy], Today, "TRY", 4);
        Assert.Equal(60.1233m, first.Outstanding); Assert.Equal(60.1233m, first.Upcoming.Sum(x => x.Amount));
        Assert.Equal(40.0001m, first.Received.Sum(x => x.Amount)); Assert.Equal(80, first.LegacyPaid);
        payment.VoidedAt = DateTimeOffset.UtcNow;
        var second = CollectionPlanning.Build([p, legacy], Today, "TRY", 4);
        Assert.Equal(100.1234m, second.Outstanding); Assert.Equal(0, second.Received.Sum(x => x.Amount));
        Assert.Equal(100.1234m, p.OvoFee); Assert.Equal(MonthlyPerformanceStatus.Invoiced, p.Status);
    }
    [Theory]
    [InlineData(4)] [InlineData(8)] [InlineData(12)]
    public void Week_windows_are_inclusive_without_overlap_and_exclude_future_receipts(int weeks)
    {
        var p = Period(Today); var start = Today.AddDays(-(weeks * 7 - 1));
        foreach (var day in new[] { start.AddDays(-1), start, start.AddDays(6), start.AddDays(7), Today, Today.AddDays(1) })
            p.Collection!.Payments.Add(new() { Amount = 1, PaidOn = day });
        var plan = CollectionPlanning.Build([p], Today, "TRY", weeks);
        Assert.Equal(4, plan.Received.Sum(x => x.Amount)); Assert.Equal(2, plan.Received[0].Count); Assert.Equal(1, plan.Received[1].Count);
        Assert.Equal(start, plan.Received[0].From); Assert.Equal(Today, plan.Received[^1].Through);
        Assert.Equal(Today, plan.Upcoming[0].From); Assert.Equal(Today.AddDays(weeks * 7 - 1), plan.Upcoming[^1].Through);
    }
    [Fact]
    public void Currencies_drafts_and_review_required_records_are_not_summed_together()
    {
        var good = Period(Today); var usd = Period(Today, 1000, "USD"); var draft = Period(Today, 2000); draft.Status = MonthlyPerformanceStatus.Draft;
        var invalid = Period(Today, -1); var overpaid = Period(Today, 10); overpaid.Collection!.Payments.Add(new() { Amount = 11, PaidOn = Today });
        var zero = Period(null, 0);
        var plan = CollectionPlanning.Build([good, usd, draft, invalid, overpaid, zero], Today, "TRY", 4);
        Assert.Equal(4, plan.ClosedPeriods); Assert.Single(plan.Items); Assert.Equal(2, plan.ReviewItems.Count);
        Assert.Equal(100, plan.Outstanding); Assert.Equal(0, plan.Received.Sum(x => x.Amount));
        Assert.Equal(1000, CollectionPlanning.Build([good, usd], Today, "USD", 4).Outstanding);
        Assert.Equal(0, CollectionPlanning.Build([good], Today, "EUR", 4).ClosedPeriods);
        Assert.Throws<ArgumentOutOfRangeException>(() => CollectionPlanning.Build([good], Today, "TRY", 5));
    }
}
