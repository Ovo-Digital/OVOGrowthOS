using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class ListPayloadTests
{
    private static async Task SeedAsync(WorkflowApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = WorkflowApiFactory.AccountId("admin@ovo.test");
        var blob = string.Join("", Enumerable.Repeat("a", 4096));
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 100; i++)
            db.UserNotifications.Add(new UserNotification { UserId = admin, Kind = NotificationKind.PortalReply, SourceId = Guid.NewGuid(), CreatedAt = now.AddMinutes(-i) });

        for (var i = 0; i < 100; i++)
        {
            var brand = new Brand { Name = $"Ölçüm Markası {i:D3}", Status = BrandStatus.Lead, ContactName = $"Kişi {i}" };
            db.Brands.Add(brand);
            db.BrandContactNotes.Add(new BrandContactNote { BrandId = brand.Id, ContactOn = DateOnly.FromDateTime(now.DateTime).AddDays(-i), Text = "Görüşme notu" });
            db.BrandStageHistories.Add(new BrandStageHistory { BrandId = brand.Id, Stage = LeadStage.Contacted, EntryKnown = true, EnteredAt = now.AddHours(-i) });
        }

        for (var i = 0; i < 40; i++)
        {
            var brand = new Brand { Name = $"Değerlendirme Markası {i:D2}", Status = BrandStatus.Active };
            db.Evaluations.Add(new BrandEvaluation
            {
                Brand = brand, BrandId = brand.Id, Status = EvaluationStatus.Analyzed, Decision = DecisionStatus.ConditionalAccept,
                CreatedBy = "test", InputSnapshotJson = blob, RuleSnapshotJson = blob, CalculationSnapshotJson = blob, RecommendationSnapshotJson = blob,
            });
        }

        for (var i = 0; i < 40; i++)
        {
            var brand = new Brand { Name = $"Anlaşma Markası {i:D2}", Status = BrandStatus.Active };
            var deal = new Deal
            {
                Brand = brand, BrandId = brand.Id, EvaluationId = Guid.NewGuid(), Name = $"Anlaşma {i:D2}", Status = DealStatus.Accepted,
                EvaluationSnapshotJson = blob, RuleSnapshotJson = blob, FinancialSnapshotJson = blob, CommissionSnapshotJson = blob, ConditionsSnapshotJson = blob,
            };
            db.Deals.Add(deal);
            db.MonthlyPerformances.Add(new MonthlyPerformance
            {
                BrandId = brand.Id, DealId = deal.Id, Year = 2026 + i / 12, Month = i % 12 + 1, NetRevenue = 100_000m, OvoFee = 20_000m, Mer = 1.2m,
            });
        }

        for (var i = 0; i < 100; i++)
            db.AuditRecords.Add(new AuditRecord { UserId = "admin@ovo.test", Action = "MeasuredAction", EntityType = "Brand", EntityId = Guid.NewGuid().ToString(), OldValueJson = blob, NewValueJson = blob, CreatedAt = now.AddMinutes(-i) });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_endpoints_stay_small_and_never_ship_snapshot_or_audit_content()
    {
        await using var factory = new WorkflowApiFactory();
        await factory.SeedAsync();
        await SeedAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));

        var limits = new Dictionary<string, int>
        {
            ["/api/notifications"] = 40_000,
            ["/api/lead-follow-ups?page=1&pageSize=50"] = 60_000,
            ["/api/evaluations?page=1&pageSize=40"] = 60_000,
            ["/api/deals?page=1&pageSize=40"] = 60_000,
            ["/api/performance?page=1&pageSize=40"] = 60_000,
            ["/api/audit?page=1&pageSize=50"] = 60_000,
        };

        foreach (var (url, limit) in limits)
        {
            var warm = await client.GetAsync(url);
            warm.EnsureSuccessStatusCode();

            var times = new List<long>();
            var bytes = 0;
            var body = "";
            for (var i = 0; i < 3; i++)
            {
                var watch = Stopwatch.StartNew();
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsByteArrayAsync();
                watch.Stop();
                response.EnsureSuccessStatusCode();
                times.Add(watch.ElapsedMilliseconds);
                bytes = content.Length;
                body = Encoding.UTF8.GetString(content);
            }
            times.Sort();

            Assert.True(bytes <= limit, $"{url} → {bytes} bayt, üst sınır {limit} bayt");
            Assert.True(times[times.Count / 2] < 1000, $"{url} → {times[times.Count / 2]} ms, üst sınır 1000 ms");
            foreach (var key in new[] { "snapshotjson", "oldvaluejson", "newvaluejson" })
                Assert.DoesNotContain(key, body, StringComparison.OrdinalIgnoreCase);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var checkDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var leadRows = await checkDb.Brands.CountAsync(x => x.Status == BrandStatus.Lead || x.Status == BrandStatus.Evaluation || x.Status == BrandStatus.Negotiation);
        var leads = JsonDocument.Parse(await client.GetStringAsync("/api/lead-follow-ups?page=1&pageSize=50")).RootElement;
        Assert.True(leadRows >= 100, $"beklenen en az 100 aday marka, veritabanında {leadRows}");
        Assert.Equal(leadRows, leads.GetProperty("total").GetInt32());
        var audit = JsonDocument.Parse(await client.GetStringAsync("/api/audit?page=1&pageSize=50")).RootElement;
        Assert.True(audit.GetProperty("total").GetInt32() >= 100);
    }

    [Fact]
    public async Task Dashboard_responses_stay_small_fast_and_snapshot_free()
    {
        await using var factory = new WorkflowApiFactory();
        await factory.SeedAsync();
        await SeedAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));

        foreach (var url in new[] { "/api/dashboard?year=2026&month=8", "/api/dashboard?year=2026&month=8&scope=All&currency=TRY" })
        {
            var warm = await client.GetAsync(url);
            warm.EnsureSuccessStatusCode();

            var watch = Stopwatch.StartNew();
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsByteArrayAsync();
            watch.Stop();
            response.EnsureSuccessStatusCode();

            var body = Encoding.UTF8.GetString(content);
            Assert.True(content.Length <= 150_000, $"{url} → {content.Length} bayt, üst sınır 150000 bayt");
            Assert.True(watch.ElapsedMilliseconds < 2000, $"{url} → {watch.ElapsedMilliseconds} ms, üst sınır 2000 ms");
            foreach (var key in new[] { "snapshotjson", "oldvaluejson", "newvaluejson" })
                Assert.DoesNotContain(key, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
