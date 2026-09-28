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
        public bool Fail { get; set; }
        public StoreOrderPeriod? LastPeriod { get; private set; }
        public Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, StorePlatform platform, CancellationToken ct)
        {
            LastPeriod = period;
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

    private static StoreOrderDraft Draft(int number) =>
        new($"source-{number}", "store-1", number, new DateTimeOffset(2026, 8, 19, 21, 0, 0, TimeSpan.Zero),
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
        orders.Drafts.AddRange([Draft(1), Draft(2)]);

        var notified = await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(3)));
        Assert.True(notified >= 1);
        Assert.Equal(2026, orders.LastPeriod!.Year);
        Assert.Equal(8, orders.LastPeriod.Month);
        await Db(f, async db =>
        {
            Assert.Equal(2, await db.StoreOrderStagings.CountAsync(x => x.BrandId == brand));
            var claim = await db.AuditRecords.SingleAsync(x => x.Action == "StoreOrdersAutoSync");
            Assert.Equal("2026-08", claim.EntityId);
            Assert.Contains("tamamlandı", claim.Reason);
            Assert.True(await db.UserNotifications.CountAsync(x => x.Kind == NotificationKind.StoreSync && x.EventKey == "store-sync:2026-08") >= 1);
            Assert.False(await db.UserNotifications.AnyAsync(x => x.UserId == Partner && x.Kind == NotificationKind.StoreSync));
            Assert.False(await db.UserNotifications.AnyAsync(x => x.UserId == Admin && x.Kind == NotificationKind.StoreSync && x.EmailStatus != null));
        });

        using var c = await Client(f);
        var body = await c.GetStringAsync("/api/notifications");
        Assert.Contains("Otomatik sipariş senkronu", body);
        Assert.Contains("/brands", body);

        Assert.Equal(0, await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 5, 0, TimeSpan.FromHours(3))));
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

        await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(3)));

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

        Assert.Equal(0, await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(3))));
        await Db(f, async db =>
        {
            Assert.False(await db.AuditRecords.AnyAsync(x => x.Action == "StoreOrdersAutoSync"));
            Assert.False(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.StoreSync));
        });

        var brand = await SeedBrandWithSettings(f);
        orders.Drafts.Add(Draft(1));
        Assert.True(await RunDue(f, new DateTimeOffset(2026, 9, 5, 10, 1, 0, TimeSpan.FromHours(3))) >= 1);
        await Db(f, async db =>
        {
            Assert.Equal(1, await db.StoreOrderStagings.CountAsync(x => x.BrandId == brand));
            Assert.Equal(1, await db.AuditRecords.CountAsync(x => x.Action == "StoreOrdersAutoSync"));
        });
    }
}
