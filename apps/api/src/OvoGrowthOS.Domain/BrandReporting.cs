using System.Globalization;

namespace OvoGrowthOS.Domain;

public sealed record BrandReportMetrics(Guid Id, int Year, int Month, MonthlyPerformanceStatus Status, decimal NetRevenue,
    decimal OvoFee, decimal AdSpend, decimal? Mer, decimal? RefundRate, decimal BrandContribution,
    decimal Paid, decimal Outstanding);
public sealed record ReportExplanation(string WhatHappened, string WhyItMatters, string NextStep);

public static class BrandReporting
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public static BrandReportMetrics Metrics(MonthlyPerformance p) => new(p.Id, p.Year, p.Month, p.Status, p.NetRevenue, p.OvoFee,
        p.TotalAdSpend, p.TotalAdSpend > 0 ? p.NetRevenue / p.TotalAdSpend : null,
        p.GrossSales > 0 ? p.Refunds / p.GrossSales : null, p.BrandContributionProfit,
        Collections.Balance(p, DateOnly.MinValue).Paid, Collections.Balance(p, DateOnly.MinValue).Outstanding);

    public static decimal? Change(decimal current, decimal? previous) => previous is > 0 ? (current - previous) / previous : null;

    public static IReadOnlyList<ReportExplanation> Explain(BrandReportMetrics? current, BrandReportMetrics? previous, bool changedDeal, bool previousIncluded = true)
    {
        if (current is null) return [new("Seçili ay ve kapsamda sonuç yok.", "Eksik veya henüz bu kapsama girmeyen veri sıfır gelir demek değildir.", "Aylık sonuç kaydını ve kapanış aşamasını kontrol edin.")];
        var result = new List<ReportExplanation>();
        if (!previousIncluded) result.Add(new("Bu rapor sürümü yalnız seçili ayı içerir.", "Önceki ay bu sürüme dahil edilmediği için büyüme veya düşüş karşılaştırması gösterilmez.", "Karşılaştırma için OVO ekibinden açıklama isteyin; paylaşılmayan veriyi sıfır kabul etmeyin."));
        else if (previous is null) result.Add(new("Önceki ayda aynı kapsama giren kayıt yok.", "Büyüme veya düşüş oranı güvenilir biçimde karşılaştırılamıyor.", "Önceki ay kaydını ve kapanış durumunu kontrol edin; eksik veriyi sıfır kabul etmeyin."));
        else if (Change(current.NetRevenue, previous.NetRevenue) is { } change)
            result.Add(new(change == 0 ? "Net ciro önceki ayla aynı." : $"Net ciro önceki aya göre {Math.Abs(change).ToString("P2", Tr)} {(change > 0 ? "arttı" : "azaldı")}.",
                "Ciro değişimi tek başına kârlılık veya tahsilat artışı demek değildir.", change < 0 ? "Sipariş, iade, stok ve kampanya sonuçlarını birlikte inceleyin." : "Büyümeyi katkı kârı ve reklam harcamasıyla birlikte değerlendirin."));
        else result.Add(new("Önceki ayın net cirosu sıfır veya negatif.", "Yüzdesel büyüme hesabı gösterilmiyor.", "Tutarları doğrudan karşılaştırın ve önceki ayın verisini kontrol edin."));
        if (changedDeal) result.Add(new("İki ay farklı anlaşmalara bağlı.", "Hakediş farkı satışın yanında ticari koşulların değişmesinden de kaynaklanabilir.", "İki dönemin anlaşma koşullarını ayrı ayrı inceleyin."));
        if (current.Mer is null) result.Add(new("Reklam harcaması sıfır; reklam verimliliği oranı hesaplanamıyor.", "Bu durum reklamın kusursuz verimli olduğunu göstermez; harcama bilgisi eksik olabilir.", "Reklam giderlerini ve reklam yapılmayan dönem olup olmadığını doğrulayın."));
        else if (previous?.Mer is { } oldMer && current.Mer < oldMer)
            result.Add(new($"Reklam verimliliği {oldMer.ToString("N2", Tr)}x seviyesinden {current.Mer.Value.ToString("N2", Tr)}x seviyesine geriledi.", "Her birim reklam giderine karşılık daha az net ciro kaydedilmiş. Bu oran tek başına kâr değildir.", "Kampanya, dönüşüm ve ürün katkı paylarını kontrol edin."));
        if (current.RefundRate is null) result.Add(new("İade oranı hesaplanamıyor.", "Brüt satış sıfır veya negatif olduğu için oran anlamlı değil.", "Brüt satış ve iade bilgilerini doğrulayın."));
        else if (previous?.RefundRate is { } oldRefund && current.RefundRate > oldRefund)
            result.Add(new($"İade oranı {oldRefund.ToString("P2", Tr)} düzeyinden {current.RefundRate.Value.ToString("P2", Tr)} düzeyine çıktı.", "İadelerin brüt satış içindeki payı artmış; nedenini yalnız bu rakam açıklamaz.", "Ürün, teslimat ve müşteri geri bildirimlerine göre iade nedenlerini inceleyin."));
        if (current.BrandContribution <= 0) result.Add(new("Markaya kalan katkı sıfır veya negatif.", "Kayıtlı satışlar, bu hesap kapsamındaki giderler ve OVO hakedişi sonrasında pozitif katkı bırakmıyor.", "Ürün maliyeti, reklam harcaması, iade ve ticari koşulları birlikte gözden geçirin."));
        if (current.Outstanding > 0) result.Add(new("Bu dönemin hakedişinde tahsil edilmemiş tutar var.", "Hakedişin oluşması paranın alındığı anlamına gelmez; kalan tutar tek başına gecikme kanıtı değildir.", "Fatura, vade ve ödeme kayıtlarını kontrol edin."));
        return result;
    }
}
