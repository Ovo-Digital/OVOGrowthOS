using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class BrandReportTests
{
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin") { var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role)); return c; }
    private static async Task<Guid> Seed(WorkflowApiFactory f, bool draft = false)
    {
        var id = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var brand = await db.Brands.FindAsync(id); brand!.Name = "=HYPERLINK(\"https://example.test\")";
        var deal = new Deal { BrandId = id, Name = "Rapor anlaşması", Currency = "TRY", SetupInvestment = 98765 };
        var old = new MonthlyPerformance { BrandId = id, DealId = deal.Id, Year = 2026, Month = 7, Status = MonthlyPerformanceStatus.Locked, NetRevenue = 1_000_000, GrossSales = 1_200_000, Refunds = 60_000, OvoFee = 100_000, TotalAdSpend = 200_000 };
        var current = new MonthlyPerformance { BrandId = id, DealId = deal.Id, Year = 2026, Month = 8, Status = draft ? MonthlyPerformanceStatus.Draft : MonthlyPerformanceStatus.Invoiced, NetRevenue = 800_000, GrossSales = 1_000_000, Refunds = 100_000, OvoFee = 80_000, TotalAdSpend = 200_000, OvoInternalCost = 12345.6789m, OvoGrossProfit = 67654.3211m };
        current.Collection = new() { MonthlyPerformanceId = current.Id, ReceivableAmount = 80_000, Currency = "TRY", Payments = [new() { Id = Guid.NewGuid(), Amount = 40_000, Reference = "SECRET-PAYMENT-REF", PaidOn = new(2026, 9, 1) }] };
        var other = new Brand { Name = "GİZLİ DİĞER MARKA" }; var otherDeal = new Deal { BrandId = other.Id, Name = "Başka anlaşma" };
        db.AddRange(deal, old, current, other, otherDeal, new MonthlyPerformance { BrandId = other.Id, DealId = otherDeal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Locked, OvoFee = 200_000 });
        db.Add(new ServiceCostAccount { MonthlyPerformanceId = current.Id, ConfirmedAt = DateTimeOffset.UtcNow,
            Entries = [new() { Id = Guid.NewGuid(), Amount = 22222, Reference = "SECRET-COST", Description = "Gizli personel maliyeti" }] });
        await db.SaveChangesAsync(); return id;
    }
    [Fact]
    public async Task Shared_report_whitelist_and_csv_exclude_private_costs_other_brands_and_payment_references()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var c = Client(f, "Analyst");
        var raw = await c.GetStringAsync($"/api/reports/brands/{id}?year=2026&month=8&audience=brand");
        using var doc = JsonDocument.Parse(raw); var d = doc.RootElement;
        Assert.False(d.TryGetProperty("internal", out _));
        foreach (var secret in new[] { "12345.6789", "67654.3211", "22222", "GİZLİ DİĞER MARKA", "SECRET-PAYMENT-REF", "SECRET-COST", "hourlyCost", "Gizli personel" }) Assert.DoesNotContain(secret, raw);
        Assert.Equal(800_000, d.GetProperty("current").GetProperty("netRevenue").GetDecimal()); Assert.Equal(40_000, d.GetProperty("current").GetProperty("outstanding").GetDecimal());
        Assert.Equal(-.2m, d.GetProperty("revenueChange").GetDecimal());
        var csv = d.GetProperty("csv").GetString()!; Assert.Contains("\"Net ciro\";\"800000\";\"1000000\"", csv); Assert.Contains("'=HYPERLINK", csv); Assert.Contains("Markayla paylaşılabilir", csv);
        Assert.Contains("\"Dönem durumu\";\"Faturalandı\";\"Kilitlendi\"", csv);
        Assert.Contains("\"Net ciro değişimi (0-1)\";\"-0,2\"", csv);
    }
    [Fact]
    public async Task Internal_report_requires_management_role_and_uses_confirmed_actual_costs_once()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var analyst = Client(f, "Analyst"); using var c = Client(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync($"/api/reports/brands/{id}?year=2026&month=8&audience=internal")).StatusCode);
        var d = await c.GetFromJsonAsync<JsonElement>($"/api/reports/brands/{id}?year=2026&month=8&audience=internal");
        var cost = d.GetProperty("internal").GetProperty("costs"); Assert.Equal(57_778, cost.GetProperty("contributionAfterRecordedCosts").GetDecimal());
        Assert.Contains("OVO iç yönetim - paylaşmayın", d.GetProperty("csv").GetString());
    }
    [Fact]
    public async Task Closed_scope_excludes_draft_and_missing_month_is_not_zero_result()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f, true); using var c = Client(f);
        var d = await c.GetFromJsonAsync<JsonElement>($"/api/reports/brands/{id}?year=2026&month=8");
        Assert.Equal(JsonValueKind.Null, d.GetProperty("current").ValueKind); Assert.Equal(JsonValueKind.Null, d.GetProperty("revenueChange").ValueKind);
        var all = await c.GetFromJsonAsync<JsonElement>($"/api/reports/brands/{id}?year=2026&month=8&scope=All"); Assert.Equal("Draft", all.GetProperty("current").GetProperty("status").GetString());
        Assert.Contains("kesinleşmiş gelir değildir", all.GetProperty("csv").GetString());
        Assert.Contains("\"Dönem durumu\";\"Taslak\";\"Kilitlendi\"", all.GetProperty("csv").GetString());
        var usd = await c.GetFromJsonAsync<JsonElement>($"/api/reports/brands/{id}?year=2026&month=8&currency=USD"); Assert.Equal(JsonValueKind.Null, usd.GetProperty("current").ValueKind);
        var missing = await c.GetFromJsonAsync<JsonElement>($"/api/reports/brands/{id}?year=2026&month=10"); Assert.Equal(JsonValueKind.Null, missing.GetProperty("current").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/reports/brands/{id}?year=2026&month=13")).StatusCode);
    }
}
