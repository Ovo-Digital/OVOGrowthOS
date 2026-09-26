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

public sealed class ScheduledReportMailTests
{
    private static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, AccountMailTests.Sender sender) => parent.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MAIL_ENABLED"] = "true", ["SMTP_USER"] = "sender@example.test", ["SMTP_PASS"] = "test-only",
            ["MAIL_FROM"] = "OVO <sender@example.test>", ["WebOrigin"] = "https://panel.example.test" }));
        b.ConfigureServices(s => { s.RemoveAll<IAccountMailSender>(); s.AddSingleton<IAccountMailSender>(sender); });
    });
    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    private static async Task Refresh(WebApplicationFactory<Program> f, Guid user, DateTimeOffset? now = null)
    { await using var s = f.Services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<NotificationService>().Refresh(user, now ?? DateTimeOffset.UtcNow); }
    private static async Task<bool> Send(WebApplicationFactory<Program> f)
    { await using var s = f.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<NotificationMailQueue>().ProcessOne(); }
    private static async Task RunDue(WebApplicationFactory<Program> f, DateTimeOffset? now = null)
    { await using var s = f.Services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<ScheduledReportQueue>().RunDue(now ?? DateTimeOffset.UtcNow); }
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword }); r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()); return c;
    }

    private static (int Day, int Hour, ReportScheduleWindow Window) OpenWindow()
    {
        var now = DateTimeOffset.UtcNow; var local = TimeZoneInfo.ConvertTime(now, Turkey);
        var window = ReportMailSchedule.CurrentWindow(now, local.Day, local.Hour);
        Assert.NotNull(window);
        return (local.Day, local.Hour, window);
    }

    private static async Task<(Guid Brand, Guid User)> Seed(WebApplicationFactory<Program> f, int year, int month,
        MonthlyPerformanceStatus status = MonthlyPerformanceStatus.Locked, DateTimeOffset? publishedAt = null, DateTimeOffset? startedAt = null)
    {
        var brand = Guid.NewGuid(); var user = Guid.NewGuid();
        await Db(f, async db =>
        {
            db.Add(new Brand { Id = brand, Name = "Lale" });
            var deal = new Deal { BrandId = brand, Name = "Anlaşma", Status = DealStatus.Active };
            db.Add(deal);
            var performance = new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = year, Month = month, Status = status };
            db.Add(performance);
            db.Add(new UserAccount { Id = user, Email = user + "@example.test", Name = "Marka yetkilisi", Role = "BrandClient", PasswordHash = JwtTokenService.HashPassword(WorkflowApiFactory.TestPassword) });
            db.Add(new PortalAccess { BrandId = brand, UserId = user });
            db.Add(new NotificationPreference { UserId = user, PortalReportsEmail = true, StartedAt = startedAt ?? DateTimeOffset.UtcNow.AddDays(-10) });
            db.Add(new PortalReport
            {
                BrandId = brand, PerformanceId = performance.Id, Year = year, Month = month, Version = 1,
                SnapshotJson = "PRIVATE FINANCIAL SNAPSHOT", PublishedAt = publishedAt ?? DateTimeOffset.UtcNow.AddDays(-2)
            });
            await db.SaveChangesAsync();
        });
        return (brand, user);
    }

    private static string Root(Guid brand) => $"/api/portal-management/brands/{brand}/email-policy";
    private static BrandMailPolicyRequest Request(int day, int hour, bool scheduled = true, int revision = 0) =>
        new(true, "{marka} · {donem}", "Merhaba {marka}: {donem}\n{baglanti}", "Aylık gönderim onayı", revision, scheduled, day, hour);

    [Fact]
    public async Task Schedule_settings_default_off_are_validated_and_audited()
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new());
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var brand = Guid.NewGuid(); await Db(f, async db => { db.Add(new Brand { Id = brand, Name = "Lale" }); await db.SaveChangesAsync(); });
        var initial = await c.GetFromJsonAsync<JsonElement>(Root(brand));
        Assert.False(initial.GetProperty("scheduledReportEnabled").GetBoolean());
        Assert.Equal(5, initial.GetProperty("scheduledSendDay").GetInt32());
        Assert.Equal(9, initial.GetProperty("scheduledSendHour").GetInt32());
        Assert.False(initial.GetProperty("schedule").GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Root(brand), Request(0, hour))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Root(brand), Request(day, 24))).StatusCode);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();
        var saved = await c.GetFromJsonAsync<JsonElement>(Root(brand));
        Assert.True(saved.GetProperty("scheduledReportEnabled").GetBoolean());
        Assert.Equal(day, saved.GetProperty("scheduledSendDay").GetInt32());
        Assert.Equal(hour, saved.GetProperty("scheduledSendHour").GetInt32());
        Assert.True(saved.GetProperty("schedule").GetProperty("enabled").GetBoolean());
        Assert.Equal(window.TargetMonth, saved.GetProperty("schedule").GetProperty("targetMonth").GetInt32());
        Assert.False(saved.GetProperty("schedule").GetProperty("targetReportPublished").GetBoolean());
        await Db(f, async db =>
        {
            var audit = await db.AuditRecords.SingleAsync(x => x.Action == "BrandMailPolicyChanged");
            Assert.Contains("scheduledReportEnabled", audit.NewValueJson);
        });
    }

    [Fact]
    public async Task Scheduled_window_defers_the_instant_mail_and_delivers_it_exactly_once()
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender);
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var (brand, user) = await Seed(f, window.TargetYear, window.TargetMonth);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();

        await Refresh(f, user);
        await Db(f, async db => Assert.Null((await db.UserNotifications.SingleAsync()).EmailStatus));

        await RunDue(f);
        await Db(f, async db => Assert.Equal(MailDeliveryStatus.Pending, (await db.UserNotifications.SingleAsync()).EmailStatus));
        await RunDue(f);
        await Db(f, async db => Assert.Single(await db.UserNotifications.ToListAsync()));

        Assert.True(await Send(f)); Assert.False(await Send(f));
        Assert.Single(sender.Messages);
        Assert.Contains($"{window.TargetMonth:00}/{window.TargetYear}", sender.Messages[0].Body);
        Assert.Contains("https://panel.example.test/portal", sender.Messages[0].Body);
        Assert.DoesNotContain("PRIVATE", sender.Messages[0].Body);
        await Db(f, async db => Assert.Equal(MailDeliveryStatus.Sent, (await db.UserNotifications.SingleAsync()).EmailStatus));
    }

    [Fact]
    public async Task Schedule_reaches_a_recipient_that_the_instant_notification_never_materialised()
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender);
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var (brand, user) = await Seed(f, window.TargetYear, window.TargetMonth,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-2), startedAt: DateTimeOffset.UtcNow.AddDays(-1));
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();

        await Refresh(f, user);
        await Db(f, async db => Assert.Empty(await db.UserNotifications.ToListAsync()));
        await RunDue(f);
        await Db(f, async db => Assert.Equal(MailDeliveryStatus.Pending, (await db.UserNotifications.SingleAsync()).EmailStatus));
        Assert.True(await Send(f));
        Assert.Single(sender.Messages);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("older-period")]
    [InlineData("open-period")]
    [InlineData("revoked")]
    public async Task Schedule_is_silent_for_older_open_or_revoked_periods_and_when_disabled(string change)
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender);
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var (targetYear, targetMonth) = change == "older-period"
            ? ReportMailSchedule.Previous(window.TargetYear, window.TargetMonth)
            : (window.TargetYear, window.TargetMonth);
        var status = change == "open-period" ? MonthlyPerformanceStatus.Draft : MonthlyPerformanceStatus.Locked;
        var (brand, _) = await Seed(f, targetYear, targetMonth, status);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour, scheduled: change != "disabled"))).EnsureSuccessStatusCode();
        if (change == "revoked")
            await Db(f, async db => { (await db.PortalReports.SingleAsync()).RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); });

        await RunDue(f);
        await Db(f, async db => Assert.Empty(await db.UserNotifications.ToListAsync()));
        Assert.False(await Send(f));
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Nothing_is_materialised_for_a_run_far_outside_the_catch_up_window()
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new());
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var (brand, _) = await Seed(f, window.TargetYear, window.TargetMonth);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();
        await RunDue(f, DateTimeOffset.UtcNow.AddDays(-40));
        await Db(f, async db => Assert.Empty(await db.UserNotifications.ToListAsync()));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("preference")]
    [InlineData("policy")]
    [InlineData("account")]
    public async Task Current_rules_are_rechecked_before_the_scheduled_mail_leaves(string change)
    {
        await using var parent = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(parent, sender);
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var (brand, user) = await Seed(f, window.TargetYear, window.TargetMonth);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();
        await RunDue(f);
        await Db(f, async db =>
        {
            Assert.Equal(MailDeliveryStatus.Pending, (await db.UserNotifications.SingleAsync()).EmailStatus);
            if (change == "revoked") (await db.PortalReports.SingleAsync()).RevokedAt = DateTimeOffset.UtcNow;
            if (change == "preference") (await db.NotificationPreferences.SingleAsync()).PortalReportsEmail = false;
            if (change == "policy") (await db.BrandMailPolicies.SingleAsync()).ReportEmailEnabled = false;
            if (change == "account") (await db.UserAccounts.SingleAsync(x => x.Id == user)).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.True(await Send(f)); Assert.False(await Send(f));
        Assert.Empty(sender.Messages);
        await Db(f, async db => Assert.Equal(MailDeliveryStatus.Cancelled, (await db.UserNotifications.SingleAsync()).EmailStatus));
    }

    [Fact]
    public async Task Preview_marks_the_scheduled_period_as_waiting_instead_of_expired()
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new());
        using var c = await Client(f); var (day, hour, window) = OpenWindow();
        var (brand, _) = await Seed(f, window.TargetYear, window.TargetMonth);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();
        var reportId = Guid.Empty;
        await Db(f, async db => { reportId = (await db.PortalReports.SingleAsync()).Id; });
        var preview = await c.GetFromJsonAsync<JsonElement>(Root(brand) + "/preview/" + reportId);
        var recipient = preview.GetProperty("recipients")[0];
        Assert.True(recipient.GetProperty("eligible").GetBoolean());
        Assert.True(recipient.GetProperty("deferred").GetBoolean());
        Assert.NotNull(recipient.GetProperty("scheduledFor").GetString());
        Assert.Equal(0, recipient.GetProperty("reasons").GetArrayLength());
        Assert.Contains("zamanlanmış gönderime", preview.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Customer_role_cannot_read_or_change_the_schedule()
    {
        await using var parent = new WorkflowApiFactory(); await using var f = Setup(parent, new());
        using var c = await Client(f); var (day, hour, _) = OpenWindow();
        var (brand, user) = await Seed(f, 2026, 8);
        (await c.PutAsJsonAsync(Root(brand), Request(day, hour))).EnsureSuccessStatusCode();
        using var customer = await Client(f, user + "@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(Root(brand))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PutAsJsonAsync(Root(brand), Request(day, hour))).StatusCode);
    }
}
