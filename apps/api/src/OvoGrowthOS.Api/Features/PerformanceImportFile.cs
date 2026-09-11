using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;

namespace OvoGrowthOS.Api.Features;

public sealed record ImportField(string Key, string Label);
public sealed record ImportFileRequest(string FileName, string ContentBase64, Dictionary<string, int>? Mapping = null, string? PreviewToken = null);
public sealed record ImportFileRow(int Line, string[] Values);
public sealed record ImportFileTable(string[] Headers, List<ImportFileRow> Rows);

// Only a bounded, values-only monthly input sheet is supported, not arbitrary Excel workbooks.
internal static class PerformanceImportFile
{
    public const int MaxBytes = 1_000_000;
    public static readonly ImportField[] Fields = [
        new("brandId", "Marka kodu"), new("dealId", "Anlaşma kodu"), new("period", "Dönem"), new("currency", "Para birimi"),
        new("grossSales", "Brüt satış"), new("vat", "KDV"), new("refunds", "İadeler"), new("cancellations", "İptaller"),
        new("chargebacks", "Ters ibrazlar"), new("customerPaidShipping", "Müşterinin ödediği kargo"), new("giftCardTopups", "Hediye kartı eklemeleri"),
        new("orders", "Sipariş sayısı"), new("sessions", "Site ziyareti"), new("newCustomers", "Yeni müşteri sayısı"), new("returningCustomers", "Tekrar alışveriş yapan müşteri"),
        new("cogs", "Ürün maliyeti"), new("paymentFees", "Ödeme kuruluşu kesintileri"), new("fulfillmentCosts", "Paketleme ve hazırlama gideri"),
        new("shippingSubsidy", "Kargo desteği"), new("otherVariableCosts", "Diğer değişken giderler"), new("metaSpend", "Meta reklam harcaması"),
        new("googleSpend", "Google reklam harcaması"), new("tikTokSpend", "TikTok reklam harcaması"), new("influencerSpend", "İçerik üreticisi harcaması"), new("otherAdSpend", "Diğer reklam harcaması")
    ];
    public static ImportFileTable Read(ImportFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 180 || request.FileName.IndexOfAny(['/', '\\', '\r', '\n']) >= 0)
            throw new InvalidDataException("Geçerli ve kısa bir dosya adı kullanın.");
        if (request.ContentBase64 is null || request.ContentBase64.Length > 1_333_336) throw new InvalidDataException("Dosya en fazla 1 MB olabilir.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.ContentBase64); }
        catch (FormatException) { throw new InvalidDataException("Dosya okunamadı. Dosyayı yeniden seçin."); }
        if (bytes.Length is 0 or > MaxBytes) throw new InvalidDataException("Boş dosya veya 1 MB'tan büyük dosya yüklenemez.");
        var rows = Path.GetExtension(request.FileName).ToLowerInvariant() switch
        {
            ".csv" => Csv(bytes), ".xlsx" => Xlsx(bytes),
            _ => throw new InvalidDataException("Yalnız .xlsx veya UTF-8 .csv dosyası yükleyin. Eski .xls dosyasını önce .xlsx olarak kaydedin.")
        };
        if (rows.Count < 2) throw new InvalidDataException("Başlık satırı ve en az bir veri satırı gereklidir.");
        var headers = rows[0].Values;
        if (headers.Length is < 4 or > 60 || headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new InvalidDataException("Başlıklar boş veya tekrar eden ad içeremez; en fazla 60 sütun kullanın.");
        if (rows.Skip(1).Any(x => x.Values.Length != headers.Length)) throw new InvalidDataException("Satırlardaki sütun sayıları başlıkla aynı olmalıdır.");
        return new(headers, rows.Skip(1).ToList());
    }
    private static List<ImportFileRow> Csv(byte[] bytes)
    {
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new InvalidDataException("CSV dosyasını UTF-8 biçiminde kaydedin."); }
        var header = text.Split('\n', 2)[0];
        var separator = new[] { ";", "\t", "," }.OrderByDescending(x => header.Count(c => c == x[0])).First();
        using var parser = new TextFieldParser(new StringReader(text)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(separator);
        List<ImportFileRow> rows = [];
        try
        {
            while (!parser.EndOfData)
            {
                var line = checked((int)parser.LineNumber); var values = parser.ReadFields()!;
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                if (values.Length > 60 || values.Any(x => x.Length > 200)) throw new InvalidDataException("En fazla 60 sütun ve hücre başına 200 karakter kullanın.");
                rows.Add(new(line, values));
                if (rows.Count > 201) throw new InvalidDataException("Bir dosyada en fazla 200 veri satırı aktarabilirsiniz.");
            }
        }
        catch (MalformedLineException) { throw new InvalidDataException("CSV tırnakları veya sütun ayırıcıları hatalı. Şablonu kullanarak yeniden kaydedin."); }
        return rows;
    }
    private static List<ImportFileRow> Xlsx(byte[] bytes)
    {
        ZipArchive archive;
        try { archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read); }
        catch (InvalidDataException) { throw new InvalidDataException("Excel dosyası bozuk veya .xlsx biçiminde değil. Dosyayı Excel'de yeniden kaydedin."); }
        try
        {
            using var zip = archive;
            if (zip.Entries.Count > 200 || zip.Entries.Sum(x => x.Length) > 8_000_000 || zip.Entries.Select(x => x.FullName).Distinct().Count() != zip.Entries.Count)
                throw new InvalidDataException("Excel dosyası çok karmaşık veya büyük. Verileri boş şablona yalnız değerler olarak yapıştırın.");
            XDocument Xml(string path)
            {
                using var stream = (zip.GetEntry(path) ?? throw new InvalidDataException("Excel dosyasının yapısı eksik.")).Open();
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
                return XDocument.Load(reader);
            }
            if (zip.Entries.Any(x => x.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase) || x.FullName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Dış bağlantı veya makro içeren dosyalar aktarılamaz.");
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var sheets = Xml("xl/workbook.xml").Descendants(ns + "sheet").ToList();
            var sheet = sheets.SingleOrDefault(x => (string?)x.Attribute("name") == "AylikVeri") ?? (sheets.Count == 1 ? sheets[0] : null);
            if (sheet is null) throw new InvalidDataException("Birden fazla sayfalı Excel dosyasında veri sayfasının adı AylikVeri olmalıdır.");
            var sheetRel = (Xml("xl/_rels/workbook.xml.rels").Root ?? throw new InvalidDataException("Excel dosyasının yapısı eksik.")).Elements().Single(x => (string?)x.Attribute("Id") == (string?)sheet.Attribute(rel + "id"));
            if ((string?)sheetRel.Attribute("TargetMode") == "External") throw new InvalidDataException("Dış bağlantılı sayfa aktarılamaz.");
            var target = (string?)sheetRel.Attribute("Target") ?? throw new InvalidDataException("Excel sayfa yolu eksik.");
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            if (path.Contains("..")) throw new InvalidDataException("Excel sayfa yolu geçersiz.");
            var strings = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToArray();
            var document = Xml(path);
            if (document.Descendants(ns + "f").Any() || document.Descendants(ns + "mergeCell").Any()) throw new InvalidDataException("Veri sayfasında formül veya birleştirilmiş hücre kullanmayın. Yalnız değerleri yapıştırın.");
            List<ImportFileRow> rows = []; int width = 0; var seenRows = new HashSet<int>();
            foreach (var row in document.Descendants(ns + "row"))
            {
                if (!int.TryParse((string?)row.Attribute("r"), out var line) || line < 1 || !seenRows.Add(line)) throw new InvalidDataException("Excel satır numaraları geçersiz veya tekrar ediyor.");
                var cells = row.Elements(ns + "c").ToList();
                if (cells.Count == 0) continue;
                var values = new string[60]; Array.Fill(values, ""); int last = 0; var seenColumns = new HashSet<int>();
                foreach (var cell in cells)
                {
                    var address = (string?)cell.Attribute("r") ?? "";
                    var match = Regex.Match(address, "^([A-Z]{1,2})([0-9]{1,7})$");
                    if (!match.Success) throw new InvalidDataException("Excel hücre adresi okunamadı.");
                    var col = match.Groups[1].Value.Aggregate(0, (v, c) => v * 26 + c - 'A' + 1);
                    if (int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) != line || !seenColumns.Add(col)) throw new InvalidDataException("Excel hücre adresleri satırla uyuşmuyor veya tekrar ediyor.");
                    var type = (string?)cell.Attribute("t"); var raw = cell.Element(ns + "v")?.Value ?? "";
                    var value = type switch { "s" => strings[int.Parse(raw, CultureInfo.InvariantCulture)], "inlineStr" => string.Concat(cell.Descendants(ns + "t").Select(x => x.Value)), null or "n" or "str" or "d" => raw, _ => throw new InvalidDataException("Excel veri sayfasında hata veya mantıksal değer var.") };
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    if (col > 60 || value.Length > 200) throw new InvalidDataException("En fazla 60 sütun ve hücre başına 200 karakter kullanın.");
                    if (type is null or "n") value = value.Replace('.', ','); // OOXML numbers use invariant decimal separators; never convert through double.
                    values[col - 1] = value.Trim(); last = Math.Max(last, col);
                }
                if (last == 0) continue;
                if (rows.Count == 0) width = last;
                if (last > width) throw new InvalidDataException("Başlıksız veri sütunu var.");
                rows.Add(new(line, values[..width]));
                if (rows.Count > 201) throw new InvalidDataException("Bir dosyada en fazla 200 veri satırı aktarabilirsiniz.");
            }
            return rows;
        }
        catch (Exception ex) when (ex is XmlException or FormatException or IndexOutOfRangeException or InvalidOperationException or OverflowException)
        { throw new InvalidDataException("Excel dosyası okunamadı. Verileri boş şablona yalnız değerler olarak yapıştırın."); }
    }
    public static decimal Number(string text)
    {
        if (!Regex.IsMatch(text, @"^(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]{1,4})?$", RegexOptions.CultureInvariant) ||
            !decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.GetCultureInfo("tr-TR"), out var value) || value > 1_000_000_000_000m)
            throw new InvalidDataException("Sıfır veya pozitif tutar girin; örnek 1234,56 veya 1.234,56. En fazla dört ondalık basamak ve 1 trilyon sınırı vardır.");
        return value;
    }
}
