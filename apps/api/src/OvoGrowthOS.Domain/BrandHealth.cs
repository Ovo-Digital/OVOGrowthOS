using System.Globalization;

namespace OvoGrowthOS.Domain;

public sealed record BrandHealthFactor(string Code, string Label, int Effect, string Detail);
public sealed record BrandHealthScore(int Score, string Band, IReadOnlyList<BrandHealthFactor> Factors);

public sealed record BrandHealthInput(
    string Readiness,
    int AlertCount,
    TargetComparison? Target,
    PromiseBalance? Promise,
    DealStatus? DealStatus,
    DateOnly Today);

public static class BrandHealth
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static BrandHealthScore Evaluate(BrandHealthInput input)
    {
        var factors = new List<BrandHealthFactor>();
        factors.Add(DataQuality(input));
        factors.Add(TargetFactor(input));
        factors.Add(PromiseFactor(input));
        factors.Add(DealFactor(input.DealStatus));
        var score = Math.Clamp(100 + factors.Sum(x => x.Effect), 0, 100);
        var band = score >= 85 ? "Güçlü" : score >= 70 ? "İzlenmeli" : score >= 50 ? "Riskli" : "Kritik";
        return new BrandHealthScore(score, band, factors);
    }

    private static BrandHealthFactor DataQuality(BrandHealthInput input)
    {
        var (effect, state) = input.Readiness switch
        {
            "attention" => (-10, "dikkat bekliyor"),
            "missing" => (-20, "kayıt eksik"),
            "none" => (-20, "kayıt yok"),
            _ => (0, "hazır")
        };
        var detail = effect == 0
            ? "Veri kalitesi kontrolünden uyarı çıkmadı."
            : $"Veri kalitesi durumu: {state}; {input.AlertCount} uyarı bulundu.";
        return new BrandHealthFactor("dataQuality", "Veri kalitesi", effect, detail);
    }

    private static BrandHealthFactor TargetFactor(BrandHealthInput input)
    {
        var target = input.Target;
        if (target is null)
            return new BrandHealthFactor("target", "Hedef sapması", 0, "Bu ay için hedef kaydı yok; sapma ölçülmeyecek.");
        if (target.MissingReason is not null || target.PerformanceId is null)
            return new BrandHealthFactor("target", "Hedef sapması", -5, "Hedef var fakat gerçekleşen sonuç kaydı yok; sapma hesaplanamadı.");
        var shortfalls = new List<decimal>();
        foreach (var metric in target.Metrics.Where(x => x.NeedsAttention == true))
        {
            var shortfall = metric.Metric switch
            {
                TargetMetric.ContributionMargin => metric.PercentagePointDifference is { } pp ? Math.Max(0, -pp) / 100m : 0m,
                _ => metric.RelativeDifference is { } rel ? Math.Max(0, -rel) : 0m
            };
            if (metric.Metric == TargetMetric.AdSpend && metric.RelativeDifference is { } over && over > 0) shortfall = over;
            if (shortfall > 0) shortfalls.Add(shortfall);
        }
        if (shortfalls.Count == 0)
            return new BrandHealthFactor("target", "Hedef sapması", 0, "Tüm hedefler tutturuldu ya da sapma eşiğin altında.");
        var worst = shortfalls.Max();
        var effect = (int)Math.Max(-25, Math.Min(-5, Math.Round(-worst * 50m)));
        return new BrandHealthFactor("target", "Hedef sapması", effect,
            $"En ağır sapma hedefin {worst.ToString("P0", Tr)} altı/üstünde.");
    }

    private static BrandHealthFactor PromiseFactor(BrandHealthInput input)
    {
        var promise = input.Promise;
        if (promise is not { State: "Overdue" })
            return new BrandHealthFactor("promise", "Tahsilat gecikmesi", 0, "Gecikmiş ödeme sözü görünmüyor.");
        var overdueDays = DaysBetween(promise.PromisedOn, input.Today);
        var effect = overdueDays > 30 ? -20 : overdueDays > 7 ? -12 : -5;
        return new BrandHealthFactor("promise", "Tahsilat gecikmesi", effect,
            $"Ödeme sözü {overdueDays} gün gecikti.");
    }

    private static BrandHealthFactor DealFactor(DealStatus? status)
    {
        var (effect, text) = status switch
        {
            null => (-15, "Aktif anlaşma yok"),
            DealStatus.Active => (0, "Anlaşma etkin"),
            DealStatus.Accepted => (-5, "Anlaşma kabul edildi ama henüz etkin değil"),
            DealStatus.Expired => (-20, "Anlaşmanın süresi doldu"),
            DealStatus.Terminated => (-25, "Anlaşma feshedildi"),
            DealStatus.Rejected => (-25, "Anlaşma reddedildi"),
            _ => (-10, "Anlaşma henüz kesinleşmedi")
        };
        return new BrandHealthFactor("deal", "Anlaşma durumu", effect, text + ".");
    }

    private static int DaysBetween(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber;
        return days < 0 ? 0 : days;
    }
}
