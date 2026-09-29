using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class AdSpendSyncTests
{
    private sealed class FakeAds : IAdSpendClient
    {
        public bool Fail { get; set; }
        public Task<bool> TestAsync(AdConnection connection, CancellationToken ct) => Task.FromResult(!Fail);
        public Task<AdSpendResult?> FetchAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
            => Task.FromResult(Fail ? null : new AdSpendResult(1234.56m, "USD", "meta"));
    }

    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, FakeAds ads) =>
        parent.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAdSpendClient>(); s.AddSingleton<IAdSpendClient>(ads);
        }));

    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> work)
    { await using var scope = f.Services.CreateAsyncScope(); await work(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    private static async Task<int> RunDue(WebApplicationFactory<Program> f, DateTimeOffset now)
    {
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdSpendSyncQueue>().RunDue(now);
    }

    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task<Guid> SeedBrandWithAdSettings(WebApplicationFactory<Program> f)
    {
        var id = Guid.NewGuid();
        await Db(f, async db => { db.Add(new Brand { Id = id, Name = "Reklomark" }); await db.SaveChangesAsync(); });
        var c = await Client(f);
        var response = await c.PutAsJsonAsync($"/api/brands/{id}/ad-settings",
            new { revision = 0, platform = "Meta", accountId = "123456789", clientId = "", secret = "meta-secret-123", clientSecret = "", developerToken = "" });
        response.EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task Auto_read_reports_the_amount_and_feeds_the_quality_alert()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new FakeAds());
        var brand = await SeedBrandWithAdSettings(f);

        Assert.True(await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(3))) >= 1);
        await Db(f, async db =>
        {
            var summary = await db.AuditRecords.SingleAsync(x => x.Action == "AdSpendAutoSyncSummary");
            Assert.Equal("2026-08", summary.EntityId);
            Assert.Contains("Reklomark", summary.Reason);
            Assert.Contains("1234,56 USD", summary.Reason);
            Assert.True(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.AdSpendSync && x.EventKey == "ad-sync:2026-08"));
        });
        using var c = await Client(f);
        var body = await c.GetStringAsync("/api/notifications");
        Assert.Contains("Otomatik reklam harcaması okundu", body);
        Assert.Contains("1234,56 USD", body);

        Assert.Equal(0, await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 5, 0, TimeSpan.FromHours(3))));
        await Db(f, async db => Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "AdSpendAutoSyncSummary")));

        await Db(f, async db =>
        {
            db.Add(new Deal { BrandId = brand, Name = "Anlaşma", Status = DealStatus.Active, Currency = "TRY" });
            await db.SaveChangesAsync();
            var deal = await db.Deals.SingleAsync(x => x.BrandId == brand);
            db.Add(new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Draft, OvoFee = 0, NetRevenue = 0 });
            await db.SaveChangesAsync();
        });
        var report = await c.GetFromJsonAsync<JsonElement>("/api/data-quality?year=2026&month=8");
        var item = report.GetProperty("brands").EnumerateArray().Single(x => x.GetProperty("brandId").GetGuid() == brand);
        var alertCodes = item.GetProperty("alerts").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToList();
        Assert.Contains("ads_auto_read", alertCodes);
    }

    [Fact]
    public async Task Auto_read_retries_after_cooldown_and_announces_the_fix()
    {
        await using var p = new WorkflowApiFactory(); var ads = new FakeAds { Fail = true }; await using var f = Setup(p, ads);
        var brand = await SeedBrandWithAdSettings(f);
        var t0 = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(3));

        await RunDue(f, t0);
        await Db(f, async db =>
        {
            var failure = await db.AuditRecords.SingleAsync(x => x.Action == "AdSpendAutoSync");
            Assert.Contains("okunamadı", failure.Reason);
            Assert.DoesNotContain("meta-secret-123", failure.Reason);
            var summary = await db.AuditRecords.SingleAsync(x => x.Action == "AdSpendAutoSyncSummary");
            Assert.Contains("başarısız", summary.Reason);
        });

        ads.Fail = false;
        Assert.Equal(0, await RunDue(f, t0.AddMinutes(10)));
        await Db(f, async db => Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "AdSpendAutoSync" && x.EntityId.Contains($"{brand}"))));

        Assert.True(await RunDue(f, t0.AddHours(7)) >= 1);
        await Db(f, async db =>
            Assert.True(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.AdSpendSync
                && x.EventKey == $"ad-sync-fixed:2026-08:{brand}:Meta")));
        using var c = await Client(f);
        var body = await c.GetStringAsync("/api/notifications");
        Assert.Contains("tekrar denemesinde okundu", body);
        Assert.DoesNotContain("meta-secret-123", body);

        Assert.Equal(0, await RunDue(f, t0.AddHours(8)));
    }
}
