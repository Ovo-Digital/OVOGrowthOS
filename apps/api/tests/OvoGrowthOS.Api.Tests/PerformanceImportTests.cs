using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PerformanceImportTests
{
    private static Dictionary<string, int> Mapping => PerformanceImportFile.Fields.Select((f, i) => (f.Key, i)).ToDictionary(x => x.Key, x => x.i);
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin") { var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role)); return c; }
    private static async Task<Deal> Seed(WorkflowApiFactory f)
    {
        var id = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var d = new Deal { BrandId = id, Name = "Aktarım anlaşması", Currency = "TRY", Status = DealStatus.Active, DealType = DealType.FlatRevenueShare, RevenueShareRate = .1m, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) };
        db.Add(d); await db.SaveChangesAsync(); return d;
    }
    private static string[] Row(Deal d, string period = "08.2026", string gross = "1.200.000,1256")
    {
        var row = Enumerable.Repeat("0", PerformanceImportFile.Fields.Length).ToArray();
        row[0] = d.BrandId.ToString(); row[1] = d.Id.ToString(); row[2] = period; row[3] = d.Currency; row[4] = gross; row[5] = "200000";
        row[11] = "100"; row[12] = "1000"; row[13] = "70"; row[14] = "30"; row[15] = "300000"; row[20] = "100000"; return row;
    }
    private static ImportFileRequest File(params string[][] rows)
    {
        var csv = string.Join(';', PerformanceImportFile.Fields.Select(x => x.Label)) + "\r\n" + string.Join("\r\n", rows.Select(x => string.Join(';', x)));
        return new("aylik.csv", Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)), Mapping);
    }
    private static async Task<JsonElement> Preview(HttpClient c, ImportFileRequest file)
    {
        var response = await c.PostAsJsonAsync("/api/performance-imports/preview", file); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task Preview_is_read_only_commit_creates_drafts_and_audit_and_replay_creates_nothing()
    {
        await using var f = new WorkflowApiFactory(); var d = await Seed(f); using var c = Client(f); var file = File(Row(d));
        var preview = await Preview(c, file); Assert.True(preview.GetProperty("canCommit").GetBoolean());
        Assert.Equal(1000000.1256m, preview.GetProperty("totals")[0].GetProperty("netRevenue").GetDecimal());
        Assert.Equal(100000.0126m, preview.GetProperty("totals")[0].GetProperty("ovoFee").GetDecimal());
        using (var scope = f.Services.CreateScope()) Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().MonthlyPerformances.CountAsync());
        var request = file with { PreviewToken = preview.GetProperty("previewToken").GetString() };
        var response = await c.PostAsJsonAsync("/api/performance-imports/commit", request); response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(preview.GetProperty("totals").ToString(), saved.GetProperty("totals").ToString());
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.MonthlyPerformances.SingleAsync();
            Assert.Equal(MonthlyPerformanceStatus.Draft, p.Status); Assert.Equal("admin@ovo.test", p.PreparedBy); Assert.Equal(1000000.1256m, p.NetRevenue);
            Assert.Single(await db.AuditRecords.Where(x => x.Action == "MonthlyPerformanceImported").ToListAsync());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/performance-imports/commit", request)).StatusCode);
        Assert.False((await Preview(c, file with { FileName = "yeniden.csv" })).GetProperty("canCommit").GetBoolean());
        var history = await c.GetFromJsonAsync<JsonElement>("/api/performance-imports/history"); Assert.Equal(1, history.GetArrayLength());
    }
    [Fact]
    public async Task One_bad_row_blocks_entire_file_without_partial_totals_or_records()
    {
        await using var f = new WorkflowApiFactory(); var d = await Seed(f); using var c = Client(f);
        var bad = Row(d, "09.2026"); bad[5] = ""; bad[0] = Guid.NewGuid().ToString();
        var file = File(Row(d), bad); var preview = await Preview(c, file);
        Assert.False(preview.GetProperty("canCommit").GetBoolean()); Assert.Equal(0, preview.GetProperty("totals").GetArrayLength());
        Assert.Contains("aynı markaya ait değil", preview.GetProperty("rows")[1].GetProperty("errors").ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/performance-imports/commit", file)).StatusCode);
        using var scope = f.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().MonthlyPerformances.ToListAsync());
    }
    [Fact]
    public async Task Approval_is_bound_to_actor_file_mapping_and_current_deal()
    {
        await using var f = new WorkflowApiFactory(); var d = await Seed(f); using var c = Client(f); using var partner = Client(f, "Partner"); var file = File(Row(d));
        var token = (await Preview(c, file)).GetProperty("previewToken").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await partner.PostAsJsonAsync("/api/performance-imports/commit", file with { PreviewToken = token })).StatusCode);
        var changed = File(Row(d, gross: "1400000")) with { PreviewToken = token };
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/performance-imports/commit", changed)).StatusCode);
        var mapping = Mapping; (mapping["metaSpend"], mapping["googleSpend"]) = (mapping["googleSpend"], mapping["metaSpend"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/performance-imports/commit", file with { Mapping = mapping, PreviewToken = token })).StatusCode);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.Deals.FindAsync(d.Id))!.Status = DealStatus.Terminated; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/performance-imports/commit", file with { PreviewToken = token })).StatusCode);
    }
    [Fact]
    public async Task Existing_locked_period_duplicate_rows_currency_and_dates_are_rejected()
    {
        await using var f = new WorkflowApiFactory(); var d = await Seed(f); using var c = Client(f);
        var duplicate = await Preview(c, File(Row(d), Row(d))); Assert.False(duplicate.GetProperty("canCommit").GetBoolean());
        var wrong = Row(d, "02.08.2026"); wrong[3] = "USD";
        var invalid = await Preview(c, File(wrong)); Assert.False(invalid.GetProperty("canCommit").GetBoolean());
        Assert.False((await Preview(c, File(Row(d, "2025-12")))).GetProperty("canCommit").GetBoolean());
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Add(new MonthlyPerformance { BrandId = d.BrandId, DealId = d.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Locked, NetRevenue = 123 }); await db.SaveChangesAsync(); }
        var existing = await Preview(c, File(Row(d))); Assert.False(existing.GetProperty("canCommit").GetBoolean());
        using var check = f.Services.CreateScope(); Assert.Equal(123, (await check.ServiceProvider.GetRequiredService<AppDbContext>().MonthlyPerformances.SingleAsync()).NetRevenue);
    }
    [Theory]
    [InlineData("1.234,5678", "1234.5678")]
    [InlineData("0", "0")]
    [InlineData("1234,50", "1234.50")]
    public void Turkish_numbers_preserve_decimal_precision(string text, string expected) => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), PerformanceImportFile.Number(text));
    [Theory]
    [InlineData("")][InlineData("-1")][InlineData("1.23")][InlineData("1,234.50")][InlineData("12,12345")][InlineData("=SUM(1)")][InlineData("NaN")][InlineData("1000000000001")]
    public void Invalid_or_ambiguous_numbers_are_not_silently_coerced(string text) => Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Number(text));
    [Fact]
    public async Task Permissions_and_mapping_are_validated()
    {
        await using var f = new WorkflowApiFactory(); var d = await Seed(f); using var analyst = Client(f, "Analyst"); using var c = Client(f); var file = File(Row(d));
        foreach (var action in new[] { "inspect", "preview", "commit" }) Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/performance-imports/{action}", file)).StatusCode);
        var mapping = Mapping; mapping["vat"] = mapping["grossSales"];
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/performance-imports/preview", file with { Mapping = mapping })).StatusCode);
        var inspection = await c.PostAsJsonAsync("/api/performance-imports/inspect", file); inspection.EnsureSuccessStatusCode();
        var data = await inspection.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, data.GetProperty("rowCount").GetInt32()); Assert.Equal(0, data.GetProperty("mapping").GetProperty("brandId").GetInt32());
    }
    [Fact]
    public void Csv_parser_supports_quotes_and_rejects_duplicate_headers_broken_rows_and_limits()
    {
        ImportFileRequest Raw(string text) => new("test.csv", Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
        var table = PerformanceImportFile.Read(Raw("a;b;c;d\n\"a;b\";x;2026-08;TRY")); Assert.Equal("a;b", table.Rows[0].Values[0]);
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(Raw("a;a;c;d\nx;x;x;x")));
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(Raw("a;b;c;d\nx;x;x")));
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(Raw("a;b;c;d\n\"bad;x;x;x")));
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(Raw("a;b;c;d\n" + string.Join('\n', Enumerable.Repeat("x;x;x;x", 201)))));
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(new("test.csv", new string('A', 1_333_340))));
    }
    internal static ImportFileRequest Xlsx(string worksheet)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string text) { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write(text); }
            Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"AylikVeri\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Entry("xl/worksheets/sheet1.xml", worksheet);
        }
        return new("test.xlsx", Convert.ToBase64String(output.ToArray()));
    }
    [Fact]
    public void Excel_parser_reads_typed_values_without_double_and_rejects_formulas_and_xml_entities()
    {
        Assert.Contains("Excel dosyası bozuk", Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(new("bozuk.xlsx", Convert.ToBase64String(Encoding.UTF8.GetBytes("not an Excel file"))))).Message);
        var xml = "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>a</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>b</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>c</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>d</t></is></c></row><row r=\"2\"><c r=\"A2\"><v>1000000.1256</v></c><c r=\"B2\"><v>0</v></c><c r=\"C2\"><v>1</v></c><c r=\"D2\"><v>2</v></c></row></sheetData></worksheet>";
        var table = PerformanceImportFile.Read(Xlsx(xml)); Assert.Equal(1000000.1256m, PerformanceImportFile.Number(table.Rows[0].Values[0]));
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(Xlsx(xml.Replace("<v>2</v>", "<f>SUM(1)</f><v>2</v>"))));
        Assert.Throws<InvalidDataException>(() => PerformanceImportFile.Read(Xlsx("<!DOCTYPE test [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" + xml)));
    }
    [Fact]
    public async Task Excel_batch_keeps_currencies_separate_and_saves_matching_totals()
    {
        await using var f = new WorkflowApiFactory(); var first = await Seed(f); using var c = Client(f);
        Deal second;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var brand = new Brand { Name = "Dolar markası" };
            second = new Deal { Brand = brand, BrandId = brand.Id, Name = "Dolar anlaşması", Currency = "USD", Status = DealStatus.Active, RevenueShareRate = .2m };
            db.Add(second); await db.SaveChangesAsync();
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new[] { PerformanceImportFile.Fields.Select(x => x.Label).ToArray(), Row(first), Row(second) };
        var xml = new XElement(ns + "worksheet", new XElement(ns + "sheetData", rows.Select((values, r) =>
            new XElement(ns + "row", new XAttribute("r", r + 1), values.Select((v, i) =>
                new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + i)}{r + 1}"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", v))))))));
        var file = Xlsx(xml.ToString()) with { Mapping = Mapping }; var preview = await Preview(c, file);
        Assert.True(preview.GetProperty("canCommit").GetBoolean()); var totals = preview.GetProperty("totals"); Assert.Equal(2, totals.GetArrayLength());
        Assert.Equal("TRY", totals[0].GetProperty("currency").GetString()); Assert.Equal(100000.0126m, totals[0].GetProperty("ovoFee").GetDecimal());
        Assert.Equal("USD", totals[1].GetProperty("currency").GetString()); Assert.Equal(200000.0251m, totals[1].GetProperty("ovoFee").GetDecimal());
        var response = await c.PostAsJsonAsync("/api/performance-imports/commit", file with { PreviewToken = preview.GetProperty("previewToken").GetString() });
        response.EnsureSuccessStatusCode(); var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(totals.ToString(), saved.GetProperty("totals").ToString()); Assert.Equal(2, saved.GetProperty("rowCount").GetInt32());
    }
    [Fact]
    public async Task Expired_or_tampered_approval_cannot_create_records()
    {
        await using var f = new WorkflowApiFactory(); var d = await Seed(f); using var c = Client(f); var file = File(Row(d));
        var token = (await Preview(c, file)).GetProperty("previewToken").GetString()!;
        var protector = f.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("OvoGrowthOS.MonthlyImport.v1");
        var approval = JsonSerializer.Deserialize<ImportApproval>(protector.Unprotect(token))!;
        var expired = protector.Protect(JsonSerializer.Serialize(approval with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }));
        foreach (var invalid in new[] { expired, "invalid-approval" })
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/performance-imports/commit", file with { PreviewToken = invalid })).StatusCode);
        using var scope = f.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().MonthlyPerformances.ToListAsync());
    }
    [Fact]
    public void Shipped_excel_and_csv_templates_have_matching_fields_and_values()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "OvoGrowthOS.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "apps", "web", "public", "templates");
        ImportFileTable Read(string extension) => PerformanceImportFile.Read(new("template." + extension, Convert.ToBase64String(System.IO.File.ReadAllBytes(Path.Combine(path, "ovo-aylik-veri." + extension)))));
        var excel = Read("xlsx"); var csv = Read("csv");
        Assert.Equal(PerformanceImportFile.Fields.Select(x => x.Label), excel.Headers); Assert.Equal(excel.Headers, csv.Headers);
        Assert.Single(excel.Rows); Assert.Equal(csv.Rows[0].Values, excel.Rows[0].Values);
    }
}
