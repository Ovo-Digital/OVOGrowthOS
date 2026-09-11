using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class CollectionTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);
    private static MonthlyPerformance Period(decimal fee = 100_000) => new() { Status = MonthlyPerformanceStatus.Invoiced, OvoFee = fee,
        NetRevenue = 1_000_000, OvoGrossProfit = 70_000, CommissionBreakdownJson = "snapshot",
        Collection = new() { ReceivableAmount = fee, DueOn = Today.AddDays(-3) } };

    [Fact]
    public void Partial_full_and_void_preserve_financial_snapshot()
    {
        var p = Period(); var first = new CollectionPayment { Amount = 40_000, PaidOn = Today };
        p.Collection!.Payments.Add(first); Collections.UpdateSettlementStatus(p, Today);
        Assert.Equal(new CollectionBalance(100_000, 40_000, 60_000, 0, 40_000, 3, "PartiallyPaid", false), Collections.Balance(p, Today));
        p.Collection.Payments.Add(new() { Amount = 60_000, PaidOn = Today }); Collections.UpdateSettlementStatus(p, Today);
        Assert.Equal(MonthlyPerformanceStatus.Paid, p.Status); Assert.Equal(0, Collections.Balance(p, Today).Outstanding);
        first.VoidedAt = DateTimeOffset.UtcNow; Collections.UpdateSettlementStatus(p, Today);
        Assert.Equal(MonthlyPerformanceStatus.Invoiced, p.Status); Assert.Equal(40_000, Collections.Balance(p, Today).Outstanding);
        Assert.Equal(1_000_000, p.NetRevenue); Assert.Equal(100_000, p.OvoFee); Assert.Equal(70_000, p.OvoGrossProfit); Assert.Equal("snapshot", p.CommissionBreakdownJson);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(100001)] [InlineData(0.00001)]
    public void Invalid_amount_is_rejected(decimal amount) => Assert.NotNull(Collections.PaymentError(Period(), amount, Today, Today));

    [Fact]
    public void Exact_balance_and_four_decimal_payment_are_allowed_but_future_is_not()
    {
        Assert.Null(Collections.PaymentError(Period(), 100_000, Today, Today));
        Assert.Null(Collections.PaymentError(Period(), .0001m, Today, Today));
        Assert.NotNull(Collections.PaymentError(Period(), 1, Today.AddDays(1), Today));
        Assert.NotNull(Collections.PaymentError(Period(-100), 1, Today, Today));
    }

    [Fact]
    public void Legacy_paid_is_preserved_without_inventing_calendar_receipts()
    {
        var legacy = new MonthlyPerformance { Status = MonthlyPerformanceStatus.Paid, OvoFee = 30_000 };
        Assert.Equal(30_000, Collections.Balance(legacy, Today).LegacyPaid);
        Assert.Equal(0, Collections.PaidInCalendarMonth([legacy], 2026, 9));
        var p = Period(); p.Year = 2026; p.Month = 8;
        p.Collection!.Payments.Add(new() { Amount = 40_000, PaidOn = Today });
        Assert.Equal(40_000, Collections.PaidInCalendarMonth([p, legacy], 2026, 9));
        Assert.Equal(0, Collections.PaidInCalendarMonth([p], 2026, 8));
        Assert.Equal(70_000, PortfolioReporting.Paid([p, legacy]));
    }

    [Fact]
    public void Due_today_unknown_due_draft_and_zero_amount_do_not_create_overdue()
    {
        var p = Period(); p.Collection!.DueOn = Today; Assert.Equal(0, Collections.Balance(p, Today).OverdueDays);
        p.Collection.DueOn = null; Assert.Equal(0, Collections.Balance(p, Today).OverdueDays);
        p.Status = MonthlyPerformanceStatus.Draft; Assert.Equal(0, Collections.Balance(p, Today).Outstanding);
        var zero = Period(0); Collections.UpdateSettlementStatus(zero, Today); Assert.Equal(MonthlyPerformanceStatus.Paid, zero.Status);
    }
}
