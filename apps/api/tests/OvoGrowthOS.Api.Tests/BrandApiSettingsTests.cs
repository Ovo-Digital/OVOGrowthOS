using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public sealed class BrandApiSettingsTests
{
    private const string Secret = "Store-secret-123";
    public sealed class TokenClient : IStoreTokenClient
    {
        public bool Accepted { get; set; } = true;
        public List<(string StoreUrl, string ApiUser, string PasswordBase64)> Calls { get; } = [];
        public Task<StoreTokenResult> CreateTokenAsync(string storeUrl, string apiUser, string passwordBase64, CancellationToken ct)
        {
            Calls.Add((storeUrl, apiUser, passwordBase64));
            return Task.FromResult(Accepted ? new StoreTokenResult(true, "header.payload.signature") : new StoreTokenResult(false, ""));
        }
    }
    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, TokenClient client) =>
        parent.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IStoreTokenClient>(); s.AddSingleton<IStoreTokenClient>(client); }));
    private static BrandApiSettingsRequest Draft(int revision = 0, string? password = Secret) =>
        new(revision, "https://store.example.test/", "api@example.test", password);
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string role = "admin")
    {
        var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode(); c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()); return c;
    }
    private static async Task<Guid> SeedBrand(WebApplicationFactory<Program> f)
    {
        var id = Guid.NewGuid();
        await Db(f, async db => { db.Add(new Brand { Id = id, Name = "Kozabiat" }); await db.SaveChangesAsync(); });
        return id;
    }
    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> work)
    { await using var scope = f.Services.CreateAsyncScope(); await work(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    [Fact]
    public async Task Empty_settings_read_as_unconfigured_and_response_is_not_cacheable()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new TokenClient()); var brand = await SeedBrand(f); using var c = await Client(f);
        var result = await c.GetAsync($"/api/brands/{brand}/api-settings"); var text = await result.Content.ReadAsStringAsync();
        Assert.True(result.Headers.CacheControl?.NoStore);
        Assert.Contains("\"revision\":0", text); Assert.Contains("\"configured\":false", text); Assert.Contains("\"passwordStored\":false", text);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/brands/{Guid.NewGuid()}/api-settings")).StatusCode);
    }

    [Fact]
    public async Task Saved_settings_encrypt_password_and_response_or_audit_never_leak_it()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new TokenClient()); var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft())).EnsureSuccessStatusCode();
        var result = await c.GetAsync($"/api/brands/{brand}/api-settings"); var text = await result.Content.ReadAsStringAsync();
        Assert.Contains("\"passwordStored\":true", text); Assert.Contains("\"configured\":true", text);
        Assert.DoesNotContain(Secret, text); Assert.DoesNotContain("protectedPassword", text);
        await Db(f, async db =>
        {
            var row = await db.BrandApiSettings.SingleAsync();
            Assert.NotEqual(Secret, row.ProtectedPassword);
            Assert.DoesNotContain(Secret, JsonSerializer.Serialize(await db.AuditRecords.ToListAsync()));
            Assert.Contains(await db.AuditRecords.ToListAsync(), a => a.Action == "BrandApiSettingsChanged");
        });
    }

    [Fact]
    public async Task First_write_requires_password_and_stale_revision_is_rejected()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new TokenClient()); var brand = await SeedBrand(f); using var c = await Client(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft(0, null))).StatusCode);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft())).EnsureSuccessStatusCode();
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft(1, null))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"/api/brands/{Guid.NewGuid()}/api-settings", Draft())).StatusCode);
    }

    [Theory]
    [InlineData("url")][InlineData("host")][InlineData("user")][InlineData("password")]
    public async Task Unsafe_or_incomplete_configuration_is_rejected(string fault)
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new TokenClient()); var brand = await SeedBrand(f); using var c = await Client(f);
        var request = fault switch
        {
            "url" => Draft() with { StoreUrl = "http://store.example.test/" },
            "host" => Draft() with { StoreUrl = "https://user:pass@store.example.test/" },
            "user" => Draft() with { ApiUser = "not-an-email" },
            _ => Draft() with { Password = "short" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", request)).StatusCode);
        await Db(f, async db => Assert.Empty(await db.BrandApiSettings.ToListAsync()));
    }

    [Theory]
    [InlineData("partner")][InlineData("analyst")]
    public async Task Only_admin_can_read_save_or_test(string role)
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new TokenClient()); var brand = await SeedBrand(f); using var c = await Client(f, role);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/brands/{brand}/api-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync($"/api/brands/{brand}/api-settings/test", new BrandApiTestRequest(0))).StatusCode);
    }

    [Fact]
    public async Task Connection_test_sends_base64_password_and_records_the_outcome_once()
    {
        await using var p = new WorkflowApiFactory(); var client = new TokenClient(); await using var f = Setup(p, client); var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft())).EnsureSuccessStatusCode();
        var test = await c.PostAsJsonAsync($"/api/brands/{brand}/api-settings/test", new BrandApiTestRequest(1));
        var body = await test.Content.ReadAsStringAsync();
        Assert.True(test.Headers.CacheControl?.NoStore); Assert.Contains("\"accepted\":true", body); Assert.Contains("Mağaza erişimi doğrulandı", body);
        Assert.DoesNotContain("header.payload.signature", body); Assert.DoesNotContain(Secret, body);
        var call = Assert.Single(client.Calls);
        Assert.Equal("https://store.example.test/", call.StoreUrl); Assert.Equal("api@example.test", call.ApiUser);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(Secret)), call.PasswordBase64);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/brands/{brand}/api-settings/test", new BrandApiTestRequest(1))).StatusCode);
        await Db(f, async db =>
        {
            var audit = JsonSerializer.Serialize(await db.AuditRecords.ToListAsync());
            Assert.Contains("BrandApiTestRequested", audit); Assert.Contains("BrandApiTestAccepted", audit); Assert.DoesNotContain(Secret, audit);
        });
    }

    [Fact]
    public async Task Failed_connection_test_reports_a_safe_generic_error()
    {
        await using var p = new WorkflowApiFactory(); var client = new TokenClient { Accepted = false }; await using var f = Setup(p, client); var brand = await SeedBrand(f); using var c = await Client(f);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft())).EnsureSuccessStatusCode();
        var body = await (await c.PostAsJsonAsync($"/api/brands/{brand}/api-settings/test", new BrandApiTestRequest(1))).Content.ReadAsStringAsync();
        Assert.Contains("\"accepted\":false", body); Assert.Contains("Bağlantı doğrulanamadı", body); Assert.DoesNotContain(Secret, body);
        await Db(f, async db => Assert.Contains(await db.AuditRecords.ToListAsync(), a => a.Action == "BrandApiTestUncertain"));
    }

    [Fact]
    public async Task Test_without_saved_settings_is_rejected()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new TokenClient()); var brand = await SeedBrand(f); using var c = await Client(f);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/brands/{brand}/api-settings/test", new BrandApiTestRequest(0))).StatusCode);
        (await c.PutAsJsonAsync($"/api/brands/{brand}/api-settings", Draft())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/brands/{brand}/api-settings/test", new BrandApiTestRequest(0))).StatusCode);
    }
}
