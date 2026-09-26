using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class MailCenterTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f, string role = "admin")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }
    private static async Task Db(WorkflowApiFactory f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    private static async Task SeedAccountMail(WorkflowApiFactory f, MailDeliveryStatus status, string purpose = "Invitation")
    {
        var userId = WorkflowApiFactory.AccountId("analyst@ovo.test");
        await Db(f, async db =>
        {
            var link = new AccountLink
            {
                UserId = userId, Email = "analyst@ovo.test", AccountVersion = 0,
                Purpose = purpose == "Invitation" ? AccountLinkPurpose.Invitation : AccountLinkPurpose.PasswordReset,
                TokenHash = "protected-token-hash-must-not-leak", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            };
            db.AccountLinks.Add(link);
            db.MailDeliveries.Add(new MailDelivery { UserId = userId, AccountLinkId = link.Id, ProtectedBody = "#token=SECRET-PROTECTED-BODY", Status = status });
            await db.SaveChangesAsync();
        });
    }
    private static async Task<Guid> SeedNotification(WorkflowApiFactory f, MailDeliveryStatus status, string email = "analyst@ovo.test", int accountVersion = 0)
    {
        var id = Guid.NewGuid();
        await Db(f, async db =>
        {
            db.UserNotifications.Add(new UserNotification
            {
                Id = id, UserId = WorkflowApiFactory.AccountId("analyst@ovo.test"), Kind = NotificationKind.PortalReport,
                EventKey = $"mail-center-{id}", Email = email, AccountVersion = accountVersion, EmailStatus = status,
                ErrorCode = status == MailDeliveryStatus.Cancelled ? "NoLongerEligible" : ""
            });
            await db.SaveChangesAsync();
        });
        return id;
    }

    [Fact]
    public async Task Mail_center_merges_sources_filters_by_type_and_status_and_never_returns_protected_content()
    {
        await using var f = new WorkflowApiFactory(); using var admin = await Client(f);
        await SeedAccountMail(f, MailDeliveryStatus.Sent);
        var cancelled = await SeedNotification(f, MailDeliveryStatus.Cancelled);
        await Db(f, async db =>
        {
            db.AuditRecords.Add(new AuditRecord { UserId = "admin@ovo.test", Action = "MailTestRequested", EntityType = "Mail", EntityId = Guid.NewGuid().ToString() });
            await db.SaveChangesAsync();
        });

        var all = await (await admin.GetAsync("/api/mail-center/messages")).Content.ReadAsStringAsync();
        foreach (var expected in new[] { "invitation", "notification", "test", "E-posta sunucusu kabul etti", "Gönderimden önce kurallar değişti" }) Assert.Contains(expected, all);
        foreach (var secret in new[] { "SECRET-PROTECTED-BODY", "protected-token-hash", "protectedBody", "ProtectedBody" }) Assert.DoesNotContain(secret, all);
        Assert.Contains("gerçek kullanım verisiyle", all);

        var json = JsonDocument.Parse(all).RootElement;
        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("summary").GetProperty("Sent").GetInt32());
        Assert.Equal(1, json.GetProperty("summary").GetProperty("Cancelled").GetInt32());
        Assert.Equal(1, json.GetProperty("summary").GetProperty("Pending").GetInt32());

        var notifications = JsonDocument.Parse(await admin.GetStringAsync("/api/mail-center/messages?type=report")).RootElement;
        Assert.Equal(1, notifications.GetProperty("total").GetInt32());
        Assert.Equal(cancelled, notifications.GetProperty("items")[0].GetProperty("id").GetGuid());

        var sent = JsonDocument.Parse(await admin.GetStringAsync("/api/mail-center/messages?status=Sent")).RootElement;
        Assert.Equal(1, sent.GetProperty("total").GetInt32());
        Assert.Equal("invitation", sent.GetProperty("items")[0].GetProperty("type").GetString());

        Assert.Equal(0, JsonDocument.Parse(await admin.GetStringAsync("/api/mail-center/messages?q=nobody@example.test")).RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, JsonDocument.Parse(await admin.GetStringAsync("/api/mail-center/messages?type=daily")).RootElement.GetProperty("total").GetInt32());

        var after = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var window = JsonDocument.Parse(await admin.GetStringAsync($"/api/mail-center/messages?from={after}")).RootElement;
        Assert.Equal(0, window.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Mail_center_is_admin_only_for_reading_and_resending()
    {
        await using var f = new WorkflowApiFactory(); using var admin = await Client(f);
        var id = await SeedNotification(f, MailDeliveryStatus.Uncertain);
        foreach (var role in new[] { "partner", "analyst" })
        {
            using var staff = await Client(f, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/mail-center/messages")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/mail-center/notifications/{id}/resend", new MailCenterResendRequest("Gerekçe"))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/mail-center/messages")).StatusCode);
    }

    [Fact]
    public async Task Resend_needs_a_reason_never_doubles_a_send_and_rechecks_the_recipient()
    {
        await using var f = new WorkflowApiFactory(); using var admin = await Client(f);
        var id = await SeedNotification(f, MailDeliveryStatus.Cancelled);
        var sent = await SeedNotification(f, MailDeliveryStatus.Sent);
        var changedRecipient = await SeedNotification(f, MailDeliveryStatus.Uncertain, email: "moved@example.test");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/mail-center/notifications/{id}/resend", new MailCenterResendRequest(""))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/mail-center/notifications/{Guid.NewGuid()}/resend", new MailCenterResendRequest("Gerekçe"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/mail-center/notifications/{sent}/resend", new MailCenterResendRequest("Gerekçe"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/mail-center/notifications/{changedRecipient}/resend", new MailCenterResendRequest("Gerekçe"))).StatusCode);

        var first = await admin.PostAsJsonAsync($"/api/mail-center/notifications/{id}/resend", new MailCenterResendRequest("Alıcı doğrulandı"));
        first.EnsureSuccessStatusCode();
        var second = await admin.PostAsJsonAsync($"/api/mail-center/notifications/{id}/resend", new MailCenterResendRequest("Tekrar"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await Db(f, async db =>
        {
            var row = await db.UserNotifications.SingleAsync(x => x.Id == id);
            Assert.Equal(MailDeliveryStatus.Pending, row.EmailStatus);
            Assert.Equal(0, row.ErrorCode.Length);
            Assert.Equal(2, row.Revision);
            var audit = await db.AuditRecords.SingleAsync(x => x.Action == "NotificationMailResent");
            Assert.Equal(id.ToString(), audit.EntityId);
            Assert.Equal("Alıcı doğrulandı", audit.Reason);
        });
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/mail-center/messages?status=Pending")).StatusCode);
    }
}
