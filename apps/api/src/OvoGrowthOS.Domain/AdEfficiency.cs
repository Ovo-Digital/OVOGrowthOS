using System.Globalization;

namespace OvoGrowthOS.Domain;

public sealed record AdEfficiencyPoint(int Year, int Month, decimal AdSpend, decimal NetRevenue, decimal? Mer);
public sealed record AdEfficiencyTrend(IReadOnlyList<AdEfficiencyPoint> Points);

public sealed record AdEfficiencySummary(
    string BandCode, string BandLabel, string BandDetail,
    decimal? LatestMer, decimal? BreakEvenMer,
    string PeriodLabel, string Note);

// Read-only ad efficiency view over already-closed periods. Never touches invoices,
// collections or period state and never decides a budget change on its own.
public static class AdEfficiency
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public const decimal WatchRatio = .90m;

    public static decimal? Mer(decimal netRevenue, decimal adSpend) =>
        adSpend <= 0 ? null : FinancialCalculator.Ratio(netRevenue, adSpend);

    public static string Band(decimal? mer, decimal? breakEven)
    {
        if (mer is null) return "unknown";
        if (breakEven is null or <= 0) return "unknown";
        if (mer >= breakEven.Value) return "strong";
        if (mer >= breakEven.Value * WatchRatio) return "watch";
        return "risk";
    }

    public static string BandLabel(string band) => band switch
    {
        "strong" => "Güçlü",
        "watch" => "İzlenmeli",
        "risk" => "Riskli",
        _ => "Bilinmiyor"
    };

    public static AdEfficiencySummary Build(decimal? breakEvenMer, IReadOnlyList<AdEfficiencyPoint> pointsNewestFirst)
    {
        var latest = pointsNewestFirst.FirstOrDefault();
        var mer = latest?.Mer;
        var band = Band(mer, breakEvenMer);
        var be = breakEvenMer is > 0 ? breakEvenMer : null;
        var detail = band switch
        {
            "strong" => $"Son kapalı dönemde MER {mer!.Value.ToString("0.00", Tr)}x; başa baş hedefi {be!.Value.ToString("0.00", Tr)}x. Reklam harcaması markanın katkı marjını koruyor.",
            "watch" => $"Son kapalı dönemde MER {mer!.Value.ToString("0.00", Tr)}x; başa baş hedefi {be!.Value.ToString("0.00", Tr)}x. Reklam harcaması hedefin çok yakın; bütçe artışı katkıyı aşındırabilir.",
            "risk" => $"Son kapalı dönemde MER {mer!.Value.ToString("0.00", Tr)}x; başa baş hedefi {be!.Value.ToString("0.00", Tr)}x. Reklam harcaması başa başın altında; bu dönemde markanın kârını siliyor olabilir.",
            _ when mer is null => latest is null
                ? "Kapatılmış bir dönem verisi yok; MER hesaplanamıyor."
                : "Son kapalı dönemde reklam harcaması yok; MER hesaplanamıyor.",
            _ => "Bu marka için başa baş MER hedefi bulunmuyor (onaylı değerlendirme analizi gerekli); bant gösterilemiyor."
        };
        var periodLabel = latest is null ? "-" : $"{latest.Month:00}/{latest.Year}";
        return new AdEfficiencySummary(band, BandLabel(band), detail, mer, be, periodLabel,
            "Bu kart yalnız kilitlenmiş (ödenmiş/onaylanmış) dönem verilerinden hesaplanır; hakediş faturasını veya dönem kararını değiştirmez.");
    }

    public static string FormatMer(decimal? mer) => mer is null ? "-" : mer.Value.ToString("0.00", Tr) + "x";
}
