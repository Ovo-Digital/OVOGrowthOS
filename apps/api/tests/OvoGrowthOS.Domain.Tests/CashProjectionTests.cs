using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class CashProjectionTests
{
    private static readonly DateOnly Today = new(2026, 9, 17);
    private static MonthlyPerformance Period(DateOnly? due, decimal fee = 100, string currency = "TRY",
        MonthlyPerformanceStatus status = MonthlyPerformanceStatus.Invoiced) => new()
    {
        Id = Guid.NewGuid(), Brand = new Brand { Name = "Deneme" }, Deal = new Deal { Name = "Deneme anlaşması", Currency = currency },
        Status = status, OvoFee = fee, NetRevenue = 1000, CommissionBreakdownJson = "korunan hesap",
        Collection = new() { ReceivableAmount = fee, Currency = currency, DueOn = due }
    };

    [Fact]
    public void Empty_portfolio_returns_thirteen_empty_weeks_and_zero_totals()
    {
        var report = CashProjection.Build([], Today, "TRY");
        Assert.Equal(CashProjection.HorizonWeeks, report.Weeks);
        Assert.Equal(13, report.Rows.Count);
        Assert.Equal(0, report.DueTotal); Assert.Equal(0, report.PromisedTotal);
        Assert.Equal(0, report.Overdue); Assert.Equal(0, report.UnknownDue); Assert.Equal(0, report.BeyondHorizon);
        Assert.Equal(0, report.ClosedPeriods); Assert.Equal("TRY", report.Currency);
        Assert.Equal(Today, report.Today); Assert.Equal(Today.AddDays(13 * 7 - 1), report.Horizon);
    }

    [Fact]
    public void Outstanding_is_partitioned_once_between_weeks_overdue_unknown_and_beyond()
    {
        var data = new[] { Period(null, 10), Period(Today.AddDays(-1), 20), Period(Today, 30), Period(Today.AddDays(6), 40),
            Period(Today.AddDays(7), 50), Period(Today.AddDays(90), 60), Period(Today.AddDays(91), 70) };
        var report = CashProjection.Build(data, Today, "TRY");
        Assert.Equal(10, report.UnknownDue); Assert.Equal(20, report.Overdue); Assert.Equal(70, report.BeyondHorizon);
        Assert.Equal(180, report.DueTotal);
        Assert.Equal(10 + 20 + 70 + report.DueTotal, data.Sum(x => x.OvoFee));
        Assert.Equal(new decimal[] { 70, 50, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 60 }, report.Rows.Select(x => x.Due));
        Assert.Equal(new int[] { 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, report.Rows.Select(x => x.DueCount));
        Assert.Equal(7, report.ClosedPeriods);
        Assert.All(data, p => Assert.Equal("korunan hesap", p.CommissionBreakdownJson));
    }

    [Fact]
    public void Promises_are_kept_in_their_own_column_and_never_added_to_vades()
    {
        var dueSoon = Period(Today.AddDays(3), 100);
        dueSoon.Collection!.Promise = new CollectionPromise { Amount = 50, PromisedOn = Today.AddDays(2), ContactNoteId = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var dueAndPromiseSameWeek = Period(Today.AddDays(1), 80);
        dueAndPromiseSameWeek.Collection!.Promise = new CollectionPromise { Amount = 30, PromisedOn = Today.AddDays(1), ContactNoteId = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var report = CashProjection.Build([dueSoon, dueAndPromiseSameWeek], Today, "TRY");
        Assert.Equal(180, report.DueTotal);
        Assert.Equal(80, report.PromisedTotal);
        Assert.Equal(80, report.Rows[0].Promised);
        Assert.Equal(2, report.Rows[0].PromiseCount);
        Assert.Equal(0, report.OverduePromises); Assert.Equal(0, report.BeyondHorizonPromises);
        Assert.Equal(report.Rows.Sum(x => x.Promised), report.PromisedTotal);
    }

    [Fact]
    public void Overdue_and_distant_promises_stay_outside_the_thirteen_week_window()
    {
        var late = Period(Today, 100);
        late.Collection!.Promise = new CollectionPromise { Amount = 40, PromisedOn = Today.AddDays(-5), ContactNoteId = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var far = Period(Today, 60);
        far.Collection!.Promise = new CollectionPromise { Amount = 25, PromisedOn = Today.AddDays(200), ContactNoteId = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var report = CashProjection.Build([late, far], Today, "TRY");
        Assert.Equal(40, report.OverduePromises); Assert.Equal(25, report.BeyondHorizonPromises);
        Assert.Equal(0, report.PromisedTotal);
        Assert.Equal(40 + 25, report.OverduePromises + report.BeyondHorizonPromises + report.Rows.Sum(x => x.Promised));
    }

    [Fact]
    public void Payments_reduce_the_due_column_because_only_the_remaining_balance_is_bucketed()
    {
        var p = Period(Today.AddDays(4), 100);
        p.Collection!.Payments.Add(new CollectionPayment { Amount = 60, PaidOn = Today });
        var report = CashProjection.Build([p], Today, "TRY");
        Assert.Equal(40, report.DueTotal); Assert.Equal(40, report.Rows[0].Due);
        Assert.Equal(0, report.Overdue); Assert.Equal(0, report.UnknownDue);
    }

    [Fact]
    public void Other_currencies_drafts_settled_records_and_review_cases_are_not_mixed()
    {
        var usd = Period(Today.AddDays(2), 500, "USD");
        var draft = Period(Today.AddDays(2), 900, "TRY", MonthlyPerformanceStatus.Draft);
        var settled = Period(Today.AddDays(2), 700, "TRY", MonthlyPerformanceStatus.Paid);
        settled.Collection!.Payments.Add(new CollectionPayment { Amount = 700, PaidOn = Today });
        var review = Period(Today.AddDays(2), 100, "TRY");
        review.Collection!.ReceivableAmount = -50; review.OvoFee = -50;
        var report = CashProjection.Build([usd, draft, settled, review], Today, "TRY");
        Assert.Equal(0, report.DueTotal);
        Assert.Equal(2, report.ClosedPeriods);
        Assert.Equal(0, report.UnknownDue + report.Overdue + report.BeyondHorizon);
        var usdReport = CashProjection.Build([usd, draft], Today, "USD");
        Assert.Equal(500, usdReport.DueTotal); Assert.Equal(1, usdReport.ClosedPeriods);
    }

    [Fact]
    public void Week_windows_are_inclusive_and_contiguous_without_overlap()
    {
        var report = CashProjection.Build([Period(Today)], Today, "TRY");
        Assert.Equal(Today, report.Rows[0].From);
        Assert.Equal(Today.AddDays(6), report.Rows[0].Through);
        Assert.Equal(Today.AddDays(90), report.Rows[^1].Through);
        for (var i = 1; i < report.Rows.Count; i++)
        {
            Assert.Equal(report.Rows[i - 1].Through.AddDays(1), report.Rows[i].From);
            Assert.Equal(6, report.Rows[i].Through.DayNumber - report.Rows[i].From.DayNumber);
        }
    }
}
