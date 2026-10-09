using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class StoreOrderAutoSyncTests
{
    private static readonly Guid Admin = WorkflowApiFactory.AccountId("admin@ovo.test");
    private static readonly Guid Partner = WorkflowApiFactory.AccountId("partner@ovo.test");

    private sealed class FakeToken : IStoreTokenClient
    {
        public Task<StoreTokenResult> CreateTokenAsync(string storeUrl, string apiUser, string passwordBase64, CancellationToken ct) =>
            Task.FromResult(new StoreTokenResult(true, "fake-store-token"));
    }

    private sealed class FakeOrders : IStoreOrderClient
    {
        public List<StoreOrderDraft> Drafts { get; } = [];
        public List<StoreOrderPeriod> Periods { get; } = [];
        public bool Fail { get; set; }
        public StoreOrderPeriod? LastPeriod { get; private set; }
        public Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, StorePlatform platform, CancellationToken ct)
        {
            LastPeriod = period; Periods.Add(period);
            return Task.FromResult(Fail ? new StoreOrderFetchResult(false, [], false) : new StoreOrderFetchResult(true, Drafts, false));
        }
        public Task<bool> TestAsync(StorePlatform platform, string token, string storeUrl, CancellationToken ct) => Task.FromResult(!Fail);
    }

    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, FakeToken token, FakeOrders orders) =>
        parent.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IStoreTokenClient>(); s.AddSingleton<IStoreTokenClient>(token);
            s.RemoveAll<IStoreOrderClient>(); s.AddSingleton<IStoreOrderClient>(orders);
        }));

    // Bildirim listesi gerçek zamana göre son 30 günü gösterir; sabit geçmiş tarihler
    // zamanla filtrenin dışında kalır. Bu yüzden tüm tarihler gerçek güne göre türetilir.
    private static (DateTime Target, string PeriodKey) PreviousPeriod(DateTimeOffset now)
    {
        var target = now.ToOffset(TimeSpan.FromHours(3)).Date.AddMonths(-1);
        return (target, $"{target.Year:0000}-{target.Month:00}");
    }

    private static StoreOrderDraft Draft(int number, DateTime target) =>
        new($"source-{number}", "store-1", number, new DateTimeOffset(target.Year, target.Month, 15, 21, 0, 0, TimeSpan.Zero),
            "TRY", 952.20m, 952.20m, 0m, StoreOrderStatus.Complete, 30);

    private static async Task<Guid> SeedBrandWithSettings(WebApplicationFactory<Program> f)
    {
        var id = Guid.NewGuid();
        await Db(f, async db => { db.Add(new Brand { Id = id, Name = "Kozabiat" }); await db.SaveChangesAsync(); });
        var c = await Client(f);
        var response = await c.PutAsJsonAsync($"/api/brands/{id}/api-settings",
            new BrandApiSettingsRequest(0, "https://store.example.test/", "api@example.test", "Store-secret-123"));
        response.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> work)
    { await using var scope = f.Services.CreateAsyncScope(); await work(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    private static async Task<int> RunDue(WebApplicationFactory<Program> f, DateTimeOffset now)
    {
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<StoreOrderSyncQueue>().RunDue(now);
    }

    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string role = "admin")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    [Fact]
    public async Task Auto_sync_runs_once_for_the_previous_period_and_notifies_admins()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrandWithSettings(f);
        var t0 = DateTimeOffset.UtcNow; var (target, periodKey) = PreviousPeriod(t0);
        orders.Drafts.AddRange([Draft(1, target), Draft(2, target)]);

        var notified = await RunDue(f, t0);
        Assert.True(notified >= 1);
        Assert.Equal(target.Year, orders.LastPeriod!.Year);
        Assert.Equal(target.Month, orders.LastPeriod.Month);
        await Db(f, async db =>
        {
            Assert.Equal(2, await db.StoreOrderStagings.CountAsync(x => x.BrandId == brand));
            var claim = await db.AuditRecords.SingleAsync(x => x.Action == "StoreOrdersAutoSync");
            Assert.Equal(periodKey, claim.EntityId);
            Assert.Contains("tamamlandı", claim.Reason);
            Assert.True(await db.UserNotifications.CountAsync(x => x.Kind == NotificationKind.StoreSync && x.EventKey == "store-sync:" + periodKey) >= 1);
            Assert.False(await db.UserNotifications.AnyAsync(x => x.UserId == Partner && x.Kind == NotificationKind.StoreSync));
            Assert.False(await db.UserNotifications.AnyAsync(x => x.UserId == Admin && x.Kind == NotificationKind.StoreSync && x.EmailStatus != null));
        });

        using var c = await Client(f);
        var body = await c.GetStringAsync("/api/notifications");
        Assert.Contains("Otomatik sipariş senkronu", body);
        Assert.Contains("/brands", body);

        Assert.Equal(0, await RunDue(f, t0.AddMinutes(5)));
        await Db(f, async db =>
        {
            Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "StoreOrdersAutoSync"));
            Assert.Equal(1, await db.UserNotifications.CountAsync(x => x.UserId == Admin && x.Kind == NotificationKind.StoreSync));
        });
    }

    [Fact]
    public async Task Auto_sync_reports_failures_without_leaking_secrets()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders { Fail = true }; await using var f = Setup(p, new FakeToken(), orders);
        await SeedBrandWithSettings(f);

        await RunDue(f, DateTimeOffset.UtcNow);

        await Db(f, async db =>
        {
            var claim = await db.AuditRecords.SingleAsync(x => x.Action == "StoreOrdersAutoSync");
            Assert.Contains("başarısız", claim.Reason);
            Assert.DoesNotContain("Store-secret-123", claim.Reason);
            Assert.DoesNotContain("fake-store-token", claim.Reason);
            Assert.Empty(await db.StoreOrderStagings.ToListAsync());
        });
        using var c = await Client(f);
        var body = await c.GetStringAsync("/api/notifications");
        Assert.Contains("başarısız", body);
        Assert.DoesNotContain("fake-store-token", body);
        Assert.DoesNotContain("Store-secret-123", body);
    }

    [Fact]
    public async Task Auto_sync_without_configured_brands_waits_and_runs_after_settings_are_saved()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);

        var t0 = DateTimeOffset.UtcNow; var (target, _) = PreviousPeriod(t0);
        Assert.Equal(0, await RunDue(f, t0));
        await Db(f, async db =>
        {
            Assert.False(await db.AuditRecords.AnyAsync(x => x.Action == "StoreOrdersAutoSync"));
            Assert.False(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.StoreSync));
        });

        var brand = await SeedBrandWithSettings(f);
        orders.Drafts.Add(Draft(1, target));
        Assert.True(await RunDue(f, t0.AddMinutes(1)) >= 1);
        await Db(f, async db =>
        {
            Assert.Equal(1, await db.StoreOrderStagings.CountAsync(x => x.BrandId == brand));
            Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "StoreOrdersAutoSync"));
        });
    }

    [Fact]
    public async Task Auto_sync_retries_failed_brands_after_cooldown_and_announces_the_fix()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders { Fail = true }; await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrandWithSettings(f);
        var t0 = DateTimeOffset.UtcNow; var (target, periodKey) = PreviousPeriod(t0);
        orders.Drafts.Add(Draft(1, target));

        await RunDue(f, t0);
        await Db(f, async db =>
        {
            var brandFailure = await db.AuditRecords.SingleAsync(x => x.Action == "StoreOrdersAutoSyncBrand");
            Assert.Contains("başarısız", brandFailure.Reason);
            Assert.DoesNotContain("Store-secret-123", brandFailure.Reason);
            Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "StoreOrdersAutoSync"));
        });

        orders.Fail = false;
        Assert.Equal(0, await RunDue(f, t0.AddMinutes(10)));
        await Db(f, async db =>
            Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "StoreOrdersAutoSyncBrand")));

        Assert.True(await RunDue(f, t0.AddHours(7)) >= 1);
        await Db(f, async db =>
        {
            Assert.Equal(1, await db.StoreOrderStagings.CountAsync(x => x.BrandId == brand));
            Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "StoreOrdersAutoSync"));
            Assert.True(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.StoreSync
                && x.EventKey == $"store-sync-fixed:{periodKey}:{brand}"));
        });
        using var c = await Client(f);
        var body = await c.GetStringAsync("/api/notifications");
        Assert.Contains("tekrar denemesinde tamamlandı", body);

        Assert.Equal(0, await RunDue(f, t0.AddHours(8)));
    }

    [Fact]
    public async Task Manual_sync_for_the_period_makes_auto_sync_skip_it()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrandWithSettings(f);
        using var c = await Client(f);
        var (manualTarget, periodKey) = PreviousPeriod(DateTimeOffset.UtcNow);
        orders.Drafts.Add(Draft(1, manualTarget));
        var response = await c.PostAsJsonAsync($"/api/brands/{brand}/store-orders/sync", new { period = periodKey });
        response.EnsureSuccessStatusCode();

        Assert.Equal(0, await RunDue(f, DateTimeOffset.UtcNow));
        await Db(f, async db =>
        {
            Assert.Equal(1, await db.StoreOrderStagings.CountAsync(x => x.BrandId == brand));
            Assert.False(await db.AuditRecords.AnyAsync(x => x.Action == "StoreOrdersAutoSync"));
        });
    }

    [Fact]
    public async Task Backfill_pulls_previous_months_oldest_first_and_validates_input()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrandWithSettings(f);
        using var c = await Client(f);

        var response = await c.PostAsJsonAsync($"/api/brands/{brand}/store-orders/backfill", new { months = 3 });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, payload.GetProperty("results").GetArrayLength());
        foreach (var item in payload.GetProperty("results").EnumerateArray())
            Assert.True(item.GetProperty("ok").GetBoolean(), item.GetProperty("error").GetString());
        var trNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3));
        var expected = Enumerable.Range(1, 3).Reverse().Select(back => { var d = trNow.Date.AddMonths(-back); return $"{d.Year:0000}-{d.Month:00}"; }).ToArray();
        Assert.Equal(expected, orders.Periods.Select(x => x.Key).ToArray());

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/brands/{brand}/store-orders/backfill", new { months = 0 })).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/brands/{brand}/store-orders/backfill", new { months = 13 })).StatusCode);
        using var partner = await Client(f, "partner");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await partner.PostAsJsonAsync($"/api/brands/{brand}/store-orders/backfill", new { months = 3 })).StatusCode);
    }
}
