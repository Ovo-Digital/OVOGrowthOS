using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class BrandMailPolicyTests
{
    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, AccountMailTests.Sender sender) => parent.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MAIL_ENABLED"] = "true", ["SMTP_USER"] = "sender@example.test", ["SMTP_PASS"] = "test-only",
            ["MAIL_FROM"] = "OVO <sender@example.test>", ["WebOrigin"] = "https://panel.example.test" }));
        b.ConfigureServices(s => { s.RemoveAll<IAccountMailSender>(); s.AddSingleton<IAccountMailSender>(sender); });
    });
    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    private static async Task Refresh(WebApplicationFactory<Program> f, Guid user)
    { await using var s = f.Services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<NotificationService>().Refresh(user, DateTimeOffset.UtcNow); }
    private static async Task<bool> Send(WebApplicationFactory<Program> f)
    { await using var s = f.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<NotificationMailQueue>().ProcessOne(); }
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword }); r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()); return c;
    }
    private static async Task<(Guid Brand, Guid User, Guid Report)> Seed(WebApplicationFactory<Program> f)
    {
        var brand = Guid.NewGuid(); var user = Guid.NewGuid(); var report = Guid.NewGuid();
        await Db(f, async db =>
        {
            db.Add(new Brand { Id = brand, Name = "Lale" });
            db.Add(new UserAccount { Id = user, Email = user + "@example.test", Name = "Marka yetkilisi", Role = "BrandClient", PasswordHash = JwtTokenService.HashPassword(WorkflowApiFactory.TestPassword) });
            db.Add(new PortalAccess { BrandId = brand, UserId = user });
            db.Add(new NotificationPreference { UserId = user, PortalReportsEmail = true, StartedAt = DateTimeOffset.UtcNow.AddDays(-1) });
            db.Add(new PortalReport { Id = report, BrandId = brand, Year = 2026, Month = 9, Version = 1, SnapshotJson = "PRIVATE FINANCIAL SNAPSHOT" });
            await db.SaveChangesAsync();
        });
        return (brand, user, report);
    }
    private static string Root(Guid brand) => $"/api/portal-management/brands/{brand}/email-policy";
    private static BrandMailPolicyRequest Request(bool enabled = true, int revision = 0) => new(enabled, "{marka} · {donem}", "Merhaba {marka}: {donem}\n{baglanti}", "İletişim planı onayı", revision);

    [Fact]
    public async Task Policy_defaults_off_requires_reason_preserves_preferences_and_rejects_stale_write()
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender); using var c = await Client(f); var seed = await Seed(f);
        var initial = await c.GetFromJsonAsync<JsonElement>(Root(seed.Brand)); Assert.False(initial.GetProperty("reportEmailEnabled").GetBoolean()); Assert.Equal(0, initial.GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Root(seed.Brand), Request() with { Reason = "" })).StatusCode);
        (await c.PutAsJsonAsync(Root(seed.Brand), Request())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Root(seed.Brand), Request())).StatusCode);
        await Db(f, async db => { Assert.True((await db.NotificationPreferences.SingleAsync()).PortalReportsEmail); Assert.Equal(1, (await db.BrandMailPolicies.SingleAsync()).Revision); Assert.Contains(await db.AuditRecords.ToListAsync(), x => x.Action == "BrandMailPolicyChanged"); });
        Assert.Empty(sender.Messages); Assert.False(await Send(f));
    }

    [Theory]
    [InlineData("partner@ovo.test")]
    [InlineData("analyst@ovo.test")]
    [InlineData("customer")]
    public async Task Non_admin_cannot_read_write_or_preview(string email)
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new()); using var admin = await Client(f); var seed = await Seed(f);
        using var c = await Client(f, email == "customer" ? seed.User + "@example.test" : email);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Root(seed.Brand))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync(Root(seed.Brand), Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Root(seed.Brand) + "/preview/" + seed.Report)).StatusCode);
    }

    [Theory]
    [InlineData("{kar}", "{baglanti}")]
    [InlineData("Konu\nBcc: other@example.test", "{baglanti}")]
    [InlineData("Konu", "<b>{baglanti}</b>")]
    [InlineData("Konu", "https://other.test {baglanti}")]
    [InlineData("Konu", "Bağlantısız metin")]
    [InlineData("{baglanti}", "{baglanti}")]
    public async Task Unsafe_or_unknown_template_is_rejected(string subject, string body)
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new()); using var c = await Client(f); var seed = await Seed(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Root(seed.Brand), Request() with { SubjectTemplate = subject, BodyTemplate = body })).StatusCode);
    }

    [Fact]
    public async Task Preview_is_brand_scoped_read_only_and_explains_each_exclusion()
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender); using var c = await Client(f); var a = await Seed(f); var b = await Seed(f);
        var response = await c.GetAsync(Root(a.Brand) + "/preview/" + a.Report); response.EnsureSuccessStatusCode(); Assert.True(response.Headers.CacheControl?.NoStore);
        var raw = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(b.User.ToString(), raw); Assert.DoesNotContain("PRIVATE", raw);
        Assert.Contains("Markanın rapor", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recipients")[0].GetProperty("reasons")[0].GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(Root(a.Brand) + "/preview/" + b.Report)).StatusCode);
        (await c.PutAsJsonAsync(Root(a.Brand), Request())).EnsureSuccessStatusCode();
        var preview = await c.GetFromJsonAsync<JsonElement>(Root(a.Brand) + "/preview/" + a.Report); Assert.True(preview.GetProperty("recipients")[0].GetProperty("eligible").GetBoolean()); Assert.Equal("Lale · 09/2026", preview.GetProperty("subject").GetString());
        await Db(f, async db => {
            var user = await db.UserAccounts.SingleAsync(x => x.Id == a.User); user.IsActive = false; user.InvitationPending = true;
            (await db.NotificationPreferences.SingleAsync(x => x.UserId == a.User)).PortalReportsEmail = false;
            (await db.PortalReports.SingleAsync(x => x.Id == a.Report)).RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        });
        preview = await c.GetFromJsonAsync<JsonElement>(Root(a.Brand) + "/preview/" + a.Report); Assert.Equal(4, preview.GetProperty("recipients")[0].GetProperty("reasons").GetArrayLength());
        Assert.Empty(sender.Messages); await Db(f, async db => Assert.Empty(await db.UserNotifications.ToListAsync()));
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("preference")]
    [InlineData("access")]
    [InlineData("revoked")]
    [InlineData("active")]
    [InlineData("send")]
    public async Task Queued_mail_rechecks_current_rules_and_uses_saved_safe_template(string change)
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender); using var c = await Client(f); var a = await Seed(f);
        (await c.PutAsJsonAsync(Root(a.Brand), Request())).EnsureSuccessStatusCode(); await Refresh(f, a.User);
        await Db(f, async db => {
            Assert.Equal(MailDeliveryStatus.Pending, (await db.UserNotifications.SingleAsync()).EmailStatus);
            if (change == "policy") (await db.BrandMailPolicies.SingleAsync()).ReportEmailEnabled = false;
            if (change == "preference") (await db.NotificationPreferences.SingleAsync()).PortalReportsEmail = false;
            if (change == "access") db.PortalAccesses.Remove(await db.PortalAccesses.SingleAsync());
            if (change == "revoked") (await db.PortalReports.SingleAsync()).RevokedAt = DateTimeOffset.UtcNow;
            if (change == "active") (await db.UserAccounts.SingleAsync(x => x.Id == a.User)).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.True(await Send(f)); Assert.False(await Send(f));
        if (change == "send") { Assert.Single(sender.Messages); Assert.Contains("09/2026", sender.Messages[0].Body); Assert.Contains("https://panel.example.test/portal", sender.Messages[0].Body); Assert.DoesNotContain("PRIVATE", sender.Messages[0].Body); }
        else { Assert.Empty(sender.Messages); await Db(f, async db => Assert.Equal(MailDeliveryStatus.Cancelled, (await db.UserNotifications.SingleAsync()).EmailStatus)); }
    }

    [Fact]
    public async Task Opening_policy_does_not_backfill_or_override_opt_out_and_in_app_report_remains_visible()
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender); using var c = await Client(f); var a = await Seed(f);
        await Refresh(f, a.User); await Db(f, async db => Assert.Null((await db.UserNotifications.SingleAsync()).EmailStatus));
        (await c.PutAsJsonAsync(Root(a.Brand), Request())).EnsureSuccessStatusCode(); await Refresh(f, a.User); Assert.False(await Send(f));
        using var customer = await Client(f, a.User + "@example.test"); var list = await customer.GetFromJsonAsync<JsonElement>("/api/notifications"); Assert.Single(list.GetProperty("items").EnumerateArray());
        var preview = await c.GetFromJsonAsync<JsonElement>(Root(a.Brand) + "/preview/" + a.Report); Assert.False(preview.GetProperty("recipients")[0].GetProperty("eligible").GetBoolean());
        await Db(f, async db => { (await db.NotificationPreferences.SingleAsync()).PortalReportsEmail = false; db.Add(new PortalReport { BrandId = a.Brand, Year = 2026, Month = 9, Version = 2 }); await db.SaveChangesAsync(); });
        await Refresh(f, a.User); Assert.False(await Send(f)); Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(MailDeliveryStatus.Sent)]
    [InlineData(MailDeliveryStatus.Sending)]
    [InlineData(MailDeliveryStatus.Cancelled)]
    [InlineData(MailDeliveryStatus.Uncertain)]
    public async Task Preview_does_not_offer_already_processed_notifications(MailDeliveryStatus status)
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new()); using var c = await Client(f); var a = await Seed(f);
        (await c.PutAsJsonAsync(Root(a.Brand), Request())).EnsureSuccessStatusCode(); await Refresh(f, a.User);
        await Db(f, async db => { (await db.UserNotifications.SingleAsync()).EmailStatus = status; await db.SaveChangesAsync(); });
        var preview = await c.GetFromJsonAsync<JsonElement>(Root(a.Brand) + "/preview/" + a.Report);
        Assert.False(preview.GetProperty("recipients")[0].GetProperty("eligible").GetBoolean()); Assert.Single(preview.GetProperty("recipients")[0].GetProperty("reasons").EnumerateArray());
    }

    [Fact]
    public async Task Preview_explains_global_pause_and_old_report_without_creating_a_delivery()
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new()); using var c = await Client(f); var a = await Seed(f);
        (await c.PutAsJsonAsync(Root(a.Brand), Request())).EnsureSuccessStatusCode();
        await Db(f, async db => { db.Add(new MailConfiguration()); (await db.PortalReports.SingleAsync()).PublishedAt = DateTimeOffset.UtcNow.AddDays(-2); await db.SaveChangesAsync(); });
        var preview = await c.GetFromJsonAsync<JsonElement>(Root(a.Brand) + "/preview/" + a.Report);
        var reasons = preview.GetProperty("recipients")[0].GetProperty("reasons").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(reasons, x => x!.StartsWith("Genel")); Assert.Contains(reasons, x => x!.Contains("24 saat"));
        await Db(f, async db => Assert.Empty(await db.UserNotifications.ToListAsync()));
    }

    [Fact]
    public void Template_rendering_does_not_expand_brand_text_as_another_variable()
    {
        Assert.Equal("Lale{baglanti} · 09/2026", ReportMailTemplate.Render("{marka} · {donem}", "Lale\r\n{baglanti}", "09/2026", "https://panel.test/portal"));
        Assert.False(ReportMailTemplate.Valid(new string('a', 181), "{baglanti}"));
        Assert.False(ReportMailTemplate.Valid("Konu", new string('a', 2000) + "{baglanti}"));
    }
}
