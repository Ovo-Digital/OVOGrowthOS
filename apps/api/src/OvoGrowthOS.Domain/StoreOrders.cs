namespace OvoGrowthOS.Domain;

public sealed class StoreOrderStaging
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string SourceOrderId { get; set; } = "";
    public string SourceStoreId { get; set; } = "";
    public int OrderNumber { get; set; }
    public DateTimeOffset PlacedOnUtc { get; set; }
    public string Currency { get; set; } = "";
    public decimal OrderTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public int OrderStatus { get; set; }
    public int PaymentStatus { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}

public sealed record StoreOrderPeriod(int Year, int Month)
{
    // GrandNode stores order dates in UTC; Turkish stores report months in Türkiye time (UTC+3, no daylight saving).
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public static bool TryParse(string? value, out StoreOrderPeriod period)
    {
        period = new StoreOrderPeriod(0, 0);
        if (value is not { Length: 7 } || value[4] != '-' ||
            !int.TryParse(value.AsSpan(0, 4), out var year) || !int.TryParse(value.AsSpan(5, 2), out var month))
            return false;
        if (year is < 2020 or > 2100 || month is < 1 or > 12) return false;
        period = new StoreOrderPeriod(year, month);
        return true;
    }

    public static StoreOrderPeriod Parse(string value) => TryParse(value, out var period) ? period : throw new FormatException("Geçersiz dönem anahtarı.");

    public DateTimeOffset UtcStart => new DateTimeOffset(Year, Month, 1, 0, 0, 0, TurkeyOffset).ToUniversalTime();
    public DateTimeOffset UtcEnd => new DateTimeOffset(Year, Month, 1, 0, 0, 0, TurkeyOffset).AddMonths(1).ToUniversalTime();
    public string Key => $"{Year}-{Month:00}";
    public bool Contains(DateTimeOffset instant) => instant >= UtcStart && instant < UtcEnd;
}

public sealed record StoreOrderPanelComparison(bool PanelExists, decimal? PanelGrossSales, decimal? Difference);

public sealed record StoreOrderSummary(int OrderCount, decimal GrossTotal, int CancelledCount, decimal CancelledTotal,
    decimal PaidTotal, decimal RefundedTotal, string Currency)
{
    public decimal ActiveTotal => GrossTotal - CancelledTotal;

    public static StoreOrderSummary Summarize(IEnumerable<StoreOrderStaging> rows, string currency)
    {
        var count = 0; var gross = 0m; var cancelledCount = 0; var cancelled = 0m; var paid = 0m; var refunded = 0m;
        foreach (var row in rows)
        {
            count++; gross += row.OrderTotal; paid += row.PaidAmount; refunded += row.RefundedAmount;
            if (row.OrderStatus == StoreOrderStatus.Cancelled) { cancelledCount++; cancelled += row.OrderTotal; }
        }
        return new StoreOrderSummary(count, gross, cancelledCount, cancelled, paid, refunded, currency);
    }

    public StoreOrderPanelComparison ComparePanel(decimal? panelGrossSales) =>
        panelGrossSales is null ? new StoreOrderPanelComparison(false, null, null) : new StoreOrderPanelComparison(true, panelGrossSales, ActiveTotal - panelGrossSales.Value);
}

public static class StoreOrderStatus
{
    public const int Pending = 10;
    public const int Processing = 20;
    public const int Complete = 30;
    public const int Cancelled = 40;
    public const int Confirmed = 50;
}
