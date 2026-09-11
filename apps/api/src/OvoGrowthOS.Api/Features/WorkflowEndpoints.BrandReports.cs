using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record InternalBrandReport(ServiceCostSummary Costs, decimal StoredGrossProfit, decimal PortfolioFeeShare,
    bool ConcentrationWarning, InvestmentSummary Investment);
public sealed class BrandReportDocument
{
    public Guid BrandId { get; init; }
    public string BrandName { get; init; } = "";
    public string Currency { get; init; } = "TRY";
    public int Year { get; init; }
    public int Month { get; init; }
    public string Scope { get; init; } = "";
    public string Audience { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public BrandReportMetrics? Current { get; init; }
    public BrandReportMetrics? Previous { get; init; }
    public decimal? RevenueChange { get; init; }
    public IReadOnlyList<ReportExplanation> Explanations { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public InternalBrandReport? Internal { get; init; }
    public string Csv { get; set; } = "";
}

public static partial class WorkflowEndpoints
{
    private static void MapBrandReports(WebApplication app) => app.MapGet("/api/reports/brands/{id:guid}", BrandReport).RequireAuthorization("ReadAccess");

    private static async Task<IResult> BrandReport(Guid id, int year, int month, AppDbContext db, ClaimsPrincipal user,
        ReportScope scope = ReportScope.Closed, string audience = "brand", string? currency = null)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12 || !Enum.IsDefined(scope) || audience is not ("brand" or "internal"))
            return Results.BadRequest(new { error = "Geçerli bir dönem, kapsam ve rapor görünümü seçin." });
        if (audience == "internal" && !user.IsInRole("Admin") && !user.IsInRole("Partner")) return Results.Forbid();
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); if (brand is null) return Results.NotFound();
        currency = (currency ?? brand.Currency).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter)) return Results.BadRequest(new { error = "Üç harfli bir para birimi seçin." });
        var date = new DateOnly(year, month, 1); var previousDate = date.AddMonths(-1);
        var rows = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal).Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
            .Where(x => x.BrandId == id && x.Deal!.Currency.ToUpper() == currency &&
                (x.Year == year && x.Month == month || x.Year == previousDate.Year && x.Month == previousDate.Month)).ToListAsync();
        var current = rows.SingleOrDefault(x => x.Year == year && x.Month == month && PortfolioReporting.Matches(x.Status, scope));
        var previous = rows.SingleOrDefault(x => x.Year == previousDate.Year && x.Month == previousDate.Month && PortfolioReporting.Matches(x.Status, scope));
        var metrics = current is null ? null : BrandReporting.Metrics(current);
        var previousMetrics = previous is null ? null : BrandReporting.Metrics(previous);
        InternalBrandReport? internalReport = null;
        if (audience == "internal" && current is not null)
        {
            var costs = await db.ServiceCostAccounts.AsNoTracking().Include(x => x.Entries).SingleOrDefaultAsync(x => x.MonthlyPerformanceId == current.Id);
            var investments = await db.InvestmentAccounts.AsNoTracking().Include(x => x.Entries).SingleOrDefaultAsync(x => x.DealId == current.DealId);
            var portfolio = await db.MonthlyPerformances.AsNoTracking().Where(x => x.Year == year && x.Month == month && x.Deal!.Currency.ToUpper() == currency).Select(x => new { x.Status, x.OvoFee }).ToListAsync();
            var fees = portfolio.Where(x => PortfolioReporting.Matches(x.Status, scope)).Sum(x => Math.Max(x.OvoFee, 0));
            var share = fees > 0 ? Math.Max(current.OvoFee, 0) / fees : 0;
            var threshold = await db.GeneralSettings.Select(x => x.ConcentrationRiskThreshold).SingleAsync();
            internalReport = new(OperatingCosts.Summary(current, costs), current.OvoGrossProfit, share, share > threshold, OperatingCosts.InvestmentSummary(current.Deal!, investments));
        }
        var doc = new BrandReportDocument { BrandId = id, BrandName = brand.Name, Currency = currency, Year = year, Month = month,
            Scope = scope.ToString(), Audience = audience, GeneratedAt = DateTimeOffset.UtcNow, Current = metrics, Previous = previousMetrics,
            RevenueChange = current is null ? null : BrandReporting.Change(current.NetRevenue, previous?.NetRevenue),
            Explanations = BrandReporting.Explain(metrics, previousMetrics, current is not null && previous is not null && current.DealId != previous.DealId), Internal = internalReport };
        doc.Csv = BrandReportCsv(doc);
        return Results.Ok(doc);
    }

    internal static string BrandReportCsv(BrandReportDocument d)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR"); var csv = new StringBuilder();
        string Cell(string value) => "\"" + ((value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' && !decimal.TryParse(value, NumberStyles.Number, tr, out _)) ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
        void Row(params string[] values) => csv.AppendLine(string.Join(';', values.Select(Cell)));
        string Money(decimal? value) => value?.ToString("0.####", tr) ?? "Veri yok";
        Row("Marka", d.BrandName); Row("Dönem", $"{d.Month}/{d.Year}"); Row("Para birimi", d.Currency);
        Row("Kapsam", d.Scope switch { "Closed" => "Kapanmış dönemler", "Approved" => "Onaylı, kilit bekleyen", "Preparation" => "Hazırlık ve kontrol", _ => "Tüm kayıtlar - taslaklar dahil" });
        Row("Görünüm", d.Audience == "internal" ? "OVO iç yönetim - paylaşmayın" : "Markayla paylaşılabilir");
        Row("Hazırlanma zamanı (UTC)", d.GeneratedAt.ToString("O"));
        Row("Uyarı", "Tutarlar KDV hariçtir. Katkı, vergi sonrası net kâr değildir. Eksik veri sıfır kabul edilmez.");
        Row("Kesinleşme", d.Scope == "Closed" ? "Yalnız kapanmış dönem sonuçları" : "Kapanmamış veya taslak sonuçlar içerebilir; kesinleşmiş gelir değildir.");
        string Status(BrandReportMetrics? metrics) => metrics?.Status switch
        {
            MonthlyPerformanceStatus.Draft => "Taslak", MonthlyPerformanceStatus.UnderReview => "Kontrolde",
            MonthlyPerformanceStatus.Approved => "Onaylandı", MonthlyPerformanceStatus.Locked => "Kilitlendi",
            MonthlyPerformanceStatus.Invoiced => "Faturalandı", MonthlyPerformanceStatus.Paid => "Ödendi", _ => "Veri yok"
        };
        Row("Ölçüm", "Seçili ay", "Önceki ay");
        Row("Dönem durumu", Status(d.Current), Status(d.Previous));
        Row("Net ciro değişimi (0-1)", Money(d.RevenueChange));
        Row("Net ciro", Money(d.Current?.NetRevenue), Money(d.Previous?.NetRevenue));
        Row("OVO hakedişi", Money(d.Current?.OvoFee), Money(d.Previous?.OvoFee));
        Row("Reklam gideri", Money(d.Current?.AdSpend), Money(d.Previous?.AdSpend));
        Row("Reklam verimliliği (x)", Money(d.Current?.Mer), Money(d.Previous?.Mer));
        Row("İade oranı (0-1)", Money(d.Current?.RefundRate), Money(d.Previous?.RefundRate));
        Row("Markaya kalan katkı", Money(d.Current?.BrandContribution), Money(d.Previous?.BrandContribution));
        Row("Hakedişe bağlı ödenen (eski tarihsizler dahil)", Money(d.Current?.Paid), Money(d.Previous?.Paid));
        Row("Kalan alacak", Money(d.Current?.Outstanding), Money(d.Previous?.Outstanding));
        if (d.Internal is { } i)
        {
            Row("OVO iç bilgileri", "Yalnız yönetim"); Row("Planlanan hizmet maliyeti", Money(i.Costs.PlannedCost));
            Row("Girilen gerçek hizmet maliyeti", Money(i.Costs.RecordedCost)); Row("Maliyet kontrolü", i.Costs.Complete ? "Tamamlandı" : "Tamamlanmadı");
            Row("Gerçek gider sonrası katkı", Money(i.Costs.ContributionAfterRecordedCosts));
            Row("Portföyde hakediş payı (pozitif hakedişler, 0-1)", Money(i.PortfolioFeeShare));
            Row("Kayıtlı yatırım harcaması", Money(i.Investment.RecordedInvestment)); Row("Kayıtlı geri kazanım", Money(i.Investment.RecordedRecovery)); Row("Kalan kayıtlı yatırım", Money(i.Investment.Remaining));
        }
        Row("Ne oldu?", "Neden dikkat gerekiyor?", "Sonraki adım"); foreach (var e in d.Explanations) Row(e.WhatHappened, e.WhyItMatters, e.NextStep);
        return csv.ToString();
    }
}
