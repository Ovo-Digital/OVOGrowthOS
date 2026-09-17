using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class CollectionPromiseTests
{
    private static readonly DateOnly Today = new(2026, 9, 18);
    private static MonthlyPerformance Period() => new() { Status = MonthlyPerformanceStatus.Invoiced, OvoFee = 100.1234m,
        Collection = new() { ReceivableAmount = 100.1234m, Currency = "TRY", DueOn = Today,
            Promise = new() { Amount = 60.1234m, PromisedOn = Today, Revision = 1 } } };

    [Fact]
    public void New_payments_reduce_promise_with_four_decimals_and_void_restores_it()
    {
        var p = Period(); var payment = new CollectionPayment { Id = Guid.NewGuid(), Amount = 20.0001m, PaidOn = Today.AddDays(-10) };
        Assert.Equal(60.1234m, CollectionPromises.Balance(p, Today)!.Remaining);
        p.Collection!.Payments.Add(payment);
        Assert.Equal(40.1233m, CollectionPromises.Balance(p, Today)!.Remaining);
        payment.VoidedAt = DateTimeOffset.UtcNow;
        Assert.Equal(60.1234m, CollectionPromises.Balance(p, Today)!.Remaining);
        Assert.Equal(100.1234m, p.OvoFee); Assert.Equal(MonthlyPerformanceStatus.Invoiced, p.Status);
    }

    [Fact]
    public void Baseline_payments_do_not_reduce_new_promise_and_their_void_does_not_inflate_it()
    {
        var p = Period(); var old = new CollectionPayment { Id = Guid.NewGuid(), Amount = 40, PaidOn = Today };
        p.Collection!.Payments.Add(old); p.Collection.Promise!.PaymentIdsAtRecording = [old.Id];
        Assert.Equal(60.1234m, CollectionPromises.Balance(p, Today)!.Remaining);
        old.VoidedAt = DateTimeOffset.UtcNow;
        Assert.Equal(60.1234m, CollectionPromises.Balance(p, Today)!.Remaining);
        p.Collection.Payments.Add(new() { Id = Guid.NewGuid(), Amount = 70, PaidOn = Today });
        Assert.Equal(new PromiseBalance(0, "Covered", Today), CollectionPromises.Balance(p, Today));
    }

    [Fact]
    public void Missing_cancelled_review_and_zero_are_distinct_and_never_forecast_excess()
    {
        var p = Period(); p.Collection!.Promise!.PromisedOn = Today.AddDays(-1);
        Assert.Equal("Overdue", CollectionPromises.Balance(p, Today)!.State);
        p.Collection.Promise.PromisedOn = Today; Assert.Equal("Waiting", CollectionPromises.Balance(p, Today)!.State);
        p.Collection.Promise.Amount = 999; Assert.Equal(100.1234m, CollectionPromises.Balance(p, Today)!.Remaining);
        p.Collection.Promise.IsCancelled = true; Assert.Equal("Cancelled", CollectionPromises.Balance(p, Today)!.State);
        Assert.Equal(0, CollectionPromises.Balance(p, Today)!.Remaining);
        p.Collection.Promise.IsCancelled = false; p.Collection.Payments.Add(new() { Id = Guid.NewGuid(), Amount = 200 });
        Assert.Equal("NeedsReview", CollectionPromises.Balance(p, Today)!.State);
        Assert.False(CollectionPromises.CanRecord(p, Today));
        p.Collection.Promise = null; Assert.Null(CollectionPromises.Balance(p, Today));
    }

    [Fact]
    public void Promise_calendar_partitions_outstanding_separately_from_due_calendar()
    {
        var a = Period(); var b = Period(); var c = Period(); var d = Period();
        a.Id = Guid.NewGuid(); b.Id = Guid.NewGuid(); c.Id = Guid.NewGuid(); d.Id = Guid.NewGuid();
        b.Collection!.Promise!.PromisedOn = Today.AddDays(-1);
        c.Collection!.Promise!.PromisedOn = Today.AddDays(28);
        d.Collection!.Promise = null;
        var plan = CollectionPlanning.Build([a, b, c, d], Today, "TRY", 4);
        Assert.Equal(400.4936m, plan.Outstanding); Assert.Equal(400.4936m, plan.Upcoming.Sum(x => x.Amount));
        Assert.Equal(60.1234m, plan.Promised.Sum(x => x.Amount)); Assert.Equal(60.1234m, plan.OverduePromises);
        Assert.Equal(60.1234m, plan.AfterHorizonPromises); Assert.Equal(220.1234m, plan.Unpromised);
        Assert.Equal(plan.Outstanding, plan.Promised.Sum(x => x.Amount) + plan.OverduePromises + plan.AfterHorizonPromises + plan.Unpromised);
        Assert.Equal(120.2468m, CollectionPlanning.Build([a, b, c, d], Today, "TRY", 8).Promised.Sum(x => x.Amount));
        Assert.Equal(0, CollectionPlanning.Build([a, b], Today, "USD", 4).Outstanding);
    }
}
