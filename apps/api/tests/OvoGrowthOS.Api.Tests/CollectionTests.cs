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

public sealed class CollectionTests
{
    private static readonly DateOnly Today = TeamWork.Today(DateTimeOffset.UtcNow);
    private static HttpClient Client(WorkflowApiFactory factory, string role = "Admin")
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role)); return client;
    }
    private static async Task<Guid> Seed(WorkflowApiFactory factory, MonthlyPerformanceStatus status = MonthlyPerformanceStatus.Locked, decimal fee = 100_000)
    {
        var brandId = await factory.SeedAsync(); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brandId, Currency = "TRY", Name = "Tahsilat test anlaşması" }; db.Add(deal);
        var p = new MonthlyPerformance { BrandId = brandId, DealId = deal.Id, Year = 2026, Month = 8, Status = status, OvoFee = fee, NetRevenue = 1_000_000, OvoGrossProfit = 70_000, CommissionBreakdownJson = "{}" };
        db.Add(p); await db.SaveChangesAsync(); return p.Id;
    }
    private static InvoiceRequest Invoice(int revision = 0) => new("FATURA-1", Today.AddDays(-4), Today.AddDays(-1), revision > 0 ? "Vade belgesine göre düzeltildi" : "", revision);
    private static PaymentRequest Payment(decimal amount = 40_000, int revision = 1) => new(Guid.NewGuid(), amount, Today, "BANKA-1", "KDV hariç pay", revision);
    private static Task<JsonElement> Read(HttpClient client, Guid id) => client.GetFromJsonAsync<JsonElement>($"/api/performance/{id}/collection");

    [Fact]
    public async Task Partial_full_payment_and_admin_correction_match_all_reports_without_changing_finances()
    {
        await using var factory = new WorkflowApiFactory(); var id = await Seed(factory); using var client = Client(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice())).StatusCode);
        var first = Payment();
        var firstResponse = await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", first);
        Assert.True(firstResponse.IsSuccessStatusCode, await firstResponse.Content.ReadAsStringAsync());
        var read = await Read(client, id); Assert.Equal(60_000, read.GetProperty("balance").GetProperty("outstanding").GetDecimal());
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=8");
        Assert.Equal(60_000, dashboard.GetProperty("outstandingCommission").GetDecimal()); Assert.Equal(40_000, dashboard.GetProperty("paidCommission").GetDecimal());
        var cash = await client.GetFromJsonAsync<JsonElement>($"/api/dashboard?year={Today.Year}&month={Today.Month}");
        Assert.Equal(40_000, cash.GetProperty("cashReceivedInSelectedMonth").GetDecimal());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/commissions?collection=partial");
        Assert.Equal(1, list.GetProperty("total").GetInt32()); Assert.Equal(60_000, list.GetProperty("outstanding").GetDecimal());
        var second = Payment(60_000, 2) with { Reference = "BANKA-2" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", second)).StatusCode);
        Assert.Equal("Paid", (await Read(client, id)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments/{first.Id}/void", new VoidPaymentRequest("Yanlış banka satırı seçildi", 3))).StatusCode);
        read = await Read(client, id); Assert.Equal("Invoiced", read.GetProperty("status").GetString()); Assert.Equal(40_000, read.GetProperty("balance").GetProperty("outstanding").GetDecimal());
        Assert.Equal(2, read.GetProperty("payments").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/performance/{id}/unlock", new { reason = "Açma denemesi" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.MonthlyPerformances.FindAsync(id);
        Assert.Equal(100_000, p!.OvoFee); Assert.Equal(70_000, p.OvoGrossProfit); Assert.Equal(1_000_000, p.NetRevenue); Assert.Equal("{}", p.CommissionBreakdownJson);
        Assert.Equal(4, await db.AuditRecords.CountAsync(x => x.EntityId == id.ToString()));
    }

    [Fact]
    public async Task Duplicate_reference_id_and_stale_revision_never_add_a_second_payment()
    {
        await using var factory = new WorkflowApiFactory(); var id = await Seed(factory); using var client = Client(factory);
        await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice()); var payment = Payment();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", payment)).StatusCode);
        foreach (var retry in new[] { payment, payment with { Revision = 2 }, payment with { Id = Guid.NewGuid(), Revision = 2, Reference = " banka-1 " } })
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", retry)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice())).StatusCode);
        Assert.Single((await Read(client, id)).GetProperty("payments").EnumerateArray());
    }

    [Fact]
    public async Task Legacy_paid_keeps_amount_but_no_fabricated_date_or_new_payment()
    {
        await using var factory = new WorkflowApiFactory(); var id = await Seed(factory, MonthlyPerformanceStatus.Paid, 30_000); using var client = Client(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", new InvoiceRequest("", null, null, "", 0))).StatusCode);
        var read = await Read(client, id); Assert.Equal(30_000, read.GetProperty("balance").GetProperty("legacyPaid").GetDecimal()); Assert.Equal(JsonValueKind.Null, read.GetProperty("invoiceOn").ValueKind); Assert.Empty(read.GetProperty("payments").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", Payment(1))).StatusCode);
    }

    [Fact]
    public async Task Role_validation_dates_overpayment_and_blind_status_routes_are_guarded()
    {
        await using var factory = new WorkflowApiFactory(); var id = await Seed(factory); using var client = Client(factory); using var analyst = Client(factory, "Analyst"); using var partner = Client(factory, "Partner");
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/performance/{id}/pay", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/performance/{id}/invoice", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice() with { InvoiceOn = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice() with { DueOn = Today.AddDays(-5) })).StatusCode);
        await client.PutAsJsonAsync($"/api/performance/{id}/collection/invoice", Invoice());
        foreach (var invalid in new[] { Payment(100_001), Payment(.00001m), Payment() with { PaidOn = Today.AddDays(1) } })
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", Payment() with { Reference = " " })).StatusCode);
        var payment = Payment(); await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments", payment);
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsJsonAsync($"/api/performance/{id}/collection/payments/{payment.Id}/void", new VoidPaymentRequest("Yanlış kayıt", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/performance/{id}/collection/payments/{payment.Id}/void", new VoidPaymentRequest(" ", 2))).StatusCode);
    }
}
