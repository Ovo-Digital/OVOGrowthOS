using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class MailSettingsTests
{
    private const string Secret = "abcdefghijklmnop";
    public sealed class TestSender : ISmtpTestSender
    {
        public List<string> Recipients { get; } = [];
        public bool Fail { get; set; }
        public Task SendAsync(SmtpSettings settings, string recipient, CancellationToken ct)
        {
            Assert.True(settings.Configured); Recipients.Add(recipient);
            if (Fail) throw new IOException("Secret server reply " + Secret);
            return Task.CompletedTask;
        }
    }
    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, TestSender sender, bool force = false, bool envEnabled = false) => parent.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MAIL_ENABLED"] = envEnabled.ToString(), ["MAIL_FORCE_DISABLED"] = force.ToString(), ["SMTP_USER"] = "env@example.test",
            ["SMTP_PASS"] = "server-only-password", ["MAIL_FROM"] = "OVO <env@example.test>", ["WebOrigin"] = "https://panel.example.test" }));
        b.ConfigureServices(s => { s.RemoveAll<ISmtpTestSender>(); s.AddSingleton<ISmtpTestSender>(sender); s.RemoveAll<IAccountMailSender>(); s.AddSingleton<IAccountMailSender>(new AccountMailTests.Sender()); });
    });
    private static MailSettingsRequest Draft(int revision = 0) => new(false, "smtp.gmail.com", 465, true, "sender@example.test", "sender@example.test", "OVO Digital", Secret, false, revision);
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string role = "admin")
    {
        var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode(); c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()); return c;
    }
    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> work)
    { await using var scope = f.Services.CreateAsyncScope(); await work(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    [Fact]
    public async Task Panel_secret_is_encrypted_write_only_and_audit_never_contains_credentials()
    {
        await using var p = new WorkflowApiFactory(); var sender = new TestSender(); await using var f = Setup(p, sender); using var c = await Client(f);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft() with { Password = "abcd efgh ijkl mnop" })).EnsureSuccessStatusCode();
        var result = await c.GetAsync("/api/account-mail/settings"); var text = await result.Content.ReadAsStringAsync();
        Assert.True(result.Headers.CacheControl?.NoStore); Assert.DoesNotContain(Secret, text); Assert.DoesNotContain("protectedPassword", text); Assert.Contains("\"passwordStored\":true", text);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.MailConfigurations.SingleAsync();
        Assert.DoesNotContain(Secret, row.ProtectedPassword); Assert.Equal(Secret, scope.ServiceProvider.GetRequiredService<SmtpSettingsProvider>().Unprotect(row.ProtectedPassword));
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(await db.AuditRecords.ToListAsync())); Assert.Empty(sender.Recipients);
    }

    [Theory]
    [InlineData("partner")][InlineData("analyst")]
    public async Task Only_admin_can_read_save_or_test(string role)
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new()); using var c = await Client(f, role);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/account-mail/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync("/api/account-mail/settings", Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/account-mail/settings/test", new MailTestRequest(0, true))).StatusCode);
    }

    [Fact]
    public async Task Blank_password_preserves_remove_disables_and_stale_version_is_rejected()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new()); using var c = await Client(f);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft() with { Enabled = true })).EnsureSuccessStatusCode();
        string original = ""; await Db(f, async db => original = (await db.MailConfigurations.SingleAsync()).ProtectedPassword);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(1) with { Password = null, FromName = "Yeni ad" })).EnsureSuccessStatusCode();
        await Db(f, async db => Assert.Equal(original, (await db.MailConfigurations.SingleAsync()).ProtectedPassword));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(1))).StatusCode);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(2) with { Password = null, RemovePassword = true })).EnsureSuccessStatusCode();
        await Db(f, async db => { var row = await db.MailConfigurations.SingleAsync(); Assert.Empty(row.ProtectedPassword); Assert.False(row.Enabled); });
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(3) with { Password = null, Enabled = true })).StatusCode);
    }

    [Theory]
    [InlineData("host")][InlineData("port")][InlineData("tls")][InlineData("password")][InlineData("header")][InlineData("address")]
    public async Task Unsafe_or_incomplete_configuration_is_rejected(string fault)
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new()); using var c = await Client(f);
        var request = fault switch { "host" => Draft() with { Host = "127.0.0.1" }, "port" => Draft() with { Port = 25 }, "tls" => Draft() with { Secure = false },
            "password" => Draft() with { Password = "normal-password" }, "header" => Draft() with { FromName = "OVO\r\nBcc: attacker@example.test" }, _ => Draft() with { FromAddress = "A <a@example.test>" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/account-mail/settings", request)).StatusCode);
        await Db(f, async db => Assert.Empty(await db.MailConfigurations.ToListAsync()));
    }

    [Fact]
    public async Task Changing_sender_requires_replacement_secret_and_587_is_supported()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new()); using var c = await Client(f);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(1) with { User = "new@example.test", Password = null })).StatusCode);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(1) with { Port = 587, Secure = false, Enabled = true })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Test_requires_confirmation_uses_only_current_admin_and_is_not_automatically_retried()
    {
        await using var p = new WorkflowApiFactory(); var sender = new TestSender { Fail = true }; await using var f = Setup(p, sender); using var c = await Client(f);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/account-mail/settings/test", new MailTestRequest(1, false))).StatusCode);
        var result = await c.PostAsJsonAsync("/api/account-mail/settings/test", new { revision = 1, confirm = true, recipient = "stranger@example.test" });
        result.EnsureSuccessStatusCode(); var text = await result.Content.ReadAsStringAsync(); Assert.Contains("\"accepted\":false", text); Assert.DoesNotContain(Secret, text);
        Assert.Equal("admin@ovo.test", Assert.Single(sender.Recipients));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/account-mail/settings/test", new MailTestRequest(1, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/account-mail/settings/test", new MailTestRequest(2, true))).StatusCode);
        await Db(f, async db => { Assert.False((await db.MailConfigurations.SingleAsync()).Enabled); Assert.DoesNotContain(Secret, JsonSerializer.Serialize(await db.AuditRecords.ToListAsync())); });
    }

    [Fact]
    public async Task Environment_works_until_panel_save_and_force_disable_blocks_both_normal_and_test_mail()
    {
        await using var p = new WorkflowApiFactory(); var sender = new TestSender(); await using var f = Setup(p, sender, force: true, envEnabled: true); using var c = await Client(f);
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft() with { Enabled = true })).EnsureSuccessStatusCode();
        var status = await c.GetStringAsync("/api/account-mail/status"); Assert.Contains("\"ready\":false", status);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/account-mail/settings/test", new MailTestRequest(1, true))).StatusCode); Assert.Empty(sender.Recipients);
        using var anonymous = f.CreateClient(); (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = "partner@ovo.test" })).EnsureSuccessStatusCode();
        await Db(f, async db => Assert.Empty(await db.MailDeliveries.ToListAsync()));
    }

    [Fact]
    public async Task Panel_takes_precedence_for_existing_account_flows_and_missing_key_fails_closed()
    {
        await using var p = new WorkflowApiFactory(); await using var f = Setup(p, new(), envEnabled: true); using var c = await Client(f);
        Assert.Contains("\"ready\":true", await c.GetStringAsync("/api/account-mail/status"));
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft())).EnsureSuccessStatusCode();
        Assert.Contains("\"ready\":false", await c.GetStringAsync("/api/account-mail/status"));
        (await c.PutAsJsonAsync("/api/account-mail/settings", Draft(1) with { Enabled = true, Password = null })).EnsureSuccessStatusCode();
        using var anonymous = f.CreateClient(); (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = "partner@ovo.test" })).EnsureSuccessStatusCode();
        await Db(f, async db => { Assert.Single(await db.MailDeliveries.ToListAsync()); (await db.MailConfigurations.SingleAsync()).ProtectedPassword = "lost-key"; await db.SaveChangesAsync(); });
        Assert.Contains("\"ready\":false", await c.GetStringAsync("/api/account-mail/status"));
        await using var scope = f.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AccountMailQueue>().ProcessOne());
    }
}
