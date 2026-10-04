using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class AdCampaignTests
{
    private sealed class FakeAds : IAdSpendClient
    {
        public IReadOnlyList<AdCampaignResult> Campaigns { get; set; } = new[]
        {
            new AdCampaignResult("Yaz kampanyası", 400m, "TRY"),
            new AdCampaignResult("Yeniden pazarlama", 250.5m, "TRY")
        };
        public Task<bool> TestAsync(AdConnection connection, CancellationToken ct) => Task.FromResult(true);
        public Task<AdSpendResult?> FetchAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
            => Task.FromResult<AdSpendResult?>(new AdSpendResult(650.5m, "TRY", "Meta reklam raporu"));
        public Task<IReadOnlyList<AdCampaignResult>?> FetchCampaignsAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdCampaignResult>?>(Campaigns);
    }

    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, FakeAds ads) =>
        parent.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAdSpendClient>(); s.AddSingleton<IAdSpendClient>(ads);
        }));

    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task<Guid> SeedBrand(WebApplicationFactory<Program> f)
    {
        var id = Guid.NewGuid();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new Brand { Id = id, Name = "Reklomarka" });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task EnableMeta(WebApplicationFactory<Program> f, Guid brand)
    {
        using var admin = await Client(f);
        var saved = await admin.PutAsJsonAsync($"/api/brands/{brand}/ad-settings",
            new { revision = 0, platform = "Meta", accountId = "1234567890", clientId = "", secret = "meta-secret-123", clientSecret = "", developerToken = "" });
        saved.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Campaigns_are_read_stored_listed_and_replaced_with_an_audit_trail()
    {
        await using var p = new WorkflowApiFactory(); var ads = new FakeAds();
        await using var f = Setup(p, ads);
        var brand = await SeedBrand(f);
        await EnableMeta(f, brand);
        using var admin = await Client(f);

        var read = await admin.PostAsJsonAsync($"/api/brands/{brand}/ad-campaigns", new { platform = "Meta", period = "2026-09" });
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var body = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal("2026-09", body.RootElement.GetProperty("period").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("campaigns").GetArrayLength());
        Assert.Equal(650.5m, body.RootElement.GetProperty("totals").EnumerateArray().Single().GetProperty("spend").GetDecimal());
        Assert.Contains("okundu", body.RootElement.GetProperty("message").GetString());

        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/ad-campaigns?period=2026-09");
        Assert.Equal(2, stored.GetProperty("campaigns").GetArrayLength());

        ads.Campaigns = new[] { new AdCampaignResult("Tek kampanya", 120m, "TRY") };
        (await admin.PostAsJsonAsync($"/api/brands/{brand}/ad-campaigns", new { platform = "Meta", period = "2026-09" })).EnsureSuccessStatusCode();
        var replaced = await admin.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/ad-campaigns?period=2026-09");
        var names = replaced.GetProperty("campaigns").EnumerateArray().Select(x => x.GetProperty("campaignName").GetString()).ToList();
        Assert.Equal(new[] { "Tek kampanya" }, names);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.AdCampaignSpends.CountAsync(x => x.BrandId == brand));
        var audits = await db.AuditRecords.Where(x => x.Action == "AdCampaignsRead").OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.DoesNotContain("meta-secret-123", audits[0].Reason);
        Assert.Contains("kampanya harcaması okundu", audits[0].Reason);
    }

    [Fact]
    public async Task Live_read_requires_connection_settings_and_a_valid_period()
    {
        await using var p = new WorkflowApiFactory(); var ads = new FakeAds();
        await using var f = Setup(p, ads);
        var brand = await SeedBrand(f);
        using var admin = await Client(f);

        var missing = await admin.PostAsJsonAsync($"/api/brands/{brand}/ad-campaigns", new { platform = "Meta", period = "2026-09" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("bağlantı ayarları kayıtlı değil", await missing.Content.ReadAsStringAsync());

        await EnableMeta(f, brand);
        var badPeriod = await admin.PostAsJsonAsync($"/api/brands/{brand}/ad-campaigns", new { platform = "Meta", period = "2026-13" });
        Assert.Equal(HttpStatusCode.BadRequest, badPeriod.StatusCode);
        Assert.Contains("YYYY-AA", await badPeriod.Content.ReadAsStringAsync());

        var badPlatform = await admin.PostAsJsonAsync($"/api/brands/{brand}/ad-campaigns", new { platform = "TikTok", period = "2026-09" });
        Assert.Equal(HttpStatusCode.BadRequest, badPlatform.StatusCode);

        var missingBrand = await admin.PostAsJsonAsync($"/api/brands/{Guid.NewGuid()}/ad-campaigns", new { platform = "Meta", period = "2026-09" });
        Assert.Equal(HttpStatusCode.NotFound, missingBrand.StatusCode);

        var emptyList = await admin.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/ad-campaigns?period=2026-10");
        Assert.Equal(0, emptyList.GetProperty("campaigns").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, emptyList.GetProperty("readAt").ValueKind);
    }

    [Fact]
    public async Task Analyst_can_read_campaigns_but_cannot_trigger_a_live_read()
    {
        await using var p = new WorkflowApiFactory(); var ads = new FakeAds();
        await using var f = Setup(p, ads);
        var brand = await SeedBrand(f);
        await EnableMeta(f, brand);
        using var analyst = await Client(f, "analyst@ovo.test");

        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync($"/api/brands/{brand}/ad-campaigns?period=2026-09")).StatusCode);
        var write = await analyst.PostAsJsonAsync($"/api/brands/{brand}/ad-campaigns", new { platform = "Meta", period = "2026-09" });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public void Meta_and_google_campaign_payloads_are_parsed()
    {
        using var meta = JsonDocument.Parse("""{"data":[{"campaign_name":"Yaz","spend":"123.45","account_currency":"TRY"},{"campaign_name":"","spend":"10","account_currency":"TRY"}]}""");
        var metaRows = AdSpendClient.ParseMetaCampaigns(meta.RootElement);
        Assert.NotNull(metaRows);
        Assert.Equal("Yaz", metaRows![0].CampaignName);
        Assert.Equal(123.45m, metaRows[0].Spend);
        Assert.Equal("TRY", metaRows[0].Currency);

        using var broken = JsonDocument.Parse("""{"data":[{"campaign_name":"Bozuk","spend":"abc"}]}""");
        Assert.Null(AdSpendClient.ParseMetaCampaigns(broken.RootElement));

        using var google = JsonDocument.Parse("""[{"results":[{"campaign":{"name":"Arama"},"metrics":{"costMicros":"45000000"},"customer":{"currencyCode":"USD"}}]}]""");
        var googleRows = AdSpendClient.ParseGoogleCampaigns(google.RootElement);
        Assert.NotNull(googleRows);
        Assert.Equal("Arama", googleRows![0].CampaignName);
        Assert.Equal(45m, googleRows[0].Spend);
        Assert.Equal("USD", googleRows[0].Currency);

        using var notAStream = JsonDocument.Parse("""{"error":"x"}""");
        Assert.Null(AdSpendClient.ParseGoogleCampaigns(notAStream.RootElement));
    }
}
