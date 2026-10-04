using System.Globalization;
using System.Text.Json;
using OvoGrowthOS.Domain;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace OvoGrowthOS.Api.Features;

// Internal staff printouts reusing the embedded fonts: an agreement summary and
// a commission statement. Both render only data the matching screen already shows
// to the same role, so the files introduce no new visibility.
internal static class DealStatementPdf
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private const double MarginX = 48;
    private const double Top = 52;
    private const double Bottom = 56;

    static DealStatementPdf() => GlobalFontSettings.FontResolver ??= new OvoFontResolver();

    private sealed class Page
    {
        public PdfSharp.Pdf.PdfDocument Document = new();
        public PdfSharp.Pdf.PdfPage Sheet;
        public XGraphics Gfx;
        public double Y = Top;
        public double Width;
        public double Height;
        public readonly XFont Title = new(OvoFontResolver.Family, 20, XFontStyleEx.Bold);
        public readonly XFont Brand = new(OvoFontResolver.Family, 14, XFontStyleEx.Bold);
        public readonly XFont HeadingFont = new(OvoFontResolver.Family, 12, XFontStyleEx.Bold);
        public readonly XFont Body = new(OvoFontResolver.Family, 10.5, XFontStyleEx.Regular);
        public readonly XFont Small = new(OvoFontResolver.Family, 9, XFontStyleEx.Regular);
        public readonly XPen Rule = new(XColors.Black, 0.75);

        public Page(string title)
        {
            Document.Info.Title = title;
            Sheet = Document.AddPage();
            Sheet.Size = PdfSharp.PageSize.A4;
            Width = Sheet.Width.Point - MarginX * 2;
            Height = Sheet.Height.Point;
            Gfx = XGraphics.FromPdfPage(Sheet);
        }

        public void NewSheet()
        {
            Gfx.Dispose();
            Sheet = Document.AddPage();
            Sheet.Size = PdfSharp.PageSize.A4;
            Height = Sheet.Height.Point;
            Gfx = XGraphics.FromPdfPage(Sheet);
            Y = Top;
        }

        public void Ensure(double needed)
        {
            if (Y + needed > Height - Bottom) NewSheet();
        }

        public List<string> Wrap(string text, XFont font, double maxWidth)
        {
            var lines = new List<string>();
            var current = "";
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length == 0 || Gfx.MeasureString(candidate, font).Width <= maxWidth) current = candidate;
                else { lines.Add(current); current = word; }
            }
            if (current.Length > 0) lines.Add(current);
            return lines.Count == 0 ? [""] : lines;
        }

        public void Lines(string text, XFont font, XBrush brush, double lineHeight, double indent = 0)
        {
            foreach (var line in Wrap(text, font, Width - indent))
            {
                Ensure(lineHeight);
                Gfx.DrawString(line, font, brush, MarginX + indent, Y, XStringFormats.TopLeft);
                Y += lineHeight;
            }
        }

        public void Heading(string text)
        {
            Ensure(30);
            Y += 8;
            Lines(text, HeadingFont, XBrushes.Black, 16);
            Y += 2;
            Ensure(6);
            Gfx.DrawLine(Rule, MarginX, Y, MarginX + Width, Y);
            Y += 8;
        }

        public void Row(string label, string value)
        {
            Ensure(16);
            Gfx.DrawString(label, Body, new XSolidBrush(XColors.Gray), MarginX, Y, XStringFormats.TopLeft);
            var valueWidth = Gfx.MeasureString(value, Body).Width;
            Gfx.DrawString(value, Body, XBrushes.Black, MarginX + Width - valueWidth, Y, XStringFormats.TopLeft);
            Y += 15;
        }

        public void Divider()
        {
            Ensure(14);
            Gfx.DrawLine(Rule, MarginX, Y, MarginX + Width, Y);
            Y += 12;
        }

        public byte[] Save()
        {
            Gfx.Dispose();
            var total = Document.PageCount;
            var small = Small;
            for (var index = 0; index < total; index++)
            {
                using var footer = XGraphics.FromPdfPage(Document.Pages[index]);
                var label = $"{index + 1} / {total}";
                var labelWidth = footer.MeasureString(label, small).Width;
                footer.DrawString(label, small, new XSolidBrush(XColors.Gray),
                    Document.Pages[index].Width.Point - MarginX - labelWidth,
                    Document.Pages[index].Height.Point - 26, XStringFormats.TopLeft);
            }
            using var stream = new MemoryStream();
            Document.Save(stream, false);
            return stream.ToArray();
        }
    }

    private static string Money(decimal value, string currency) => $"{value.ToString("0.##", Tr)} {currency}";
    private static string Rate(decimal fraction) => $"%{(fraction * 100).ToString("0.##", Tr)}";
    private static string Day(DateOnly? day) => day?.ToString("dd.MM.yyyy", Tr) ?? "Belirtilmemiş";

    private static string DealTypeTr(DealType type) => type switch
    {
        DealType.FlatRevenueShare => "Sabit ciro payı",
        DealType.TieredRevenueShare => "Kademeli ciro payı",
        DealType.RetainerPlusRevenueShare => "Aylık hizmet bedeli + ciro payı",
        DealType.MinimumFeePlusRevenueShare => "Asgari ücret + ciro payı",
        DealType.IncrementalRevenueShare => "Büyüme farkı üzerinden pay",
        DealType.RetainerPlusIncrementalRevenueShare => "Aylık hizmet bedeli + büyüme farkı payı",
        DealType.ContributionProfitShare => "Katkı kârı paylaşımı",
        _ => "Sabit aylık hizmet bedeli",
    };

    private static string DealStatusTr(DealStatus status) => status switch
    {
        DealStatus.Draft => "Taslak",
        DealStatus.InternalReview => "İç incelemede",
        DealStatus.Proposed => "Önerildi",
        DealStatus.Negotiation => "Görüşmede",
        DealStatus.Accepted => "Kabul edildi",
        DealStatus.Rejected => "Reddedildi",
        DealStatus.Active => "Etkin",
        DealStatus.Expired => "Süresi doldu",
        _ => "Sonlandırıldı",
    };

    private static string ConditionStatusTr(ConditionStatus status) => status switch
    {
        ConditionStatus.Satisfied => "Tamamlandı",
        ConditionStatus.Waived => "Feragat edildi",
        _ => "Bekliyor",
    };

    private static string PeriodStatusTr(MonthlyPerformanceStatus status) => status switch
    {
        MonthlyPerformanceStatus.Draft => "Taslak",
        MonthlyPerformanceStatus.UnderReview => "İncelemede",
        MonthlyPerformanceStatus.Approved => "Onaylandı",
        MonthlyPerformanceStatus.Locked => "Kilitlendi",
        MonthlyPerformanceStatus.Invoiced => "Faturalandı",
        _ => "Ödendi",
    };

    internal static byte[] BuildDeal(Deal deal, string brandName)
    {
        var page = new Page($"{brandName} · anlaşma özeti");
        page.Gfx.DrawString("Anlaşma özeti", page.Title, XBrushes.Black, MarginX, page.Y, XStringFormats.TopLeft);
        page.Y += 28;
        page.Gfx.DrawString(brandName, page.Brand, XBrushes.Black, MarginX, page.Y, XStringFormats.TopLeft);
        page.Y += 20;
        page.Lines($"Anlaşma: {deal.Name} · Para birimi: {deal.Currency}", page.Body, XBrushes.Black, 15);
        page.Lines($"Durum: {DealStatusTr(deal.Status)} · Model: {DealTypeTr(deal.DealType)}", page.Body, XBrushes.Black, 15);
        page.Y += 4;
        page.Divider();

        page.Heading("Ticari koşullar");
        page.Row("Sözleşme süresi", $"{deal.ContractMonths} ay");
        page.Row("Başlangıç / bitiş", $"{Day(deal.StartDate)} / {Day(deal.EndDate)}");
        if (deal.MonthlyRetainer > 0) page.Row("Aylık hizmet bedeli", Money(deal.MonthlyRetainer, deal.Currency));
        if (deal.MinimumMonthlyFee > 0) page.Row("Asgari aylık ücret", Money(deal.MinimumMonthlyFee, deal.Currency));
        if (deal.RevenueShareRate > 0) page.Row("Ciro payı", Rate(deal.RevenueShareRate));
        if (deal.BaselineRevenue > 0) page.Row("Başlangıç cirosu", Money(deal.BaselineRevenue, deal.Currency));
        if (deal.IncrementalRate > 0) page.Row("Büyüme farkı payı", Rate(deal.IncrementalRate));
        if (deal.ProfitShareRate > 0) page.Row("Kâr paylaşım oranı", Rate(deal.ProfitShareRate));
        if (deal.DealType == DealType.TieredRevenueShare) page.Row("Kademeli dilimler", TierCount(deal.CommissionTiersJson));
        if (deal.SetupInvestment > 0) page.Row("Kurulum yatırımı", Money(deal.SetupInvestment, deal.Currency));
        if (deal.EstimatedMonthlyInternalCost > 0) page.Row("Tahmini aylık iç maliyet", Money(deal.EstimatedMonthlyInternalCost, deal.Currency));
        if (!string.IsNullOrWhiteSpace(deal.StatusReason)) page.Row("Durum notu", deal.StatusReason.Trim());

        page.Heading("Koşullar");
        if (deal.Conditions.Count == 0)
            page.Lines("Bu anlaşmaya bağlı koşul kaydedilmedi.", page.Body, new XSolidBrush(XColors.Gray), 14);
        foreach (var condition in deal.Conditions.OrderByDescending(x => x.Required).ThenBy(x => x.Title))
        {
            page.Lines($"{(condition.Required ? "Zorunlu" : "İsteğe bağlı")} · {condition.Title} — {ConditionStatusTr(condition.Status)}", page.Body, XBrushes.Black, 14);
            if (!string.IsNullOrWhiteSpace(condition.ResolutionReason))
                page.Lines($"Gerekçe: {condition.ResolutionReason.Trim()}", page.Small, new XSolidBrush(XColors.Gray), 13, indent: 10);
            page.Y += 4;
        }

        page.Heading("Bu belge hakkında");
        page.Lines("Tutarlar KDV hariçtir. Bu belge, çıktının alındığı andaki anlaşma kaydının özetidir; imzalı sözleşme yerine geçmez.", page.Small, new XSolidBrush(XColors.Gray), 13);
        page.Lines($"Oluşturma: {DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm", Tr)}", page.Small, new XSolidBrush(XColors.Gray), 13);
        return page.Save();
    }

    private static string TierCount(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? $"{doc.RootElement.GetArrayLength()} dilim (ayrıntı anlaşma kaydında)"
                : "Ayrıntı anlaşma kaydında";
        }
        catch (JsonException) { return "Ayrıntı anlaşma kaydında"; }
    }

    internal static byte[] BuildStatement(MonthlyPerformance p, string brandName, CollectionBalance balance, IReadOnlyList<CollectionPayment> payments)
    {
        var currency = p.Collection?.Currency ?? p.Deal?.Currency ?? "TRY";
        var page = new Page($"{brandName} · {p.Month:00}/{p.Year} hakediş dökümü");
        page.Gfx.DrawString("Hakediş dökümü", page.Title, XBrushes.Black, MarginX, page.Y, XStringFormats.TopLeft);
        page.Y += 28;
        page.Gfx.DrawString(brandName, page.Brand, XBrushes.Black, MarginX, page.Y, XStringFormats.TopLeft);
        page.Y += 20;
        page.Lines($"Dönem: {p.Month:00}/{p.Year} · Para birimi: {currency} · Durum: {PeriodStatusTr(p.Status)}", page.Body, XBrushes.Black, 15);
        page.Lines($"Anlaşma: {p.Deal?.Name ?? "—"}", page.Body, XBrushes.Black, 15);
        page.Y += 4;
        page.Divider();

        page.Heading("Dönem sonucu");
        page.Row("Net ciro", Money(p.NetRevenue, currency));
        page.Row("OVO hakedişi", Money(p.OvoFee, currency));
        page.Row("Reklam gideri", Money(p.TotalAdSpend, currency));
        page.Row("Markaya kalan katkı", Money(p.BrandContributionProfit, currency));

        page.Heading("Fatura ve tahsilat");
        page.Row("Fatura referansı", string.IsNullOrWhiteSpace(p.Collection?.InvoiceReference) ? "Kayıtlı değil" : p.Collection.InvoiceReference);
        page.Row("Fatura / vade", $"{Day(p.Collection?.InvoiceOn)} / {Day(p.Collection?.DueOn)}");
        page.Row("Alacak", Money(balance.Receivable, currency));
        page.Row("Ödenen", Money(balance.Paid, currency));
        page.Row("Kalan alacak", Money(balance.Outstanding, currency));

        page.Heading("Gerçekleşmiş ödemeler");
        var active = payments.Where(x => x.VoidedAt is null).OrderBy(x => x.PaidOn).ThenBy(x => x.CreatedAt).ToList();
        var voided = payments.Count - active.Count;
        if (active.Count == 0)
            page.Lines("Bu döneme kayıtlı ödeme yok.", page.Body, new XSolidBrush(XColors.Gray), 14);
        foreach (var payment in active)
        {
            page.Lines($"{payment.PaidOn.ToString("dd.MM.yyyy", Tr)} · {Money(payment.Amount, currency)} · {payment.Reference}", page.Body, XBrushes.Black, 14);
            if (!string.IsNullOrWhiteSpace(payment.Note))
                page.Lines(payment.Note.Trim(), page.Small, new XSolidBrush(XColors.Gray), 13, indent: 10);
        }
        if (voided > 0)
            page.Lines($"İptal edilmiş {voided} kayıt tabloya dahil edilmedi; ayrıntı hakediş ekranındadır.", page.Small, new XSolidBrush(XColors.Gray), 13);

        page.Heading("Bu belge hakkında");
        page.Lines("Tutarlar KDV hariçtir; banka bakiyesi veya vergi sonrası net kâr değildir.", page.Small, new XSolidBrush(XColors.Gray), 13);
        page.Lines($"Bu belge, {p.Month:00}/{p.Year} dönemine ait hakediş kaydının çıktının alındığı andaki durumudur.", page.Small, new XSolidBrush(XColors.Gray), 13);
        page.Lines($"Oluşturma: {DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm", Tr)}", page.Small, new XSolidBrush(XColors.Gray), 13);
        return page.Save();
    }

    internal static byte[] BuildBrandReport(BrandReportDocument doc)
    {
        var internalView = doc.Audience == "internal";
        var page = new Page($"{doc.BrandName} · {doc.Month:00}/{doc.Year} marka raporu");
        page.Gfx.DrawString("Açıklamalı marka raporu", page.Title, XBrushes.Black, MarginX, page.Y, XStringFormats.TopLeft);
        page.Y += 28;
        page.Gfx.DrawString(doc.BrandName, page.Brand, XBrushes.Black, MarginX, page.Y, XStringFormats.TopLeft);
        page.Y += 20;
        page.Lines($"Dönem: {doc.Month:00}/{doc.Year} · Para birimi: {doc.Currency}", page.Body, XBrushes.Black, 15);
        page.Lines($"Kapsam: {ScopeTr(doc.Scope)} · Görünüm: {(internalView ? "OVO iç yönetim - paylaşmayın" : "Markayla paylaşılabilir")}", page.Body, XBrushes.Black, 15);
        page.Y += 4;
        page.Divider();

        foreach (var (label, metrics) in new[] { ("Seçili ay", doc.Current), ("Önceki ay", doc.Previous) })
        {
            page.Heading(label);
            if (metrics is null)
            {
                page.Lines("Bu dönem için veri yok. Rakamlar sıfır kabul edilmedi.", page.Body, new XSolidBrush(XColors.Gray), 14);
                continue;
            }
            page.Row("Dönem durumu", WorkflowEndpoints.BrandReportStatus(metrics));
            page.Row("Net ciro", Money(metrics.NetRevenue, doc.Currency));
            page.Row("OVO hakedişi", Money(metrics.OvoFee, doc.Currency));
            page.Row("Reklam gideri", Money(metrics.AdSpend, doc.Currency));
            page.Row("Reklam verimliliği (MER)", metrics.Mer is { } mer ? $"{mer.ToString("N2", Tr)}x" : "Hesaplanamıyor");
            page.Row("İade oranı", metrics.RefundRate is { } rate ? rate.ToString("P2", Tr) : "Hesaplanamıyor");
            page.Row("Markaya kalan katkı", Money(metrics.BrandContribution, doc.Currency));
            page.Row("Ödenen", Money(metrics.Paid, doc.Currency));
            page.Row("Kalan alacak", Money(metrics.Outstanding, doc.Currency));
        }

        page.Heading("Markanın durumu ve sonraki adımlar");
        if (doc.Explanations.Count == 0)
            page.Lines("Bu dönem için açıklama üretilmedi.", page.Body, new XSolidBrush(XColors.Gray), 14);
        foreach (var item in doc.Explanations)
        {
            page.Lines(item.WhatHappened, page.Body, XBrushes.Black, 14);
            page.Lines($"Neden dikkat gerekiyor: {item.WhyItMatters}", page.Small, new XSolidBrush(XColors.Gray), 13, indent: 10);
            page.Lines($"Sonraki adım: {item.NextStep}", page.Small, new XSolidBrush(XColors.Gray), 13, indent: 10);
            page.Y += 6;
        }

        if (doc.Internal is { } inner && internalView)
        {
            page.Heading("Yalnız OVO iç yönetimi");
            page.Row("Planlanan hizmet maliyeti", Money(inner.Costs.PlannedCost, doc.Currency));
            page.Row("Girilen gerçek hizmet maliyeti", Money(inner.Costs.RecordedCost, doc.Currency));
            page.Row("Maliyet kontrolü", inner.Costs.Complete ? "Tamamlandı" : "Tamamlanmadı");
            page.Row("Gerçek gider sonrası katkı", inner.Costs.ContributionAfterRecordedCosts is { } c ? Money(c, doc.Currency) : "Kontrol tamamlanmadı; gösterilmiyor");
            page.Row("Portföyde hakediş payı", inner.PortfolioFeeShare.ToString("P2", Tr));
            page.Row("Kayıtlı yatırım harcaması", Money(inner.Investment.RecordedInvestment, doc.Currency));
            page.Row("Kayıtlı geri kazanım", Money(inner.Investment.RecordedRecovery, doc.Currency));
            page.Row("Kalan kayıtlı yatırım", Money(inner.Investment.Remaining, doc.Currency));
        }

        page.Heading("Bu belge hakkında");
        page.Lines("Tutarlar KDV hariçtir. Katkı, vergi sonrası net kâr değildir. Eksik veri sıfır kabul edilmez.", page.Small, new XSolidBrush(XColors.Gray), 13);
        page.Lines("Bu açıklamalar kayıtlı verilere dayalı sabit kurallarla hazırlanır; gelecek performansı garanti etmez.", page.Small, new XSolidBrush(XColors.Gray), 13);
        page.Lines($"Oluşturma: {DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm", Tr)}", page.Small, new XSolidBrush(XColors.Gray), 13);
        return page.Save();
    }

    private static string ScopeTr(string scope) => scope switch
    {
        "Closed" => "Kapanmış dönemler",
        "Approved" => "Onaylı, kilit bekleyen",
        "Preparation" => "Hazırlık ve kontrol",
        _ => "Tüm kayıtlar",
    };
}
