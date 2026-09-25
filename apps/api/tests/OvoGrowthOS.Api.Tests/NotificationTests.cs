using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class NotificationTests
{
    private static readonly Guid Admin = WorkflowApiFactory.AccountId("admin@ovo.test");
    private static readonly Guid Partner = WorkflowApiFactory.AccountId("partner@ovo.test");
    private static WebApplicationFactory<Program> Setup(WorkflowApiFactory parent, AccountMailTests.Sender sender, bool enabled = true) => parent.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MAIL_ENABLED"] = enabled.ToString(), ["SMTP_USER"] = "sender@example.test", ["SMTP_PASS"] = "test-not-real",
            ["MAIL_FROM"] = "OVO <sender@example.test>", ["WebOrigin"] = "https://panel.example.test" }));
        b.ConfigureServices(s => { s.RemoveAll<IAccountMailSender>(); s.AddSingleton<IAccountMailSender>(sender); });
    });
    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    private static async Task Refresh(WebApplicationFactory<Program> f, Guid user, DateTimeOffset? now = null)
    { await using var s = f.Services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<NotificationService>().Refresh(user, now ?? DateTimeOffset.UtcNow); }
    private static async Task<bool> Send(WebApplicationFactory<Program> f)
    { await using var s = f.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<NotificationMailQueue>().ProcessOne(); }
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword }); r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()); return c;
    }
    private static async Task<Guid> TaskSeed(WebApplicationFactory<Program> f)
    {
        var task = new WorkTask { AssigneeId = Admin, Title = "Private task text", DueOn = TeamWork.Today(DateTimeOffset.UtcNow), BrandId = Guid.NewGuid() };
        await Db(f, async db => { db.Add(new Brand { Id = task.BrandId, Name = "Private brand" }); db.Add(task); await db.SaveChangesAsync(); }); return task.Id;
    }

    [Fact]
    public async Task Panel_configuration_gates_notifications_without_overriding_recipient_preferences()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(p, sender, false); using var c = await Client(f);
        var request = new OvoGrowthOS.Api.Features.MailSettingsRequest(true, "smtp.gmail.com", 465, true, "sender@example.test", "sender@example.test", "OVO", "abcdefghijklmnop", false, 0);
        (await c.PutAsJsonAsync("/api/account-mail/settings", request)).EnsureSuccessStatusCode();
        await TaskSeed(f); await Refresh(f, Admin); Assert.False(await Send(f)); // Per-recipient opt-in still required.
        await Db(f, async db => { (await db.NotificationPreferences.SingleAsync()).TaskDueEmail = true; db.WorkTasks.Add(new WorkTask { BrandId = (await db.Brands.SingleAsync()).Id, AssigneeId = Admin, Title = "Next task", DueOn = TeamWork.Today(DateTimeOffset.UtcNow) }); await db.SaveChangesAsync(); });
        await Refresh(f, Admin);
        (await c.PutAsJsonAsync("/api/account-mail/settings", request with { Revision = 1, Enabled = false, Password = null })).EnsureSuccessStatusCode();
        Assert.False(await Send(f)); Assert.Empty(sender.Messages);
        (await c.PutAsJsonAsync("/api/account-mail/settings", request with { Revision = 2, Password = null })).EnsureSuccessStatusCode();
        Assert.True(await Send(f)); Assert.Single(sender.Messages); Assert.Equal("admin@ovo.test", sender.Messages[0].To);
        Assert.DoesNotContain("Next task", sender.Messages[0].Body); Assert.False(await Send(f));
    }

    [Fact]
    public async Task Default_preferences_are_off_and_daily_event_is_idempotent_and_private()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(p, sender); using var c = await Client(f);
        await TaskSeed(f); var now = DateTimeOffset.UtcNow.Date.AddHours(12); await Refresh(f, Admin, now); await Refresh(f, Admin, now);
        await Db(f, async db => { Assert.Equal(2, await db.UserNotifications.CountAsync()); Assert.All(await db.UserNotifications.ToListAsync(), x => Assert.Null(x.EmailStatus)); });
        Assert.False(await Send(f));
        var body = await c.GetStringAsync("/api/notifications"); Assert.DoesNotContain("Private task", body); Assert.DoesNotContain("Private brand", body); Assert.DoesNotContain("accountVersion", body);
        using var other = await Client(f, "partner@ovo.test"); Guid id = Guid.Empty; await Db(f, async db => id = (await db.UserNotifications.FirstAsync()).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/notifications/{id}/read", null)).StatusCode);
        (await c.PostAsync($"/api/notifications/{id}/read", null)).EnsureSuccessStatusCode();
        await Db(f, async db => Assert.NotNull((await db.UserNotifications.SingleAsync(x => x.Id == id)).ReadAt));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("reassigned")]
    [InlineData("preference")]
    [InlineData("email")]
    [InlineData("complete")]
    public async Task Eligibility_is_rechecked_before_sending(string change)
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(p, sender); using var c = await Client(f);
        await Refresh(f, Admin); var taskId = await TaskSeed(f);
        await Db(f, async db => { (await db.NotificationPreferences.SingleAsync()).TaskDueEmail = true; await db.SaveChangesAsync(); });
        await Refresh(f, Admin);
        await Db(f, async db => {
            var user = await db.UserAccounts.SingleAsync(x => x.Id == Admin); var task = await db.WorkTasks.SingleAsync();
            if (change == "closed") user.IsActive = false;
            if (change == "reassigned") task.AssigneeId = Partner;
            if (change == "preference") (await db.NotificationPreferences.SingleAsync()).TaskDueEmail = false;
            if (change == "email") user.Email = "changed@example.test";
            if (change == "complete") task.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        Assert.True(await Send(f)); Assert.Empty(sender.Messages);
        await Db(f, async db => Assert.Equal(MailDeliveryStatus.Cancelled, (await db.UserNotifications.SingleAsync(x => x.SourceId == taskId)).EmailStatus));
    }

    [Fact]
    public async Task Ambiguous_delivery_is_not_retried_or_leaked_and_disabling_smtp_does_not_send()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender { Fail = true }; await using var f = Setup(p, sender); using var c = await Client(f);
        await Refresh(f, Admin); await TaskSeed(f);
        await Db(f, async db => { (await db.NotificationPreferences.SingleAsync()).TaskDueEmail = true; await db.SaveChangesAsync(); });
        await Refresh(f, Admin); Assert.True(await Send(f)); Assert.False(await Send(f)); Assert.Single(sender.Messages);
        Assert.DoesNotContain("Private", sender.Messages[0].Body); Assert.Equal("admin@ovo.test", sender.Messages[0].To);
        var body = await c.GetStringAsync("/api/notifications"); Assert.Contains("Uncertain", body); Assert.DoesNotContain("Pretend", body);
        await using var p2 = new WorkflowApiFactory(); await using var disabled = Setup(p2, sender, false); using var c2 = await Client(disabled);
        Assert.False(await Send(disabled));
    }

    [Fact]
    public async Task Portal_messages_are_account_scoped_and_revoked_reports_disappear()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(p, sender); using var admin = await Client(f);
        var client = Guid.NewGuid(); var other = Guid.NewGuid(); var brand = Guid.NewGuid(); var report = Guid.NewGuid(); var question = Guid.NewGuid();
        await Db(f, async db => {
            db.Add(new Brand { Id = brand, Name = "Secret brand" });
            foreach (var id in new[] { client, other }) { db.Add(new UserAccount { Id = id, Email = id+"@example.test", Role = "BrandClient", Name = "Client", PasswordHash = JwtTokenService.HashPassword(WorkflowApiFactory.TestPassword) }); db.Add(new PortalAccess { UserId = id, BrandId = brand }); }
            await db.SaveChangesAsync();
        });
        foreach (var id in new[] { client, other, Admin, Partner }) await Refresh(f, id);
        await Db(f, async db => {
            foreach (var pref in await db.NotificationPreferences.ToListAsync()) { pref.PortalMessagesEmail = true; pref.PortalReportsEmail = true; }
            db.Add(new PortalReport { Id = report, BrandId = brand });
            db.Add(new PortalQuestion { Id = question, BrandId = brand, ReportId = report, UserId = client, Question = "Private customer message", OwnerId = Partner, AnsweredAt = DateTimeOffset.UtcNow, Answer = "Private staff answer" });
            await db.SaveChangesAsync();
        });
        foreach (var id in new[] { client, other, Admin, Partner }) await Refresh(f, id);
        await Db(f, async db => {
            Assert.Equal(1, await db.UserNotifications.CountAsync(x => x.UserId == client && x.Kind == NotificationKind.PortalReply));
            Assert.False(await db.UserNotifications.AnyAsync(x => x.UserId == other && x.Kind == NotificationKind.PortalReply));
            Assert.False(await db.UserNotifications.AnyAsync(x => x.UserId == Admin));
            Assert.True(await db.UserNotifications.AnyAsync(x => x.UserId == Partner && x.Kind == NotificationKind.PortalQuestion));
            (await db.PortalReports.SingleAsync()).RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        });
        while (await Send(f)) { }
        Assert.Empty(sender.Messages);
        using var customer = await Client(f, client+"@example.test"); var list = await customer.GetFromJsonAsync<JsonElement>("/api/notifications"); Assert.Equal(0, list.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/account-mail/deliveries")).StatusCode);
    }

    [Fact]
    public async Task Preference_write_rejects_stale_version_and_does_not_backfill_email()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(p, sender); using var c = await Client(f);
        await TaskSeed(f); await Refresh(f, Admin);
        var body = new { DailyTasksEmail = true, TaskDueEmail = true, PortalMessagesEmail = false, PortalReportsEmail = false, Revision = 1 };
        (await c.PutAsJsonAsync("/api/notifications/preferences", body)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync("/api/notifications/preferences", body)).StatusCode);
        await Refresh(f, Admin); Assert.False(await Send(f));
    }

    [Fact]
    public async Task Daily_summary_starts_at_nine_in_Turkey_and_stale_claim_is_not_sent_again()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender(); await using var f = Setup(p, sender); using var c = await Client(f);
        await TaskSeed(f);
        var day = DateTimeOffset.UtcNow.Date;
        await Refresh(f, Admin, new DateTimeOffset(day.AddHours(5).AddMinutes(59), TimeSpan.Zero));
        await Db(f, async db => Assert.False(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.DailyTasks)));
        await Refresh(f, Admin, new DateTimeOffset(day.AddHours(6), TimeSpan.Zero));
        await Db(f, async db => {
            var n = await db.UserNotifications.SingleAsync(x => x.Kind == NotificationKind.DailyTasks);
            n.EmailStatus = MailDeliveryStatus.Sending; n.AttemptedAt = DateTimeOffset.UtcNow.AddMinutes(-6); await db.SaveChangesAsync();
        });
        Assert.False(await Send(f)); Assert.Empty(sender.Messages);
        await Db(f, async db => Assert.Equal(MailDeliveryStatus.Uncertain, (await db.UserNotifications.SingleAsync(x => x.Kind == NotificationKind.DailyTasks)).EmailStatus));
    }
}
