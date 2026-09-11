using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class ReportingTests
{
    [Fact]
    public async Task Dashboard_and_commissions_match_for_the_same_period_scope_and_currency_without_writing()
    {
        await using var factory = new WorkflowApiFactory();
        await Seed(factory);
        using var client = Client(factory);
        await using var dbScope = factory.Services.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.MonthlyPerformances.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Status, x.OvoFee, x.UpdatedAt }).ToListAsync();
        foreach (var scope in new[] { "Closed", "Approved", "Preparation", "All" })
        {
            var parameters = $"year=2026&month=9&scope={scope}&currency=TRY";
            var report = await client.GetFromJsonAsync<JsonElement>($"/api/dashboard?{parameters}");
            var commissions = await client.GetFromJsonAsync<JsonElement>($"/api/commissions?{parameters}&pageSize=1");
            Assert.Equal(report.GetProperty("totals").GetProperty("ovoFee").GetDecimal(), commissions.GetProperty("summary").GetProperty("ovoFee").GetDecimal());
            Assert.Equal(report.GetProperty("totals").GetProperty("recordCount").GetInt32(), commissions.GetProperty("total").GetInt32());
        }
        var closed = await client.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=9");
        Assert.Equal(30_000m, closed.GetProperty("ovoMonthlyRevenue").GetDecimal());
        Assert.Equal(30_000m, closed.GetProperty("paidCommission").GetDecimal());
        Assert.Equal(20_000m, closed.GetProperty("outstandingCommission").GetDecimal());
        Assert.Equal(0m, closed.GetProperty("periodOutstandingCommission").GetDecimal());
        Assert.Equal(1, closed.GetProperty("coverage").GetProperty("missingBrands").GetArrayLength());
        Assert.Equal(40_000m, closed.GetProperty("stages").GetProperty("approved").GetProperty("ovoFee").GetDecimal());
        Assert.Equal(900_000m, closed.GetProperty("stages").GetProperty("preparation").GetProperty("ovoFee").GetDecimal());
        var usd = await client.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=9&currency=USD");
        Assert.Equal(1_000m, usd.GetProperty("ovoMonthlyRevenue").GetDecimal());
        var after = await db.MonthlyPerformances.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Status, x.OvoFee, x.UpdatedAt }).ToListAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Latest_draft_period_does_not_show_drafts_as_closed_or_hide_older_debts()
    {
        await using var factory = new WorkflowApiFactory();
        await Seed(factory);
        using var client = Client(factory);
        var report = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(10, report.GetProperty("period").GetProperty("month").GetInt32());
        Assert.Equal(0, report.GetProperty("totals").GetProperty("recordCount").GetInt32());
        Assert.Equal(20_000m, report.GetProperty("outstandingCommission").GetDecimal());
        Assert.Equal("Unknown", report.GetProperty("concentrationRisk").GetString());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("trends")[11].GetProperty("netRevenue").ValueKind);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=7");
        Assert.Equal(0, empty.GetProperty("totals").GetProperty("recordCount").GetInt32());
        Assert.Equal(12, empty.GetProperty("trends").GetArrayLength());
        Assert.All(empty.GetProperty("trends").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("netRevenue").ValueKind));
    }

    [Theory]
    [InlineData("year=2026")]
    [InlineData("year=2026&month=13")]
    [InlineData("year=2010&month=1")]
    [InlineData("scope=99")]
    [InlineData("currency=INVALID")]
    public async Task Invalid_report_filters_are_rejected(string query)
    {
        await using var factory = new WorkflowApiFactory();
        await factory.SeedAsync();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/dashboard?{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/commissions?{query}")).StatusCode);
    }

    private static HttpClient Client(WorkflowApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        return client;
    }

    private static async Task Seed(WorkflowApiFactory factory)
    {
        await factory.SeedAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = new[] { ("Ödenen", MonthlyPerformanceStatus.Paid, 9, 30_000m, "TRY"),
            ("Eski alacak", MonthlyPerformanceStatus.Locked, 8, 20_000m, "TRY"),
            ("Onaylı", MonthlyPerformanceStatus.Approved, 9, 40_000m, "TRY"),
            ("Hazırlık", MonthlyPerformanceStatus.Draft, 9, 900_000m, "TRY"),
            ("Son taslak", MonthlyPerformanceStatus.Draft, 10, 500_000m, "TRY"),
            ("Dolar markası", MonthlyPerformanceStatus.Paid, 9, 1_000m, "USD") };
        foreach (var (name, state, month, fee, currency) in rows)
        {
            var brand = new Brand { Name = name, Status = BrandStatus.Active };
            var deal = new Deal { Name = name, Brand = brand, BrandId = brand.Id, Currency = currency, Status = DealStatus.Active,
                StartDate = new DateOnly(2026, month, 1), EndDate = new DateOnly(2026, month, DateTime.DaysInMonth(2026, month)) };
            db.MonthlyPerformances.Add(new MonthlyPerformance { Brand = brand, BrandId = brand.Id, Deal = deal, DealId = deal.Id,
                Year = 2026, Month = month, Status = state, OvoFee = fee, NetRevenue = fee * 10, OvoGrossProfit = fee / 2 });
        }
        var missing = new Brand { Name = "Verisi eksik", Status = BrandStatus.Active };
        db.Deals.Add(new Deal { Name = "Eksik dönem", Brand = missing, BrandId = missing.Id, Status = DealStatus.Active, StartDate = new DateOnly(2026, 9, 1) });
        await db.SaveChangesAsync();
    }
}
