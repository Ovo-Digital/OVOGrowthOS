using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class OperatingCostTests
{
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin") { var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role)); return c; }
    private static async Task<(Guid Period, Guid Deal)> Seed(WorkflowApiFactory f)
    {
        var brand = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var d = new Deal { BrandId = brand, Name = "Maliyet denemesi", Status = DealStatus.Active, SetupInvestment = 200_000 };
        var p = new MonthlyPerformance { BrandId = brand, DealId = d.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Locked, OvoFee = 100_000, OvoInternalCost = 30_000, OvoGrossProfit = 70_000 };
        db.Add(d); db.Add(p); await db.SaveChangesAsync(); return (p.Id, d.Id);
    }
    private static ServiceCostRequest Cost(int revision = 0) => new(Guid.NewGuid(), ServiceCostKind.DirectExpense, 10_000, null, null, new(2026, 8, 10), "GIDER-1", "Gizli tedarikçi gideri", revision);
    private static InvestmentEntryRequest Investment(int revision = 0) => new(Guid.NewGuid(), InvestmentEntryKind.Investment, 100_000, new(2026, 8, 10), "YATIRIM-1", "Gizli yatırım dayanağı", revision);

    [Fact]
    public async Task Cost_confirmation_reopen_correction_and_decimal_work_are_persistent()
    {
        await using var f = new WorkflowApiFactory(); var (id, _) = await Seed(f); using var c = Client(f);
        var first = Cost(); Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", first)).StatusCode);
        var second = Cost(1) with { Kind = ServiceCostKind.TeamWork, Amount = 0, Hours = 20, HourlyCost = 1000, Reference = "EMEK-1" };
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", second)).StatusCode);
        var read = await c.GetFromJsonAsync<JsonElement>($"/api/performance/{id}/costs"); Assert.Equal(JsonValueKind.Null, read.GetProperty("summary").GetProperty("contributionAfterRecordedCosts").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/performance/{id}/costs/confirmation", new CostConfirmationRequest(true, "Tüm giderler kontrol edildi", 2))).StatusCode);
        read = await c.GetFromJsonAsync<JsonElement>($"/api/performance/{id}/costs"); Assert.Equal(70_000, read.GetProperty("summary").GetProperty("contributionAfterRecordedCosts").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", Cost(3))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/performance/{id}/costs/confirmation", new CostConfirmationRequest(false, "Gizli düzeltme nedeni", 3))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries/{first.Id}/void", new VoidPaymentRequest("Yanlış gider", 4))).StatusCode);
        read = await c.GetFromJsonAsync<JsonElement>($"/api/performance/{id}/costs"); Assert.Equal(20_000, read.GetProperty("summary").GetProperty("recordedCost").GetDecimal()); Assert.Equal(2, read.GetProperty("entries").GetArrayLength()); Assert.Equal(2, read.GetProperty("reviews").GetArrayLength());
        using var scope = f.Services.CreateScope(); var p = await scope.ServiceProvider.GetRequiredService<AppDbContext>().MonthlyPerformances.FindAsync(id);
        Assert.Equal(30_000, p!.OvoInternalCost); Assert.Equal(70_000, p.OvoGrossProfit); Assert.Equal(MonthlyPerformanceStatus.Locked, p.Status);
    }
    [Fact]
    public async Task Analyst_cannot_read_sensitive_costs_investment_or_audit_details()
    {
        await using var f = new WorkflowApiFactory(); var (id, deal) = await Seed(f); using var c = Client(f); using var analyst = Client(f, "Analyst"); using var partner = Client(f, "Partner");
        await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", Cost()); await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries", Investment());
        await c.PutAsJsonAsync($"/api/performance/{id}/costs/confirmation", new CostConfirmationRequest(true, "Gizli kontrol notu", 1));
        foreach (var path in new[] { $"/api/performance/{id}/costs", $"/api/deals/{deal}/investment" }) Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/performance/{id}/costs/entries", Cost())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PutAsJsonAsync($"/api/performance/{id}/costs/confirmation", new CostConfirmationRequest(false, "Gerekçe", 2))).StatusCode);
        foreach (var path in new[] { $"/api/performance/{id}", $"/api/deals/{deal}", "/api/audit" })
        {
            var response = await analyst.GetAsync(path); response.EnsureSuccessStatusCode(); var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Gizli", body); Assert.DoesNotContain("hourlyCost", body); Assert.DoesNotContain("serviceCostEntries", body);
        }
    }
    [Fact]
    public async Task Cost_duplicate_stale_revision_bad_date_and_team_amount_are_rejected()
    {
        await using var f = new WorkflowApiFactory(); var (id, _) = await Seed(f); using var c = Client(f); var first = Cost();
        await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", first);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", first)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", Cost(1) with { Reference = " gider-1 " })).StatusCode);
        foreach (var invalid in new[] { Cost(1) with { IncurredOn = new(2026, 7, 1) }, Cost(1) with { Amount = -1 }, Cost(1) with { Kind = ServiceCostKind.TeamWork, Amount = 999, Hours = 1, HourlyCost = 5 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", invalid)).StatusCode);
    }
    [Fact]
    public async Task Investment_recovery_is_explicit_bounded_and_does_not_duplicate_revenue_or_expenses()
    {
        await using var f = new WorkflowApiFactory(); var (id, deal) = await Seed(f); using var c = Client(f);
        var spent = Investment(); var recovered = Investment(1) with { Kind = InvestmentEntryKind.Recovery, Amount = 40_000, Reference = "GERI-1" };
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries", recovered with { Revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries", spent)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries", recovered)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/performance/{id}/costs/entries", Cost() with { Reference = "YATIRIM-1" })).StatusCode);
        var read = await c.GetFromJsonAsync<JsonElement>($"/api/deals/{deal}/investment"); Assert.Equal(60_000, read.GetProperty("summary").GetProperty("remaining").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries", recovered with { Id = Guid.NewGuid(), Revision = 2, Amount = 60_001, Reference = "GERI-2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries/{spent.Id}/void", new VoidPaymentRequest("Hatalı harcama", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync($"/api/deals/{deal}/investment/entries/{recovered.Id}/void", new VoidPaymentRequest("Hatalı geri kazanım", 2))).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.CollectionPayments.ToListAsync()); Assert.Equal(100_000, (await db.MonthlyPerformances.FindAsync(id))!.OvoFee);
    }
}
