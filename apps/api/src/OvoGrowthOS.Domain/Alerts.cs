using System.Globalization;

namespace OvoGrowthOS.Domain;

public sealed record AlertItem(string Code, string Severity, string Title, string Detail, Guid? BrandId, string? BrandName, string Link);
public sealed record AlertSummary(int Critical, int Warning, int Info, int Total);
public sealed record AlertReport(DateOnly Today, string PeriodLabel, AlertSummary Summary, IReadOnlyList<AlertItem> Items);

public sealed record MissingCloseInput(Guid BrandId, string BrandName);
public sealed record OverdueReceivableInput(Guid BrandId, string BrandName, int Days, decimal Amount, string Currency);
public sealed record OverduePromiseInput(Guid BrandId, string BrandName, int Days, decimal Remaining, string Currency);
public sealed record MerBreachInput(Guid BrandId, string BrandName, Guid PerformanceId, string PeriodLabel, decimal Mer, decimal BreakEvenMer);

// Read-only early warning center: every input comes from existing ledgers and closed records.
// The engine never changes a record, never opens a task and never produces a financial decision.
public static class AlertEngine
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public const int StageTimeoutDays = 30;
    public const int LongOverdueDays = 30;
    public const decimal MerCriticalRatio = .80m;

    private static readonly string[] SeverityOrder = ["critical", "warning", "info"];

    public static AlertReport Build(
        DateOnly today, string periodLabel,
        IReadOnlyCollection<MissingCloseInput> missingClose,
        IReadOnlyCollection<OverdueReceivableInput> receivables,
        IReadOnlyCollection<OverduePromiseInput> promises,
        IReadOnlyCollection<MerBreachInput> merBreaches,
        int timedOutLeads, int longestLeadDays,
        int overdueTasks)
    {
        var items = new List<AlertItem>();

        foreach (var row in missingClose.OrderBy(x => x.BrandName, StringComparer.CurrentCultureIgnoreCase))
            items.Add(new AlertItem("missing_close", "warning", "Aylık kayıt eksik",
                $"{row.BrandName} için {periodLabel} dönemi aylık sonucu girilmemiş. Bu marka bu ay toplamlara dahil değil; verisi yok.",
                row.BrandId, row.BrandName, "/data-quality"));

        foreach (var row in receivables.OrderByDescending(x => x.Days).ThenBy(x => x.BrandName, StringComparer.CurrentCultureIgnoreCase))
            items.Add(new AlertItem("overdue_receivable", row.Days > LongOverdueDays ? "critical" : "warning", "Vadesi geçmiş alacak",
                $"{row.BrandName}: son ödeme tarihi {row.Days} gün geçti; kalan alacak {row.Amount.ToString("N2", Tr)} {row.Currency}.",
                row.BrandId, row.BrandName, "/commissions/planning"));

        foreach (var row in promises.OrderByDescending(x => x.Days).ThenBy(x => x.BrandName, StringComparer.CurrentCultureIgnoreCase))
            items.Add(new AlertItem("overdue_promise", row.Days > LongOverdueDays ? "critical" : "warning", "Ödeme sözü gecikti",
                $"{row.BrandName}: verilen ödeme sözü {row.Days} gün gecikti; kalan tutar {row.Remaining.ToString("N2", Tr)} {row.Currency}.",
                row.BrandId, row.BrandName, "/commissions/planning"));

        foreach (var row in merBreaches)
        {
            var critical = row.Mer < row.BreakEvenMer * MerCriticalRatio;
            items.Add(new AlertItem("mer_below_breakeven", critical ? "critical" : "warning", "Reklam verimliliği başa başın altında",
                $"{row.BrandName} ({row.PeriodLabel}): MER {row.Mer.ToString("0.00", Tr)}x, başa baş {row.BreakEvenMer.ToString("0.00", Tr)}x. Reklam harcaması bu dönemde markanın hedeflenen katkı marjını siliyor olabilir.",
                row.BrandId, row.BrandName, $"/performance/{row.PerformanceId}"));
        }

        if (timedOutLeads > 0)
            items.Add(new AlertItem("stage_timeout", "warning", "Aday hattında uzun bekleme",
                $"Aday hattında {timedOutLeads} marka {StageTimeoutDays} günden uzun süredir aynı aşamada bekliyor; en uzun bekleme {longestLeadDays} gün.",
                null, null, "/leads"));

        if (overdueTasks > 0)
            items.Add(new AlertItem("overdue_task", "info", "Son tarihi geçen görev",
                $"{overdueTasks} görev son tarihini geçti. İşlerim → Gecikenler listesinden bakabilirsiniz.",
                null, null, "/work"));

        var ordered = items
            .OrderBy(x => Array.IndexOf(SeverityOrder, x.Severity))
            .ThenBy(x => x.BrandName ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var summary = new AlertSummary(
            ordered.Count(x => x.Severity == "critical"),
            ordered.Count(x => x.Severity == "warning"),
            ordered.Count(x => x.Severity == "info"),
            ordered.Count);
        return new AlertReport(today, periodLabel, summary, ordered);
    }
}
