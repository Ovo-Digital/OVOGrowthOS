using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortalTrendTests
{
    private const string Path = "/api/portal/trend?endYear=2026&endMonth=8&months=3&currency=TRY";
    private static string Root(Guid id) => $"/api/portal-management/brands/{id}/reports";
    private static async Task<Guid> Publish(HttpClient c, Guid brand, Guid period)
    {
        var result = await c.PostAsJsonAsync(Root(brand), new PortalPublishRequest(period)); result.EnsureSuccessStatusCode();
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task Trend_reads_only_own_published_snapshots_and_latest_visible_version_once()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        await Publish(admin, s.BrandId, s.PeriodId); await Publish(admin, s.OtherBrandId, s.OtherPeriodId);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.MonthlyPerformances.FindAsync(s.PeriodId); p!.NetRevenue = 1250000.1234m;
            db.Add(new MonthlyPerformance { BrandId = s.BrandId, DealId = p.DealId, Year = 2026, Month = 7, Status = MonthlyPerformanceStatus.Locked, NetRevenue = 123456789 }); await db.SaveChangesAsync();
        }
        var latest = await Publish(admin, s.BrandId, s.PeriodId);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.MonthlyPerformances.FindAsync(s.PeriodId))!.NetRevenue = 555555; await db.SaveChangesAsync(); }
        var response = await c.GetAsync(Path + "&brandId=" + s.OtherBrandId); var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.Headers.CacheControl!.NoStore);
        foreach (var hidden in new[] { "OTHER_", "98765", "PRIVATE_", "123456789", "555555", "ovoGrossProfit", "estimatedMonthlyInternalCost" }) Assert.DoesNotContain(hidden, text);
        var trend = JsonSerializer.Deserialize<JsonElement>(text).GetProperty("trend"); Assert.Equal(1, trend.GetProperty("sharedMonths").GetInt32());
        Assert.Equal(1250000.1234m, trend.GetProperty("totals").GetProperty("netRevenue").GetDecimal());
        Assert.Equal(3, trend.GetProperty("items").GetArrayLength()); Assert.Equal(JsonValueKind.Null, trend.GetProperty("items")[1].GetProperty("metrics").ValueKind);
        Assert.Equal(latest, trend.GetProperty("items")[2].GetProperty("reportId").GetGuid()); Assert.Equal(2, trend.GetProperty("items")[2].GetProperty("version").GetInt32());
    }
    [Fact]
    public async Task Revocation_excludes_that_version_and_all_revoked_is_missing_not_zero()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        var first = await Publish(admin, s.BrandId, s.PeriodId); var second = await Publish(admin, s.BrandId, s.PeriodId);
        (await admin.PostAsync(Root(s.BrandId) + $"/{second}/revoke", null)).EnsureSuccessStatusCode();
        var trend = (await c.GetFromJsonAsync<JsonElement>(Path)).GetProperty("trend"); Assert.Equal(first, trend.GetProperty("items")[2].GetProperty("reportId").GetGuid());
        (await admin.PostAsync(Root(s.BrandId) + $"/{first}/revoke", null)).EnsureSuccessStatusCode();
        trend = (await c.GetFromJsonAsync<JsonElement>(Path)).GetProperty("trend"); Assert.Equal(JsonValueKind.Null, trend.GetProperty("totals").ValueKind);
        Assert.Equal(0, trend.GetProperty("sharedMonths").GetInt32()); Assert.All(trend.GetProperty("items").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("metrics").ValueKind));
    }
    [Fact]
    public async Task Filters_and_access_are_enforced_and_currency_is_not_combined()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        await Publish(admin, s.BrandId, s.PeriodId);
        using var anonymous = f.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(Path)).StatusCode);
        foreach (var query in new[] { "endYear=2019&endMonth=8", "endYear=2026&endMonth=13", "endYear=2026&endMonth=8&months=4", "endYear=2026&endMonth=8&currency=try" })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/portal/trend?" + query)).StatusCode);
        foreach (var months in new[] { 3, 6, 12 }) Assert.Equal(months, (await c.GetFromJsonAsync<JsonElement>($"/api/portal/trend?endYear=2026&endMonth=8&months={months}")).GetProperty("trend").GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, (await c.GetFromJsonAsync<JsonElement>(Path.Replace("TRY", "USD"))).GetProperty("trend").GetProperty("totals").ValueKind);
    }
}
