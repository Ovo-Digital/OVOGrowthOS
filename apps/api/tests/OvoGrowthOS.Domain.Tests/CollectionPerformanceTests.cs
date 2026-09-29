using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class CollectionPerformanceTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);

    private static MonthlyPerformance Settled(DateOnly due, string currency, params (decimal Amount, DateOnly PaidOn)[] payments)
    {
        var total = payments.Sum(x => x.Amount);
        var p = new MonthlyPerformance { Status = MonthlyPerformanceStatus.Invoiced, OvoFee = total,
            Collection = new CollectionAccount { ReceivableAmount = total, Currency = currency, DueOn = due } };
        foreach (var (amount, paidOn) in payments) p.Collection.Payments.Add(new CollectionPayment { Amount = amount, PaidOn = paidOn });
        Collections.UpdateSettlementStatus(p, Today);
        return p;
    }

    [Fact]
    public void Rate_and_average_days_use_exact_on_time_and_late_amounts()
    {
        var due = new DateOnly(2026, 9, 1);
        var onTime = Settled(due, "TRY", (60_000m, new DateOnly(2026, 8, 30)), (40_000m, new DateOnly(2026, 9, 6)));
        var stats = CollectionPerformance.Calculate([onTime], Today, "TRY");
        Assert.Equal(0.6m, stats.OnTimeRate);
        Assert.Equal(1.5m, stats.AverageDays);
        Assert.Equal(1, stats.RecordCount);
        Assert.Equal(2, stats.PaymentCount);
        Assert.Equal(1, stats.OnTimePayments);
        Assert.Equal(60_000m, stats.OnTimeAmount);
        Assert.Equal(100_000m, stats.TotalAmount);
    }

    [Fact]
    public void Voided_legacy_unknown_due_unsettled_and_foreign_currency_records_do_not_count()
    {
        var due = new DateOnly(2026, 9, 1);
        var withVoided = new MonthlyPerformance { Status = MonthlyPerformanceStatus.Invoiced, OvoFee = 100_000,
            Collection = new CollectionAccount { ReceivableAmount = 100_000, Currency = "TRY", DueOn = due,
                Payments = [new CollectionPayment { Amount = 100_000m, PaidOn = due },
                    new CollectionPayment { Amount = 50_000m, PaidOn = new DateOnly(2026, 9, 7) }] } };
        Collections.UpdateSettlementStatus(withVoided, Today);
        withVoided.Collection!.Payments[1].VoidedAt = DateTimeOffset.UtcNow;

        var legacyOnly = new MonthlyPerformance { Status = MonthlyPerformanceStatus.Paid, OvoFee = 30_000,
            Collection = new CollectionAccount { ReceivableAmount = 30_000, LegacyPaidAmount = 30_000, Currency = "TRY", DueOn = due } };

        var unknownDue = Settled(new DateOnly(2026, 9, 1), "TRY", (10_000m, new DateOnly(2026, 9, 1)));
        unknownDue.Collection!.DueOn = null;

        var partial = new MonthlyPerformance { Status = MonthlyPerformanceStatus.Invoiced, OvoFee = 50_000,
            Collection = new CollectionAccount { ReceivableAmount = 50_000, Currency = "TRY", DueOn = due,
                Payments = [new CollectionPayment { Amount = 20_000, PaidOn = new DateOnly(2026, 9, 1) }] } };

        var foreign = Settled(due, "USD", (999m, new DateOnly(2026, 9, 5)));

        var all = CollectionPerformance.Calculate([withVoided, legacyOnly, unknownDue, partial, foreign], Today, "TRY");
        Assert.Equal(1, all.RecordCount);
        Assert.Equal(1, all.PaymentCount);
        Assert.Equal(1, all.OnTimePayments);
        Assert.Equal(1m, all.OnTimeRate);
        Assert.Equal(0m, all.AverageDays);
        Assert.Equal(100_000m, all.TotalAmount);

        var withoutCurrency = CollectionPerformance.Calculate([withVoided, legacyOnly, unknownDue, partial, foreign], Today);
        Assert.Equal(2, withoutCurrency.RecordCount);
        Assert.Equal(2, withoutCurrency.PaymentCount);
        Assert.Equal(100_999m, withoutCurrency.TotalAmount);
        Assert.Equal(decimal.Round(100_000m / 100_999m, 4), withoutCurrency.OnTimeRate);
    }

    [Fact]
    public void Empty_input_gives_explicit_no_data_instead_of_zero()
    {
        var stats = CollectionPerformance.Calculate([], Today, "TRY");
        Assert.Null(stats.OnTimeRate);
        Assert.Null(stats.AverageDays);
        Assert.Equal(0, stats.RecordCount);
        Assert.Equal(0, stats.PaymentCount);
        Assert.Equal(0m, stats.OnTimeAmount);
        Assert.Equal(0m, stats.TotalAmount);
    }

    [Fact]
    public void Rounding_happens_at_the_stated_boundaries_only()
    {
        var due = new DateOnly(2026, 9, 1);
        var third = Settled(due, "TRY", (10_000m, new DateOnly(2026, 8, 31)), (10_000m, new DateOnly(2026, 9, 1)), (10_000m, new DateOnly(2026, 9, 3)));
        var stats = CollectionPerformance.Calculate([third], Today, "TRY");
        Assert.Equal(0.6667m, stats.OnTimeRate);
        Assert.Equal(0.3m, stats.AverageDays);

        var early = Settled(due, "TRY", (10_000m, new DateOnly(2026, 8, 31)), (10_000m, new DateOnly(2026, 8, 31)), (10_000m, new DateOnly(2026, 9, 1)));
        var days = CollectionPerformance.Calculate([early], Today, "TRY");
        Assert.Equal(1m, days.OnTimeRate);
        Assert.Equal(-0.7m, days.AverageDays);
    }

    [Fact]
    public void Negative_or_zero_amounts_are_ignored_even_when_the_record_settles()
    {
        var due = new DateOnly(2026, 9, 1);
        var p = new MonthlyPerformance { Status = MonthlyPerformanceStatus.Invoiced, OvoFee = 95_000,
            Collection = new CollectionAccount { ReceivableAmount = 95_000, Currency = "TRY", DueOn = due,
                Payments = [new CollectionPayment { Amount = 100_000m, PaidOn = due },
                    new CollectionPayment { Amount = -5_000m, PaidOn = new DateOnly(2026, 9, 7) },
                    new CollectionPayment { Amount = 0m, PaidOn = new DateOnly(2026, 9, 7) }] } };
        Collections.UpdateSettlementStatus(p, Today);
        Assert.Equal("Settled", Collections.Balance(p, Today).State);
        var stats = CollectionPerformance.Calculate([p], Today, "TRY");
        Assert.Equal(1m, stats.OnTimeRate);
        Assert.Equal(0m, stats.AverageDays);
        Assert.Equal(1, stats.PaymentCount);
        Assert.Equal(100_000m, stats.TotalAmount);
    }
}
