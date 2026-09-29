using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class CollectionPlanningTests
{
    [Fact]
    public async Task Planning_reads_all_closed_months_in_one_currency_without_writes_or_fabricated_dates()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); var today = TeamWork.Today(DateTimeOffset.UtcNow);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deal = new Deal { BrandId = brand, Name = "Tahsilat planı", Currency = "TRY" }; var usd = new Deal { BrandId = brand, Name = "Dolar anlaşması", Currency = "USD" };
            var p = new MonthlyPerformance { BrandId = brand, Deal = deal, DealId = deal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Invoiced,
                OvoFee = 100.1234m, Collection = new() { ReceivableAmount = 100.1234m, Currency = "TRY", DueOn = today, Payments = [new() { Id = Guid.NewGuid(), Amount = 40.0001m, PaidOn = today, Reference = "PLAN-TEST" }] } };
            db.AddRange(p,
                new MonthlyPerformance { BrandId = brand, Deal = deal, DealId = deal.Id, Year = 2026, Month = 7, Status = MonthlyPerformanceStatus.Locked, OvoFee = 20 },
                new MonthlyPerformance { BrandId = brand, Deal = deal, DealId = deal.Id, Year = 2026, Month = 6, Status = MonthlyPerformanceStatus.Paid, OvoFee = 30 },
                new MonthlyPerformance { BrandId = brand, Deal = usd, DealId = usd.Id, Year = 2026, Month = 5, Status = MonthlyPerformanceStatus.Locked, OvoFee = 1000 },
                new MonthlyPerformance { BrandId = brand, Deal = deal, DealId = deal.Id, Year = 2026, Month = 4, Status = MonthlyPerformanceStatus.Draft, OvoFee = 9000 },
                new MonthlyPerformance { BrandId = brand, Deal = deal, DealId = deal.Id, Year = 2026, Month = 3, Status = MonthlyPerformanceStatus.Paid, OvoFee = 50,
                    Collection = new() { ReceivableAmount = 50, Currency = "TRY", DueOn = today.AddDays(-10), Payments = [new() { Id = Guid.NewGuid(), Amount = 50, PaidOn = today.AddDays(-10), Reference = "PLAN-ON-TIME" }] } });
            await db.SaveChangesAsync();
        }
        using var c = CustomerPortalTests.Staff(f, "Analyst");
        var result = await c.GetFromJsonAsync<JsonElement>("/api/collection-planning?weeks=8"); var plan = result.GetProperty("plan");
        Assert.Equal(new[] { "TRY", "USD" }, result.GetProperty("currencies").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("TRY", plan.GetProperty("currency").GetString()); Assert.Equal(4, plan.GetProperty("closedPeriods").GetInt32());
        Assert.Equal(80.1233m, plan.GetProperty("outstanding").GetDecimal()); Assert.Equal(20, plan.GetProperty("unknownDue").GetDecimal());
        Assert.Equal(30, plan.GetProperty("legacyPaid").GetDecimal()); Assert.Equal(8, plan.GetProperty("upcoming").GetArrayLength());
        Assert.Equal(90.0001m, plan.GetProperty("received").EnumerateArray().Sum(x => x.GetProperty("amount").GetDecimal()));
        Assert.Equal(2, plan.GetProperty("items").GetArrayLength());
        var performance = result.GetProperty("performance");
        Assert.Equal(1m, performance.GetProperty("onTimeRate").GetDecimal());
        Assert.Equal(0m, performance.GetProperty("averageDays").GetDecimal());
        Assert.Equal(1, performance.GetProperty("recordCount").GetInt32());
        Assert.Equal(1, performance.GetProperty("paymentCount").GetInt32());
        Assert.Equal(1, performance.GetProperty("onTimePayments").GetInt32());
        Assert.Equal(50m, performance.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(1000, (await c.GetFromJsonAsync<JsonElement>("/api/collection-planning?currency=USD&weeks=12")).GetProperty("plan").GetProperty("outstanding").GetDecimal());
        var usdResult = await c.GetFromJsonAsync<JsonElement>("/api/collection-planning?currency=USD&weeks=12");
        Assert.Equal(JsonValueKind.Null, usdResult.GetProperty("performance").GetProperty("onTimeRate").ValueKind);
        Assert.Equal(0, usdResult.GetProperty("performance").GetProperty("recordCount").GetInt32());
        Assert.Equal(0, (await c.GetFromJsonAsync<JsonElement>("/api/collection-planning?currency=EUR")).GetProperty("plan").GetProperty("closedPeriods").GetInt32());
        using var check = f.Services.CreateScope(); var saved = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await saved.AuditRecords.ToListAsync()); Assert.Equal(6, await saved.MonthlyPerformances.CountAsync()); Assert.Equal(2, await saved.CollectionPayments.CountAsync());
        Assert.Equal(100.1234m, (await saved.MonthlyPerformances.SingleAsync(x => x.Month == 8)).OvoFee);
    }

    [Fact]
    public async Task Empty_portfolio_is_explicit_and_invalid_filters_are_rejected()
    {
        await using var f = new WorkflowApiFactory(); await f.SeedAsync(); using var c = CustomerPortalTests.Staff(f);
        var plan = (await c.GetFromJsonAsync<JsonElement>("/api/collection-planning")).GetProperty("plan");
        Assert.Equal(0, plan.GetProperty("closedPeriods").GetInt32()); Assert.Empty(plan.GetProperty("items").EnumerateArray());
        var empty = await c.GetFromJsonAsync<JsonElement>("/api/collection-planning");
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("performance").GetProperty("onTimeRate").ValueKind);
        Assert.Equal(0, empty.GetProperty("performance").GetProperty("recordCount").GetInt32());
        foreach (var filter in new[] { "weeks=0", "weeks=5", "weeks=13", "currency=try", "currency=TRYUSD" })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/collection-planning?" + filter)).StatusCode);
        using var anonymous = f.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/collection-planning")).StatusCode);
    }
}
