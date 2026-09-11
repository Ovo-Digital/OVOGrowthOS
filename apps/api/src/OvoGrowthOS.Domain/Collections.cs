namespace OvoGrowthOS.Domain;

public sealed class CollectionAccount
{
    public Guid MonthlyPerformanceId { get; set; }
    public decimal ReceivableAmount { get; set; }
    public decimal LegacyPaidAmount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string InvoiceReference { get; set; } = "";
    public DateOnly? InvoiceOn { get; set; }
    public DateOnly? DueOn { get; set; }
    public int Revision { get; set; }
    public List<CollectionPayment> Payments { get; set; } = [];
}

public sealed class CollectionPayment
{
    public Guid Id { get; set; }
    public Guid MonthlyPerformanceId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VoidedAt { get; set; }
    public string VoidedBy { get; set; } = "";
    public string VoidReason { get; set; } = "";
}

public sealed record CollectionBalance(decimal Receivable, decimal Paid, decimal Outstanding, decimal LegacyPaid,
    decimal DatedPayments, int OverdueDays, string State, bool NeedsReview);

public static class Collections
{
    public static CollectionBalance Balance(MonthlyPerformance period, DateOnly today)
    {
        var account = period.Collection;
        var amount = account?.ReceivableAmount ?? period.OvoFee;
        var legacy = account?.LegacyPaidAmount ?? (period.Status == MonthlyPerformanceStatus.Paid ? Math.Max(amount, 0) : 0);
        var dated = account?.Payments.Where(x => x.VoidedAt is null).Sum(x => x.Amount) ?? 0;
        var paid = legacy + dated;
        var outstanding = Math.Max(amount - paid, 0);
        var review = amount < 0 || paid > amount;
        var closed = PortfolioReporting.IsClosed(period.Status);
        var overdue = closed && outstanding > 0 && account?.DueOn is { } due && due < today ? today.DayNumber - due.DayNumber : 0;
        return new(amount, paid, closed ? outstanding : 0, legacy, dated, overdue,
            review ? "NeedsReview" : !closed ? "NotClosed" : outstanding == 0 ? "Settled" : paid > 0 ? "PartiallyPaid" : "Unpaid", review);
    }

    public static decimal PaidInCalendarMonth(IEnumerable<MonthlyPerformance> periods, int year, int month) => periods
        .SelectMany(x => x.Collection?.Payments ?? []).Where(x => x.VoidedAt is null && x.PaidOn.Year == year && x.PaidOn.Month == month).Sum(x => x.Amount);

    public static string? PaymentError(MonthlyPerformance period, decimal amount, DateOnly paidOn, DateOnly today)
    {
        if (period.Collection is null || period.Status is not (MonthlyPerformanceStatus.Invoiced or MonthlyPerformanceStatus.Paid)) return "Önce fatura ve tahsilat takibini başlatın.";
        var balance = Balance(period, today);
        if (balance.NeedsReview) return "Bu hakediş inceleme gerektiriyor; ödeme eklemeden önce yöneticinizle görüşün.";
        if (amount <= 0 || decimal.Round(amount, 4) != amount) return "Ödeme tutarı sıfırdan büyük ve en fazla dört ondalık haneli olmalıdır.";
        if (amount > balance.Outstanding) return "Ödeme kalan alacaktan fazla olamaz. Fazla ödeme veya mahsup bu akışta desteklenmiyor.";
        if (paidOn > today) return "Gerçekleşmiş ödeme tarihi gelecekte olamaz.";
        return null;
    }

    // Only settlement flags change; locked commercial calculations and snapshots stay untouched.
    public static void UpdateSettlementStatus(MonthlyPerformance period, DateOnly today)
    {
        if (period.Collection is null || period.Status is not (MonthlyPerformanceStatus.Invoiced or MonthlyPerformanceStatus.Paid))
            throw new InvalidOperationException("Tahsilat yalnızca faturalanmış kapalı dönemde güncellenebilir.");
        var paid = Balance(period, today).Outstanding == 0;
        period.Status = paid ? MonthlyPerformanceStatus.Paid : MonthlyPerformanceStatus.Invoiced;
        period.CommissionStatus = paid ? CommissionStatus.Paid : CommissionStatus.Invoiced;
    }
}
