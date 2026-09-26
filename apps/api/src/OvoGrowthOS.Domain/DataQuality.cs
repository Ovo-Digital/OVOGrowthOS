using System.Globalization;

namespace OvoGrowthOS.Domain;

public sealed record QualityPeriod(int Year, int Month);
public sealed record QualitySource(string Key, string Label, string State, decimal Amount, string Note);
public sealed record QualityAlert(string Code, string Severity, string Finding, string WhyItMatters, string NextStep);
public sealed record QualityApproval(string? PreparedBy, string? ReviewedBy, bool Complete);

public sealed record BrandQuality(
    Guid BrandId, string BrandName, Guid? DealId, string Expectation, Guid? PerformanceId,
    MonthlyPerformanceStatus? Status, string Origin, string OriginDetail,
    string Responsible, Guid? ResponsibleId, QualityApproval? Approval,
    IReadOnlyList<QualitySource> Sources, IReadOnlyList<QualityAlert> Alerts,
    string Readiness, Guid? TaskId, bool TaskCompleted);

public sealed record QualitySummary(int Total, int Ready, int Attention, int Missing, int NotApplicable, int WithOpenTask);
public sealed record QualityReport(QualityPeriod Period, string Label, QualitySummary Summary, IReadOnlyList<BrandQuality> Brands);

public sealed record QualityInput(
    int Year, int Month, Guid BrandId, string BrandName, Deal? Deal, MonthlyPerformance? Current, MonthlyPerformance? Previous,
    string Origin, string OriginDetail, string Responsible, Guid? ResponsibleId,
    Guid? TaskId, bool TaskCompleted, decimal VatRate);

public static class DataQuality
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public const decimal ReturnShareWarning = .15m;
    public const decimal MonthChangeRatio = .50m;
    public const decimal MonthChangeFloor = 5_000m;
    public const decimal VatRateTolerance = .05m;

    private static readonly string[] MonthNames =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    public static QualityPeriod Previous(QualityPeriod period) =>
        period.Month == 1 ? new QualityPeriod(period.Year - 1, 12) : new QualityPeriod(period.Year, period.Month - 1);

    public static string Label(int year, int month) => month is >= 1 and <= 12 ? $"{MonthNames[month - 1]} {year}" : $"{year}";

    public static decimal Returns(MonthlyPerformance p) => p.Refunds + p.Cancellations + p.Chargebacks;
    public static decimal Costs(MonthlyPerformance p) => p.Cogs + p.PaymentFees + p.FulfillmentCosts + p.ShippingSubsidy + p.OtherVariableCosts;

    public static QualitySummary Summarize(IEnumerable<BrandQuality> brands)
    {
        var rows = brands.ToList();
        return new QualitySummary(rows.Count, rows.Count(x => x.Readiness == "ready"),
            rows.Count(x => x.Readiness == "attention"), rows.Count(x => x.Readiness == "missing"),
            rows.Count(x => x.Readiness == "notApplicable"), rows.Count(x => x.TaskId is not null && !x.TaskCompleted));
    }

    public static BrandQuality Evaluate(QualityInput input)
    {
        var period = new QualityPeriod(input.Year, input.Month);
        var expectation = Expectation(input.Deal, period);
        QualityApproval? approval = input.Current is null ? null
            : new QualityApproval(OrNull(input.Current.PreparedBy), OrNull(input.Current.ReviewedBy),
                !string.IsNullOrEmpty(input.Current.ReviewedBy) && input.Current.ReviewedBy != input.Current.PreparedBy);

        if (input.Current is null)
        {
            var missing = expectation == "expected";
            var alerts = new List<QualityAlert>();
            if (missing) alerts.Add(new QualityAlert("missing_record", "warning",
                "Bu ay için aylık sonuç kaydı yok.",
                "Kayıt olmadığı için tutarlar sıfır sayılmadı; verinin olup olmadığı bilinmiyor.",
                $"Aylık sonuçlar ekranından {Label(input.Year, input.Month)} dönemini girin ya da eksikse takip işi açın."));
            else if (expectation == "unknownStart") alerts.Add(new QualityAlert("unknown_start", "info",
                "Anlaşma başlangıç tarihi girilmediği için bu ayın kapsamı belirlenemiyor.",
                "Kapsam tahmin edilemediği için kayıt olsa da olmasa da sonuç yorumlanamaz.",
                "Anlaşmada başlangıç ve bitiş tarihlerini netleştirin."));
            return new BrandQuality(input.BrandId, input.BrandName, input.Deal?.Id, expectation, null, null,
                "none", "", input.Responsible, input.ResponsibleId, null,
                Sources(null, missing ? "missing" : "notApplicable"), alerts,
                missing ? "missing" : "notApplicable", input.TaskId, input.TaskCompleted);
        }

        var current = input.Current;
        var previous = input.Previous;
        var alerts2 = new List<QualityAlert>();
        var returns = Returns(current);
        var costs = Costs(current);

        if (returns > current.GrossSales)
            alerts2.Add(new QualityAlert("returns_exceed_sales", "warning",
                "İade, iptal ve ters ibraz toplamı brüt satıştan büyük.",
                $"Kesintiler {returns.ToString("N2", Tr)}; brüt satış {current.GrossSales.ToString("N2", Tr)}.",
                "Kesintiler farklı tarih kapsamlarından iki kez düşülmüş olabilir. İade raporunun hangi ayları kapsadığını kontrol edin."));
        else if (current.GrossSales > 0 && returns / current.GrossSales > ReturnShareWarning)
            alerts2.Add(new QualityAlert("returns_share", "warning",
                $"İade ve kesinti oranı brüt satışın {(returns / current.GrossSales).ToString("P0", Tr)}'i.",
                $"Eşik yüzde {(ReturnShareWarning * 100).ToString("0", Tr)}; bu oran hakediş ve kâr hesabını doğrudan etkiler.",
                "İade raporunu ve iade nedenlerini inceleyin; hatalı dönem seçildiyse düzeltin."));

        if (current.GrossSales > 0 && current.Vat > 0)
        {
            var ratio = current.Vat / current.GrossSales;
            if (Math.Abs(ratio - input.VatRate) > VatRateTolerance)
                alerts2.Add(new QualityAlert("vat_share", "warning",
                    $"KDV brüt satışın {ratio.ToString("P0", Tr)}'i; ayarlanan oran {input.VatRate.ToString("P0", Tr)}.",
                    "Net ciro hesabı brüt satıştan KDV'yi bir kez düşer. Farklılık KDV'nin iki kez düşürüldüğünü gösterebilir.",
                    "Brüt satışın KDV dahil mi girildiğini ve KDV alanını kaynak belgeyle karşılaştırın."));
        }
        else if (current.GrossSales > 0 && current.Vat == 0)
            alerts2.Add(new QualityAlert("vat_zero", "info",
                "Brüt satış girildi ama KDV 0.",
                "KDV 0 ise brüt satış KDV hariç girilmiş olabilir; net ciro bu durumda farklı yorumlanır.",
                "Kaynak fatura veya platform raporunda KDV dahil mi kontrol edin."));

        if (current.NetRevenue < 0)
            alerts2.Add(new QualityAlert("negative_net", "warning",
                "Net ciro negatif.",
                "Brüt satıştan KDV ve kesintiler düşüldüğünde sonuç negatif; bu hatalı giriş ya da gerçek olabilir.",
                "Kesintilerin ve KDV'nin doğru dönemde girildiğini kontrol edin."));

        if (previous is not null && current.Refunds > 0 && previous.Refunds == current.Refunds)
            alerts2.Add(new QualityAlert("repeat_returns", "warning",
                "Aynı iade tutarı önceki ayda da görünüyor.",
                $"İade {current.Refunds.ToString("N2", Tr)} iki ardışık dönemde aynı.",
                "Aynı iade iki kez düşülmüş ya da iade iki aya bölünmüş olabilir. Tarih kapsamını kontrol edin."));

        if (previous is not null)
        {
            var identical = Returns(current) == Returns(previous) && current.GrossSales == previous.GrossSales
                && current.TotalAdSpend == previous.TotalAdSpend && costs == Costs(previous);
            if (identical && current.GrossSales > 0)
                alerts2.Add(new QualityAlert("same_as_previous", "warning",
                    "Dört kaynak tutarı da önceki ayla birebir aynı.",
                    "Tesadüfen aynı değerlerin çıkması olası değil; kayıt kopyalanmış ya da yanlış dönem seçilmiş olabilir.",
                    "Bu ayın kendi satış, iade, reklam ve gider raporlarını karşılaştırın."));
            foreach (var change in Changes(current, previous, returns, costs)) alerts2.Add(change);
        }

        if (current.GrossSales == 0 && returns == 0 && current.TotalAdSpend == 0 && costs == 0)
            alerts2.Add(new QualityAlert("all_zero", "warning",
                "Dört kaynak da 0 görünüyor.",
                "Bu ay için satış, iade, reklam ve gider bilgisi hiç girilmemiş ya da marka bu ay çalışmamış olabilir.",
                "Markanın bu ay çalışıp çalışmadığını teyit edin; çalışıyorsa kaynak raporları girin."));

        if (current.GrossSales > 0 && current.Orders == 0)
            alerts2.Add(new QualityAlert("missing_orders", "info",
                "Brüt satış girildi ama sipariş sayısı 0.",
                "Sipariş yoksa satış rakamı farklı bir kaynaktan gelmiş olabilir.",
                "Sipariş raporunu kontrol edin; satış rakamını doğrulayın."));
        if (current.GrossSales > 0 && current.TotalAdSpend == 0)
            alerts2.Add(new QualityAlert("missing_ads", "info",
                "Brüt satış var ama reklam harcaması 0.",
                "Reklam raporu girilmemişse harcama eksik görünür; reklam yoksa 0 doğru değerdir.",
                "Reklam raporunu kontrol edin; harcama yoksa bunu ekipte teyit edin."));
        if (current.GrossSales > 0 && costs == 0)
            alerts2.Add(new QualityAlert("missing_costs", "info",
                "Brüt satış var ama gider raporu 0.",
                "Ürün ve operasyon maliyeti girilmediyse kâr hesabını güvenilir değildir.",
                "Gider raporunu kontrol edin; maliyet yoksa bunu ekipte teyit edin."));

        return new BrandQuality(input.BrandId, input.BrandName, input.Deal?.Id, expectation, current.Id, current.Status,
            input.Origin, input.OriginDetail, input.Responsible, input.ResponsibleId, approval,
            Sources(current, "auto"), alerts2, alerts2.Count > 0 ? "attention" : "ready",
            input.TaskId, input.TaskCompleted);
    }

    private static string Expectation(Deal? deal, QualityPeriod? period)
    {
        if (deal is null) return "notExpected";
        if (deal.StartDate is null) return "unknownStart";
        return period is not null && PortfolioReporting.ExpectedInPeriod(deal, new ReportPeriod(period.Year, period.Month)) ? "expected" : "notApplicable";
    }

    private static string? OrNull(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static IReadOnlyList<QualitySource> Sources(MonthlyPerformance? p, string fallback)
    {
        if (p is null) return
        [
            Source("sales", "Satış raporu", fallback, 0, fallback == "missing" ? "Bu ay için kayıt yok; sıfır sayılmadı." : "Bu ay için kayıt beklenmiyor."),
            Source("returns", "İade raporu", fallback, 0, fallback == "missing" ? "Bu ay için kayıt yok; sıfır sayılmadı." : "Bu ay için kayıt beklenmiyor."),
            Source("ads", "Reklam raporu", fallback, 0, fallback == "missing" ? "Bu ay için kayıt yok; sıfır sayılmadı." : "Bu ay için kayıt beklenmiyor."),
            Source("costs", "Gider raporu", fallback, 0, fallback == "missing" ? "Bu ay için kayıt yok; sıfır sayılmadı." : "Bu ay için kayıt beklenmiyor.")
        ];
        return
        [
            Source("sales", "Satış raporu", p.GrossSales, "Brüt satış ve KDV bu kayıtta tutulur."),
            Source("returns", "İade raporu", Returns(p), "İade, iptal ve ters ibraz toplamı."),
            Source("ads", "Reklam raporu", p.TotalAdSpend, "Meta, Google, TikTok, influencer ve diğer reklam toplamı."),
            Source("costs", "Gider raporu", Costs(p), "Ürün maliyeti, ödeme, lojistik ve diğer değişken giderler.")
        ];
    }

    private static QualitySource Source(string key, string label, decimal amount, string note) =>
        new(key, label, amount > 0 ? "entered" : "zero", amount,
            amount > 0 ? note : "0 girildi. Veri olmadığı anlamına gelmez; kaynak belgesiyle kontrol edin.");

    private static QualitySource Source(string key, string label, string state, decimal amount, string note) => new(key, label, state, amount, note);

    private static IEnumerable<QualityAlert> Changes(MonthlyPerformance current, MonthlyPerformance previous, decimal returns, decimal costs)
    {
        foreach (var (key, label, now, before) in new[]
        {
            ("sales", "Satış raporu", current.GrossSales, previous.GrossSales),
            ("returns", "İade raporu", returns, Returns(previous)),
            ("ads", "Reklam raporu", current.TotalAdSpend, previous.TotalAdSpend),
            ("costs", "Gider raporu", costs, Costs(previous))
        })
        {
            if (before <= 0) continue;
            var delta = now - before;
            if (Math.Abs(delta) < MonthChangeFloor || Math.Abs(delta) / before < MonthChangeRatio) continue;
            yield return new QualityAlert($"change_{key}", "info",
                $"{label} önceki aya göre {(delta > 0 ? "arttı" : "azaldı")} {Math.Abs(delta / before).ToString("P0", Tr)}.",
                $"Önceki ay {before.ToString("N2", Tr)}, bu ay {now.ToString("N2", Tr)}. Fark tek başına hata değildir.",
                "Dönemin ve kaynak raporun doğru ayı içerdiğini karşılaştırın; hata yoksa farkı not edin.");
        }
    }
}
