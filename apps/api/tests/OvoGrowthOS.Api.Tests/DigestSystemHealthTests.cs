using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class DigestSystemHealthTests
{
    private static async Task Db(WorkflowApiFactory f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    private static async Task<string> Build(WorkflowApiFactory f)
    {
        string body = "";
        await Db(f, async db => body = await WeeklyDigestReport.BuildAsync(db, DateTimeOffset.UtcNow, "https://x.test"));
        return body;
    }

    [Fact]
    public async Task Clean_state_reports_no_failures_and_failures_are_listed()
    {
        await using var f = new WorkflowApiFactory();

        var clean = await Build(f);
        Assert.Contains("5) Sistem sağlığı", clean);
        Assert.Contains("Son 7 günde gönderilemeyen e-posta veya başarısız otomatik senkron yok.", clean);

        await Db(f, async db =>
        {
            db.UserNotifications.Add(new UserNotification
            {
                UserId = WorkflowApiFactory.AccountId("admin@ovo.test"),
                Kind = NotificationKind.WeeklyDigest,
                EventKey = "digest:2026-W39",
                EmailStatus = MailDeliveryStatus.Uncertain,
                Email = "admin@ovo.test"
            });
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = "sistem",
                Action = "StoreOrdersAutoSync",
                EntityType = "StoreOrder",
                EntityId = "2026-08",
                NewValueJson = """{"synced":1,"failed":2,"truncated":0,"errors":["Shopify: 500"]}""",
                Reason = "1 marka güncellendi, 2 marka başarısız"
            });
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = "sistem",
                Action = "StoreOrdersAutoSync",
                EntityType = "StoreOrder",
                EntityId = "2026-07",
                NewValueJson = """{"synced":3,"failed":0,"truncated":0,"errors":[]}""",
                Reason = "3 marka güncellendi"
            });
            await db.SaveChangesAsync();
        });

        var body = await Build(f);
        Assert.Contains("Gönderilemeyen veya doğrulanamayan e-posta: 1 adet", body);
        Assert.Contains("Başarısız otomatik sipariş senkronu: 2026-08 (2 marka)", body);
        Assert.DoesNotContain("2026-07", body);
        Assert.DoesNotContain("Son 7 günde gönderilemeyen", body);
    }
}
