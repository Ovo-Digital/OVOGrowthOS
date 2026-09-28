using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortalBalanceTests
{
    private static async Task SeedBalances(WorkflowApiFactory f, HttpClient admin, Guid brandId)
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var deal = new Deal { BrandId = brandId, Name = "Bakiye Anlaşması", Status = DealStatus.Active, Currency = "TRY" };
        var period = new MonthlyPerformance { BrandId = brandId, DealId = deal.Id, Year = 2026, Month = 6, Status = MonthlyPerformanceStatus.Locked, OvoFee = 50000, NetRevenue = 500000 };
        var draft = new MonthlyPerformance { BrandId = brandId, DealId = deal.Id, Year = 2026, Month = 7, Status = MonthlyPerformanceStatus.Draft, OvoFee = 40000, NetRevenue = 400000 };
        var collection = new CollectionAccount { MonthlyPerformanceId = period.Id, Currency = "TRY", ReceivableAmount = 50000, DueOn = today.AddDays(-10) };
        var note = new BrandContactNote { BrandId = brandId, ContactOn = today.AddDays(-5), Text = "Müşteri söz verdi.", CreatedBy = "admin" };
        var promise = new CollectionPromise { MonthlyPerformanceId = period.Id, Amount = 20000, PromisedOn = today.AddDays(3), ContactNoteId = note.Id, OwnerId = WorkflowApiFactory.AccountId("admin@ovo.test"), PaymentIdsAtRecording = [] };
        db.AddRange(deal, period, draft, collection, note, promise);
        await db.SaveChangesAsync();
        using var adminScope = f.Services.CreateAsyncScope();
        var adminDb = adminScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var other = new Brand { Name = "BAKIYE_YABANCI" };
        var otherDeal = new Deal { BrandId = other.Id, Name = "Yabancı", Status = DealStatus.Active, Currency = "TRY" };
        var otherPeriod = new MonthlyPerformance { BrandId = other.Id, DealId = otherDeal.Id, Year = 2026, Month = 6, Status = MonthlyPerformanceStatus.Locked, OvoFee = 70000, NetRevenue = 700000 };
        adminDb.AddRange(other, otherDeal, otherPeriod, new CollectionAccount { MonthlyPerformanceId = otherPeriod.Id, Currency = "TRY", ReceivableAmount = 70000, DueOn = today.AddDays(-30) });
        await adminDb.SaveChangesAsync();
    }

    private static async Task<JsonElement> Get(HttpClient c)
    {
        var response = await c.GetAsync("/api/portal/balance");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Customer_sees_only_their_own_open_balance_with_promise_and_without_drafts()
    {
        await using var f = new WorkflowApiFactory();
        var brandId = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        await SeedBalances(f, admin, brandId);
        var (client, _) = await CustomerPortalTests.Customer(f, admin, brandId, "balance-client@ovo.test");
        using var c = client;

        var json = await Get(c);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal(2026, item.GetProperty("year").GetInt32());
        Assert.Equal(6, item.GetProperty("month").GetInt32());
        Assert.Equal(50000m, item.GetProperty("outstanding").GetDecimal());
        Assert.True(item.GetProperty("overdueDays").GetInt32() >= 10);
        Assert.Equal(0m, item.GetProperty("paid").GetDecimal());
        var promise = item.GetProperty("promise");
        Assert.Equal(20000m, promise.GetProperty("amount").GetDecimal());
        Assert.Equal("Waiting", promise.GetProperty("state").GetString());
        var totals = json.GetProperty("totals").EnumerateArray().ToList();
        var total = Assert.Single(totals);
        Assert.Equal("TRY", total.GetProperty("currency").GetString());
        Assert.Equal(50000m, total.GetProperty("outstanding").GetDecimal());
        Assert.Equal(50000m, total.GetProperty("overdue").GetDecimal());
    }

    [Fact]
    public async Task Staff_account_cannot_read_portal_balance()
    {
        await using var f = new WorkflowApiFactory();
        var brandId = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        await SeedBalances(f, admin, brandId);
        var response = await admin.GetAsync("/api/portal/balance");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
