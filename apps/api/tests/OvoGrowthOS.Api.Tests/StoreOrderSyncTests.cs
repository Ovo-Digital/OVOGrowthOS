using System.Net;
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
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class StoreOrderSyncTests
{
    private sealed class FakeToken : IStoreTokenClient
    {
        public bool Accepted { get; set; } = true;
        public Task<StoreTokenResult> CreateTokenAsync(string storeUrl, string apiUser, string passwordBase64, CancellationToken ct) =>
            Task.FromResult(Accepted ? new StoreTokenResult(true, "fake-store-token") : new StoreTokenResult(false, ""));
    }

    private sealed class FakeOrders : IStoreOrderClient
    {
        public List<StoreOrderDraft> Drafts { get; } = [];
        public bool Fail { get; set; }
        public StoreOrderPeriod? LastPeriod { get; private set; }
        public StorePlatform? LastPlatform { get; private set; }
        public Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, StorePlatform platform, CancellationToken ct)
        {
            LastPeriod = period; LastPlatform = platform;
            return Task.FromResult(Fail ? new StoreOrderFetchResult(false, [], false) : new StoreOrderFetchResult(true, Drafts, false));
        }
        public Task<bool> TestAsync(StorePlatform platform, string token, string storeUrl, CancellationToken ct) =>
            Task.FromResult(!Fail);
    }

    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, FakeToken token, FakeOrders orders) =>
        parent.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IStoreTokenClient>(); s.AddSingleton<IStoreTokenClient>(token);
            s.RemoveAll<IStoreOrderClient>(); s.AddSingleton<IStoreOrderClient>(orders);
        }));

    private static StoreOrderDraft Draft(int number, decimal total = 952.20m, int status = StoreOrderStatus.Complete, string currency = "TRY",
        DateTimeOffset? at = null, string? id = null) =>
        new(id ?? $"source-{number}", "store-1", number, at ?? new DateTimeOffset(2026, 8, 19, 21, 0, 0, TimeSpan.Zero),
            currency, total, total, 0m, status, 30);

    private static BrandApiSettingsRequest Settings() => new(0, "https://store.example.test/", "api@example.test", "Store-secret-123");

    private static async Task<Guid> SeedBrand(WebApplicationFactory<Program> f)
    {
        var id = Guid.NewGuid();
        await Db(f, async db => { db.Add(new Brand { Id = id, Name = "Kozabiat" }); await db.SaveChangesAsync(); });
        return id;
    }

    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> work)
    { await using var scope = f.Services.CreateAsyncScope(); await work(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string role = "admin")
    {
        var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode(); c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()); return c;
    }

    private static string SyncUrl(Guid brand) => $"/api/brands/{brand}/store-orders/sync";
    private static string ReadUrl(Guid brand, string period = "2026-08") => $"/api/brands/{brand}/store-orders?period={period}";

    [Fact]
    public async Task Repeated_sync_updates_staging_without_duplicates()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Settings())).EnsureSuccessStatusCode();
        orders.Drafts.AddRange([Draft(1, 952.20m), Draft(2, 476.10m, StoreOrderStatus.Cancelled)]);

        var first = await (await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).Content.ReadAsStringAsync();
        Assert.Contains("2 yeni, 0 güncellenen", first);

        var read = await (await c.GetAsync(ReadUrl(brand))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("source-1", read); Assert.DoesNotContain("SourceOrderId", read); Assert.DoesNotContain("customerEmail", read);
        using (var doc = JsonDocument.Parse(read))
        {
            var root = doc.RootElement;
            Assert.Equal("2026-08", root.GetProperty("period").GetString());
            Assert.True(root.GetProperty("configured").GetBoolean());
            Assert.False(root.GetProperty("lastSyncAt").ValueKind == JsonValueKind.Null);
            Assert.Equal(2, root.GetProperty("total").GetInt32());
            var summary = root.GetProperty("summary");
            Assert.Equal(2, summary.GetProperty("orderCount").GetInt32());
            Assert.Equal(1428.30m, summary.GetProperty("grossTotal").GetDecimal());
            Assert.Equal(1, summary.GetProperty("cancelledCount").GetInt32());
            Assert.Equal(476.10m, summary.GetProperty("cancelledTotal").GetDecimal());
            Assert.Equal(952.20m, summary.GetProperty("activeTotal").GetDecimal());
            Assert.Equal("TRY", summary.GetProperty("currency").GetString());
            Assert.False(root.GetProperty("panel").GetProperty("panelExists").GetBoolean());
            Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        }
        Assert.Equal(2026, orders.LastPeriod!.Year);
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 21, 0, 0, TimeSpan.Zero), orders.LastPeriod.UtcStart);

        var before = await Rows(f, brand);
        var second = await (await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).Content.ReadAsStringAsync();
        Assert.Contains("0 yeni, 2 güncellenen", second);
        var after = await Rows(f, brand);
        Assert.Equal(2, after.Count);
        foreach (var row in after)
        {
            var previous = before.Single(x => x.SourceOrderId == row.SourceOrderId);
            Assert.Equal(previous.OrderTotal, row.OrderTotal);
            Assert.True(row.ImportedAt >= previous.ImportedAt);
        }
        await Db(f, async db => Assert.Contains(await db.AuditRecords.ToListAsync(), a => a.Action == "StoreOrdersSynced"));
    }

    [Theory]
    [InlineData("outside", "dönem sınırı")]
    [InlineData("negative", "eksi tutar")]
    [InlineData("currency", "para birimi")]
    public async Task Bad_source_rows_stop_the_sync_without_writing(string fault, string expected)
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Settings())).EnsureSuccessStatusCode();
        orders.Drafts.Add(fault switch
        {
            "outside" => Draft(9, at: new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero)),
            "negative" => Draft(9, total: -5m),
            _ => Draft(9, currency: "TRYX")
        });
        var response = await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
        await Db(f, async db => Assert.Empty(await db.StoreOrderStagings.ToListAsync()));
    }

    [Fact]
    public async Task Invalid_period_format_is_rejected()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new FakeToken(), new FakeOrders());
        var brand = await SeedBrand(f); using var c = await Client(f);
        var response = await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-13"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("biçiminde", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/brands/{brand}/store-orders?period=202613")).StatusCode);
    }

    [Fact]
    public async Task Read_is_open_to_internal_roles_and_sync_is_admin_only()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new FakeToken(), new FakeOrders());
        var brand = await SeedBrand(f);
        using var analyst = await Client(f, "analyst");
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync(ReadUrl(brand))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).StatusCode);
        using var partner = await Client(f, "partner");
        Assert.Equal(HttpStatusCode.OK, (await partner.GetAsync(ReadUrl(brand))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await analyst.GetAsync(ReadUrl(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Sync_without_saved_api_settings_is_rejected()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new FakeToken(), new FakeOrders());
        var brand = await SeedBrand(f); using var c = await Client(f);
        var body = await (await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).Content.ReadAsStringAsync();
        Assert.Contains("API ayarlarını", body);
        await Db(f, async db => Assert.Empty(await db.StoreOrderStagings.ToListAsync()));
    }

    [Fact]
    public async Task Failed_fetch_keeps_existing_rows_and_reports_a_safe_error()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Settings())).EnsureSuccessStatusCode();
        orders.Drafts.AddRange([Draft(1, 952.20m), Draft(2, 476.10m)]);
        (await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).EnsureSuccessStatusCode();
        var before = await Rows(f, brand);
        var lastSyncBefore = JsonDocument.Parse(await (await c.GetAsync(ReadUrl(brand))).Content.ReadAsStringAsync()).RootElement.GetProperty("lastSyncAt").GetRawText();

        orders.Fail = true;
        var response = await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("okunamadı", body);
        Assert.DoesNotContain("fake-store-token", body); Assert.DoesNotContain("Store-secret-123", body); Assert.DoesNotContain("Exception", body);

        var after = await Rows(f, brand);
        Assert.Equal(before.Count, after.Count);
        foreach (var row in after)
        {
            var previous = before.Single(x => x.Id == row.Id);
            Assert.Equal(previous.ImportedAt, row.ImportedAt);
            Assert.Equal(previous.OrderTotal, row.OrderTotal);
        }
        var read = JsonDocument.Parse(await (await c.GetAsync(ReadUrl(brand))).Content.ReadAsStringAsync());
        Assert.Equal(lastSyncBefore, read.RootElement.GetProperty("lastSyncAt").GetRawText());
    }

    [Fact]
    public async Task Panel_comparison_uses_entered_monthly_result_when_present()
    {
        await using var p = new WorkflowApiFactory(); var orders = new FakeOrders(); await using var f = Setup(p, new FakeToken(), orders);
        var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Settings())).EnsureSuccessStatusCode();
        orders.Drafts.Add(Draft(1, 1428.30m));
        (await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"))).EnsureSuccessStatusCode();
        await Db(f, async db =>
        {
            db.Add(new MonthlyPerformance { BrandId = brand, DealId = Guid.NewGuid(), Year = 2026, Month = 8, GrossSales = 1400.00m });
            await db.SaveChangesAsync();
        });
        using var doc = JsonDocument.Parse(await (await c.GetAsync(ReadUrl(brand))).Content.ReadAsStringAsync());
        var panel = doc.RootElement.GetProperty("panel");
        Assert.True(panel.GetProperty("panelExists").GetBoolean());
        Assert.Equal(1400.00m, panel.GetProperty("panelGrossSales").GetDecimal());
        Assert.Equal(28.30m, panel.GetProperty("difference").GetDecimal());
    }

    [Fact]
    public async Task Shopify_sync_uses_admin_token_directly_without_grandnode_login()
    {
        await using var p = new WorkflowApiFactory();
        var orders = new FakeOrders();
        // GrandNode jeton akışı kapalı olsa bile Shopify senkronu çalışmalı;
        // Shopify yolunda IStoreTokenClient hiç kullanılmaz.
        await using var f = Setup(p, new FakeToken { Accepted = false }, orders);
        var brand = await SeedBrand(f); using var c = await Client(f);
        var save = await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings",
            new BrandApiSettingsRequest(0, "https://demo.myshopify.com", "", "shpat-test-token-12345", "Shopify"));
        save.EnsureSuccessStatusCode();
        orders.Drafts.Add(Draft(1001, 2500m, currency: "USD"));

        var response = await c.PostAsJsonAsync(SyncUrl(brand), new StoreOrderSyncRequest("2026-08"));
        response.EnsureSuccessStatusCode();
        Assert.Equal(StorePlatform.Shopify, orders.LastPlatform);
        Assert.Contains("1 yeni, 0 güncellenen", await response.Content.ReadAsStringAsync());
        await Db(f, async db =>
        {
            var row = Assert.Single(await db.StoreOrderStagings.ToListAsync());
            Assert.Equal("USD", row.Currency);
            Assert.Equal(2500m, row.OrderTotal);
        });
    }

    [Fact]
    public async Task Shopify_connection_test_uses_order_client_result()
    {
        await using var p = new WorkflowApiFactory();
        var orders = new FakeOrders();
        await using var f = Setup(p, new FakeToken { Accepted = false }, orders);
        using var c = await Client(f);

        var okBrand = await SeedBrand(f);
        var okSave = await c.PutAsJsonAsync($"/api/brands/{okBrand}/api-settings",
            new BrandApiSettingsRequest(0, "https://demo.myshopify.com", "", "shpat-test-token-12345", "Shopify"));
        okSave.EnsureSuccessStatusCode();
        var okRevision = JsonDocument.Parse(await okSave.Content.ReadAsStringAsync()).RootElement.GetProperty("revision").GetInt32();
        var okBody = await (await c.PostAsJsonAsync($"/api/brands/{okBrand}/api-settings/test", new BrandApiTestRequest(okRevision))).Content.ReadAsStringAsync();
        Assert.Contains("Shopify", okBody);

        orders.Fail = true;
        var failBrand = await SeedBrand(f);
        var failSave = await c.PutAsJsonAsync($"/api/brands/{failBrand}/api-settings",
            new BrandApiSettingsRequest(0, "https://baska.myshopify.com", "", "shpat-baska-jeton-67890", "Shopify"));
        failSave.EnsureSuccessStatusCode();
        var failRevision = JsonDocument.Parse(await failSave.Content.ReadAsStringAsync()).RootElement.GetProperty("revision").GetInt32();
        var failBody = await (await c.PostAsJsonAsync($"/api/brands/{failBrand}/api-settings/test", new BrandApiTestRequest(failRevision))).Content.ReadAsStringAsync();
        Assert.Contains("Admin API jetonunu", failBody);
    }

    private static async Task<List<StoreOrderStaging>> Rows(WebApplicationFactory<Program> f, Guid brand)
    {
        List<StoreOrderStaging> rows = [];
        await Db(f, async db => rows = await db.StoreOrderStagings.AsNoTracking().Where(x => x.BrandId == brand).ToListAsync());
        return rows;
    }
}
