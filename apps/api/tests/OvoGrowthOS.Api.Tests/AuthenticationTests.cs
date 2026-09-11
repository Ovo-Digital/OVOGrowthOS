using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task Login_normalizes_email_and_issues_account_bound_session()
    {
        await using var factory = new WorkflowApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = " ADMIN@OVO.TEST ", password = WorkflowApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("token").GetString());
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(WorkflowApiFactory.AccountId("admin@ovo.test"), me.GetProperty("id").GetGuid());
        Assert.False(body.GetProperty("user").TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Missing_and_disabled_accounts_cannot_use_default_admin_password()
    {
        await using var factory = new WorkflowApiFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserAccounts.SingleAsync(x => x.Email == "analyst@ovo.test")).IsActive = false;
        await db.SaveChangesAsync();
        foreach (var email in new[] { "missing@ovo.test", "analyst@ovo.test" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = "Wrong-password" })).StatusCode);
    }

    [Fact]
    public async Task Legacy_or_mismatched_role_tokens_are_rejected()
    {
        await using var factory = new WorkflowApiFactory();
        using var client = factory.CreateClient();
        foreach (var token in new[] { WorkflowApiFactory.Token("admin@ovo.test", "Admin", false), WorkflowApiFactory.Token("analyst@ovo.test", "Admin") })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        }
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("role")]
    [InlineData("password")]
    [InlineData("email")]
    public async Task Account_changes_revoke_existing_sessions(string change)
    {
        await using var factory = new WorkflowApiFactory();
        using var admin = AdminClient(factory);
        using var analyst = factory.CreateClient();
        analyst.DefaultRequestHeaders.Authorization = new("Bearer", WorkflowApiFactory.Token("analyst@ovo.test", "Analyst"));
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync("/api/auth/me")).StatusCode);
        var request = new UserAccountRequest(change == "email" ? "renamed@ovo.test" : "analyst@ovo.test", "Analist",
            change == "role" ? "Partner" : "Analyst", change == "password" ? "New-test-password!" : null, change != "disable");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{WorkflowApiFactory.AccountId("analyst@ovo.test")}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await analyst.GetAsync("/api/auth/me")).StatusCode);
        if (change == "password")
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await analyst.PostAsJsonAsync("/api/auth/login", new { email = "analyst@ovo.test", password = WorkflowApiFactory.TestPassword })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await analyst.PostAsJsonAsync("/api/auth/login", new { email = "analyst@ovo.test", password = request.Password })).StatusCode);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditRecords.SingleAsync(x => x.Action == "UserChanged");
        Assert.DoesNotContain("Password", audit.NewValueJson);
        Assert.DoesNotContain("New-test-password", audit.NewValueJson);
    }

    [Theory]
    [InlineData("Admin", false)]
    [InlineData("Analyst", true)]
    public async Task Last_active_admin_cannot_be_removed(string role, bool active)
    {
        await using var factory = new WorkflowApiFactory();
        using var client = AdminClient(factory);
        var response = await client.PutAsJsonAsync($"/api/users/{WorkflowApiFactory.AccountId("admin@ovo.test")}",
            new UserAccountRequest("admin@ovo.test", "Yönetici", role, null, active));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Theory]
    [InlineData("bad-email", "Yeni kişi", "Analyst", "Long-test-password")]
    [InlineData("new@ovo.test", " ", "Analyst", "Long-test-password")]
    [InlineData("new@ovo.test", "Yeni kişi", "SuperAdmin", "Long-test-password")]
    [InlineData("new@ovo.test", "Yeni kişi", "Analyst", "short")]
    public async Task Invalid_user_fields_are_rejected_on_create_and_edit(string email, string name, string role, string password)
    {
        await using var factory = new WorkflowApiFactory();
        using var client = AdminClient(factory);
        var request = new UserAccountRequest(email, name, role, password);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/users/{WorkflowApiFactory.AccountId("analyst@ovo.test")}", request)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_normalized_email_and_non_admin_changes_are_rejected()
    {
        await using var factory = new WorkflowApiFactory();
        using var client = AdminClient(factory);
        var request = new UserAccountRequest(" ADMIN@OVO.TEST ", "Yeni kişi", "Analyst", "Long-test-password");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{WorkflowApiFactory.AccountId("analyst@ovo.test")}", request)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", WorkflowApiFactory.Token("partner@ovo.test", "Partner"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{WorkflowApiFactory.AccountId("analyst@ovo.test")}", request)).StatusCode);
    }

    [Fact]
    public async Task Repeated_login_attempts_are_limited_even_with_spoofed_forwarding_header()
    {
        await using var factory = new WorkflowApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 10; i++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{i}");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@ovo.test", password = "Wrong-password" })).StatusCode);
        }
        var limited = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@ovo.test", password = "Wrong-password" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), limited.Headers.RetryAfter?.Delta);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("!.!")]
    [InlineData(".eA==")]
    public void Malformed_password_hash_does_not_throw(string encoded) => Assert.False(JwtTokenService.VerifyPassword("test", encoded));

    [Fact]
    public async Task New_admin_can_reactivate_an_account_without_restoring_old_sessions()
    {
        await using var factory = new WorkflowApiFactory();
        using var original = AdminClient(factory);
        var created = await original.PostAsJsonAsync("/api/users", new UserAccountRequest("second@ovo.test", "İkinci yönetici", "Admin", WorkflowApiFactory.TestPassword));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(createdBody.TryGetProperty("passwordHash", out _));
        using var second = factory.CreateClient();
        var login = await second.PostAsJsonAsync("/api/auth/login", new { email = "second@ovo.test", password = WorkflowApiFactory.TestPassword });
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        second.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());
        var id = WorkflowApiFactory.AccountId("admin@ovo.test");
        Assert.Equal(HttpStatusCode.OK, (await original.PutAsJsonAsync($"/api/users/{id}", new UserAccountRequest("admin@ovo.test", "Yönetici", "Admin", null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await original.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.PutAsJsonAsync($"/api/users/{id}", new UserAccountRequest("admin@ovo.test", "Yönetici", "Admin", "", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await original.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await original.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditRecords.CountAsync(x => x.Action == "UserCreated"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task New_account_requires_a_password(string? password)
    {
        await using var factory = new WorkflowApiFactory();
        using var client = AdminClient(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new UserAccountRequest("new@ovo.test", "Yeni kişi", "Analyst", password))).StatusCode);
    }

    private static HttpClient AdminClient(WorkflowApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        return client;
    }
}
