using System.Globalization;
using OvoGrowthOS.Domain;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace OvoGrowthOS.Api.Features;

// Resolves one embedded family for every report page: the container image has no system fonts.
internal sealed class OvoFontResolver : IFontResolver
{
    public const string Family = "Ovo Sans";
    internal const string RegularFace = "OvoSans-Regular";
    internal const string BoldFace = "OvoSans-Bold";

    private static readonly byte[] Regular = Read("NotoSans-Regular.ttf");
    private static readonly byte[] Bold = Read("NotoSans-Bold.ttf");

    private static byte[] Read(string fileName)
    {
        using var stream = typeof(OvoFontResolver).Assembly.GetManifestResourceStream($"OvoGrowthOS.Api.Fonts.{fileName}")
            ?? throw new InvalidOperationException("Gömülü yazı tipi bulunamadı.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(string.Equals(familyName, Family, StringComparison.OrdinalIgnoreCase) && isBold ? BoldFace : RegularFace);

    public byte[] GetFont(string faceName) => faceName == BoldFace ? Bold : Regular;
}

// Builds the customer facing PDF of a published portal report. Only snapshot values are used, so the
// file can never contain internal costs, internal notes or anything that is not already on the portal.
internal static class PortalReportPdf
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private const double MarginX = 48;
    private const double Top = 52;
    private const double Bottom = 56;

    static PortalReportPdf() => GlobalFontSettings.FontResolver ??= new OvoFontResolver();

    internal static byte[] Build(PortalReportSnapshot snapshot, int year, int month)
    {
        var document = new PdfSharp.Pdf.PdfDocument();
        document.Info.Title = $"{snapshot.BrandName} · {month:00}/{year} raporu";
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var width = page.Width.Point - MarginX * 2;
        var height = page.Height.Point;
        var gfx = XGraphics.FromPdfPage(page);
        double y = Top;

        var title = new XFont(OvoFontResolver.Family, 20, XFontStyleEx.Bold);
        var brand = new XFont(OvoFontResolver.Family, 14, XFontStyleEx.Bold);
        var heading = new XFont(OvoFontResolver.Family, 12, XFontStyleEx.Bold);
        var body = new XFont(OvoFontResolver.Family, 10.5, XFontStyleEx.Regular);
        var small = new XFont(OvoFontResolver.Family, 9, XFontStyleEx.Regular);
        var ink = XBrushes.Black;
        var muted = new XSolidBrush(XColors.Gray);
        var rule = new XPen(XColors.Black, 0.75);

        void NewPage()
        {
            gfx.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            height = page.Height.Point;
            gfx = XGraphics.FromPdfPage(page);
            y = Top;
        }

        void Ensure(double needed)
        {
            if (y + needed > height - Bottom) NewPage();
        }

        void Lines(string text, XFont font, XBrush brush, double lineHeight, double indent = 0)
        {
            foreach (var line in Wrap(text, font, width - indent))
            {
                Ensure(lineHeight);
                gfx.DrawString(line, font, brush, MarginX + indent, y, XStringFormats.TopLeft);
                y += lineHeight;
            }
        }

        List<string> Wrap(string text, XFont font, double maxWidth)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = "";
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length == 0 || gfx.MeasureString(candidate, font).Width <= maxWidth) current = candidate;
                else { lines.Add(current); current = word; }
            }
            if (current.Length > 0) lines.Add(current);
            return lines.Count == 0 ? [""] : lines;
        }

        void Heading(string text)
        {
            Ensure(30);
            y += 8;
            Lines(text, heading, ink, 16);
            y += 2;
            Ensure(6);
            gfx.DrawLine(rule, MarginX, y, MarginX + width, y);
            y += 8;
        }

        void Row(string label, string value)
        {
            Ensure(16);
            gfx.DrawString(label, body, muted, MarginX, y, XStringFormats.TopLeft);
            var valueWidth = gfx.MeasureString(value, body).Width;
            gfx.DrawString(value, body, ink, MarginX + width - valueWidth, y, XStringFormats.TopLeft);
            y += 15;
        }

        gfx.DrawString("Aylık performans raporu", title, ink, MarginX, y, XStringFormats.TopLeft);
        y += 28;
        gfx.DrawString(snapshot.BrandName, brand, ink, MarginX, y, XStringFormats.TopLeft);
        y += 20;
        Lines($"Dönem: {month:00}/{year} · Para birimi: {snapshot.Currency}", body, ink, 15);
        Lines($"Sürüm {snapshot.Version} · Yayımlanma: {snapshot.PublishedAt.ToString("dd.MM.yyyy HH:mm", Tr)}", small, muted, 13);
        y += 4;
        gfx.DrawLine(rule, MarginX, y, MarginX + width, y);
        y += 12;

        var m = snapshot.Metrics;
        Heading("Dönem özeti");
        Row("Dönem durumu", WorkflowEndpoints.BrandReportStatus(m));
        Row("Net ciro", Money(m?.NetRevenue, snapshot.Currency));
        Row("OVO hakedişi", Money(m?.OvoFee, snapshot.Currency));
        Row("Reklam gideri", Money(m?.AdSpend, snapshot.Currency));
        Row("Reklam verimliliği (MER)", m?.Mer is { } mer ? $"{mer.ToString("N2", Tr)}x" : "Hesaplanamıyor");
        Row("İade oranı", m?.RefundRate is { } rate ? rate.ToString("P2", Tr) : "Hesaplanamıyor");
        Row("Markaya kalan katkı", Money(m?.BrandContribution, snapshot.Currency));
        Row("Tahsil edilen tutar", Money(m?.Paid, snapshot.Currency));
        Row("Kalan alacak", Money(m?.Outstanding, snapshot.Currency));

        Heading("Açıklamalar");
        if (snapshot.Explanations.Count == 0)
            Lines("Bu sürüm için açıklama üretilmedi.", body, muted, 14);
        foreach (var item in snapshot.Explanations)
        {
            Lines(item.WhatHappened, body, ink, 14);
            Lines($"Neden önemli: {item.WhyItMatters}", small, muted, 13, indent: 10);
            Lines($"Sonraki adım: {item.NextStep}", small, muted, 13, indent: 10);
            y += 6;
        }

        Heading("Bu belge hakkında");
        Lines("Tutarlar KDV hariçtir. Katkı, vergi sonrası net kâr değildir. Eksik veri sıfır kabul edilmez.", small, muted, 13);
        Lines($"Bu belge, {month:00}/{year} dönemine ait {snapshot.Version}. sürümde müşteri portalında yayımlanan raporun verileriyle üretilmiştir. OVO’nun iç maliyet ve kâr bilgileri ile iç notlar içermez.", small, muted, 13);
        Lines($"Oluşturma: {DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm", Tr)}", small, muted, 13);

        gfx.Dispose();
        var total = document.PageCount;
        for (var index = 0; index < total; index++)
        {
            using var footer = XGraphics.FromPdfPage(document.Pages[index]);
            var label = $"{index + 1} / {total}";
            var labelWidth = footer.MeasureString(label, small).Width;
            footer.DrawString(label, small, muted, document.Pages[index].Width.Point - MarginX - labelWidth,
                document.Pages[index].Height.Point - 26, XStringFormats.TopLeft);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static string Money(decimal? value, string currency) => value is { } number
        ? $"{number.ToString("0.##", Tr)} {currency}"
        : "Veri yok";
}
