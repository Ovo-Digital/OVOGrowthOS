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
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class V6Dalga12ApiTests
{
    private static readonly Guid Admin = WorkflowApiFactory.AccountId("admin@ovo.test");

    private sealed class FakeAds : IAdSpendClient
    {
        public AdSpendResult? Result { get; set; } = new(1234.56m, "TRY", "Meta reklam raporu");
        public Task<bool> TestAsync(AdConnection connection, CancellationToken ct) => Task.FromResult(Result is not null);
        public Task<AdSpendResult?> FetchAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct) => Task.FromResult(Result);
    }

    private static WebApplicationFactory<Program> AdsSetup(WorkflowApiFactory parent, FakeAds ads) =>
        parent.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IAdSpendClient>(); s.AddSingleton<IAdSpendClient>(ads); }));

    private static WebApplicationFactory<Program> MailSetup(WorkflowApiFactory parent, AccountMailTests.Sender sender) => parent.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MAIL_ENABLED"] = "true", ["SMTP_USER"] = "sender@example.test", ["SMTP_PASS"] = "test-not-real",
            ["MAIL_FROM"] = "OVO <sender@example.test>", ["WebOrigin"] = "https://panel.example.test" }));
        b.ConfigureServices(s => { s.RemoveAll<IAccountMailSender>(); s.AddSingleton<IAccountMailSender>(sender); });
    });

    private static async Task<HttpClient> Client(WebApplicationFactory<Program> f, string role = "admin")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext, Task> work)
    {
        await using var scope = f.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task Refresh(WebApplicationFactory<Program> f, Guid user, DateTimeOffset? now = null)
    {
        await using var scope = f.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationService>().Refresh(user, now ?? DateTimeOffset.UtcNow);
    }

    private static async Task<bool> Send(WebApplicationFactory<Program> f)
    {
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationMailQueue>().ProcessOne();
    }

    private static async Task<Guid> SeedBrand(WebApplicationFactory<Program> f, string industry = "")
    {
        var id = Guid.NewGuid();
        await Db(f, async db => { db.Add(new Brand { Id = id, Name = "V6 Test Markası", Industry = industry }); await db.SaveChangesAsync(); });
        return id;
    }

    private static async Task<(Guid PerformanceId, Guid NoteId)> SeedPromisePeriod(WebApplicationFactory<Program> f, DateOnly dueOn)
    {
        var brand = Guid.NewGuid();
        Guid performanceId = Guid.Empty; Guid noteId = Guid.NewGuid();
        await Db(f, async db =>
        {
            db.Add(new Brand { Id = brand, Name = "Söz hatırlatma markası" });
            var deal = new Deal { BrandId = brand, Currency = "TRY", Name = "Söz hatırlatma anlaşması" };
            var performance = new MonthlyPerformance
            {
                BrandId = brand, Deal = deal, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Invoiced,
                OvoFee = 100m, NetRevenue = 1000m, OvoGrossProfit = 80m, CommissionBreakdownJson = "{}",
                Collection = new() { ReceivableAmount = 100m, Currency = "TRY", Revision = 1, DueOn = dueOn.AddDays(-10) }
            };
            db.AddRange(performance, new BrandContactNote { Id = noteId, BrandId = brand, ContactOn = dueOn.AddDays(-1), Text = "Ödeme sözü görüşmesi." });
            await db.SaveChangesAsync();
            performanceId = performance.Id;
        });
        return (performanceId, noteId);
    }

    [Fact]
    public async Task Ad_settings_save_test_and_spend_flow_never_leak_credentials()
    {
        await using var p = new WorkflowApiFactory(); var ads = new FakeAds();
        await using var f = AdsSetup(p, ads); var brand = await SeedBrand(f); using var c = await Client(f);
        var empty = await c.GetStringAsync($"/api/brands/{brand}/ad-settings?platform=Meta");
        Assert.Contains("\"revision\":0", empty);
        Assert.Contains("\"configured\":false", empty);

        var save = await c.PutAsJsonAsync($"/api/brands/{brand}/ad-settings",
            new AdSettingsRequest(0, "Meta", "123456789", "", "meta-secret-value", "", ""));
        save.EnsureSuccessStatusCode();
        var saved = await save.Content.ReadAsStringAsync();
        Assert.Contains("\"revision\":1", saved);
        Assert.DoesNotContain("meta-secret-value", saved);

        var read = await c.GetStringAsync($"/api/brands/{brand}/ad-settings?platform=Meta");
        Assert.Contains("\"configured\":true", read);
        Assert.Contains("\"secretStored\":true", read);
        Assert.DoesNotContain("meta-secret-value", read);

        var test = await c.PostAsJsonAsync($"/api/brands/{brand}/ad-settings/test", new AdTestRequest(1, "Meta"));
        test.EnsureSuccessStatusCode();
        var testBody = await test.Content.ReadAsStringAsync();
        Assert.Contains("\"accepted\":true", testBody);
        Assert.Contains("doğrulandı", testBody);

        var spend = await c.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/ad-spend?platform=Meta&period=2026-08");
        Assert.Equal(1234.56m, spend.GetProperty("amount").GetDecimal());
        Assert.Equal("TRY", spend.GetProperty("currency").GetString());

        var google = await c.PutAsJsonAsync($"/api/brands/{brand}/ad-settings",
            new AdSettingsRequest(0, "Google", "123456789", "", "google-refresh-token-1", "client-secret", "developer-token"));
        Assert.Equal(HttpStatusCode.BadRequest, google.StatusCode);
        Assert.Contains("client id", (await google.Content.ReadAsStringAsync()).ToLowerInvariant());

        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/brands/{brand}/ad-settings",
            new AdSettingsRequest(0, "Meta", "123456789", "", "another-secret-value", "", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/brands/{brand}/ad-settings?platform=Bing")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/brands/{brand}/ad-spend?platform=Google&period=2026-08")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/brands/{brand}/ad-spend?platform=Meta&period=2026-13")).StatusCode);
    }

    [Fact]
    public async Task Ad_spend_without_saved_connection_is_rejected_with_clear_message()
    {
        await using var p = new WorkflowApiFactory(); await using var f = AdsSetup(p, new FakeAds());
        var brand = await SeedBrand(f); using var c = await Client(f);
        var response = await c.GetAsync($"/api/brands/{brand}/ad-spend?platform=Meta&period=2026-08");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("bağlantı ayarları kayıtlı değil", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Shopify_orders_parse_into_staging_drafts_and_bad_rows_are_rejected()
    {
        using var paid = JsonDocument.Parse("""
            {"id":987654321,"order_number":1001,"created_at":"2026-08-05T10:15:00Z","currency":"try",
             "total_price":"250.50","total_refunded":"10.00","financial_status":"paid","cancelled":false}
            """);
        Assert.True(StoreOrderClient.TryParseShopifyOrder(paid.RootElement, "demo.myshopify.com", out var draft));
        Assert.Equal("987654321", draft.SourceOrderId);
        Assert.Equal("demo.myshopify.com", draft.SourceStoreId);
        Assert.Equal(1001, draft.OrderNumber);
        Assert.Equal(new DateTimeOffset(2026, 8, 5, 10, 15, 0, TimeSpan.Zero), draft.PlacedOnUtc);
        Assert.Equal("TRY", draft.Currency);
        Assert.Equal(250.50m, draft.OrderTotal);
        Assert.Equal(250.50m, draft.PaidAmount);
        Assert.Equal(10.00m, draft.RefundedAmount);
        Assert.Equal(StoreOrderStatus.Complete, draft.OrderStatus);

        using var cancelled = JsonDocument.Parse("""
            {"id":111,"name":"#1007","created_at":"2026-08-06T00:00:00Z","currency":"TRY",
             "total_price":"40.00","financial_status":"pending","cancelled":true}
            """);
        Assert.True(StoreOrderClient.TryParseShopifyOrder(cancelled.RootElement, "demo.myshopify.com", out var cancelledDraft));
        Assert.Equal(1007, cancelledDraft.OrderNumber);
        Assert.Equal(StoreOrderStatus.Cancelled, cancelledDraft.OrderStatus);
        Assert.Equal(0m, cancelledDraft.PaidAmount);

        using var badPayment = JsonDocument.Parse("""
            {"id":222,"order_number":3,"created_at":"2026-08-06T00:00:00Z","currency":"TRY",
             "total_price":"10.00","financial_status":"bogus"}
            """);
        Assert.False(StoreOrderClient.TryParseShopifyOrder(badPayment.RootElement, "demo.myshopify.com", out _));

        using var missingCurrency = JsonDocument.Parse("""
            {"id":333,"order_number":4,"created_at":"2026-08-06T00:00:00Z","total_price":"10.00","financial_status":"paid"}
            """);
        Assert.False(StoreOrderClient.TryParseShopifyOrder(missingCurrency.RootElement, "demo.myshopify.com", out _));
    }

    [Fact]
    public async Task Promise_reminder_is_created_once_and_mail_explains_remaining_amount()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender();
        await using var f = MailSetup(p, sender);
        var (performanceId, noteId) = await SeedPromisePeriod(f, TeamWork.Today(DateTimeOffset.UtcNow));
        using var c = await Client(f);
        var tomorrow = TeamWork.Today(DateTimeOffset.UtcNow).AddDays(1);
        var saved = await c.PutAsJsonAsync($"/api/performance/{performanceId}/collection/promise",
            new CollectionPromiseRequest(60.05m, tomorrow, noteId, Admin, "Ödeme sözü alındı.", 0, 1));
        saved.EnsureSuccessStatusCode();

        await Db(f, async db =>
        {
            db.NotificationPreferences.Add(new NotificationPreference { UserId = Admin, StartedAt = DateTimeOffset.UtcNow, PromiseRemindersEmail = true });
            await db.SaveChangesAsync();
        });
        await Refresh(f, Admin);
        await Refresh(f, Admin);
        await Db(f, async db =>
        {
            var row = await db.UserNotifications.SingleAsync(x => x.Kind == NotificationKind.PromiseReminder);
            Assert.StartsWith("promise-due:", row.EventKey);
            Assert.NotNull(row.EmailStatus);
        });

        Assert.True(await Send(f));
        Assert.Contains("Yarın ödeme sözü var", sender.Messages[0].Body);
        Assert.Contains("60,05", sender.Messages[0].Body);
        Assert.Contains("/commissions/planning", sender.Messages[0].Body);
        Assert.False(await Send(f));
    }

    [Fact]
    public async Task Weekly_digest_opens_only_after_monday_morning_and_is_idempotent()
    {
        await using var p = new WorkflowApiFactory(); var sender = new AccountMailTests.Sender();
        await using var f = MailSetup(p, sender);
        var before = new DateTimeOffset(2026, 9, 28, 5, 0, 0, TimeSpan.Zero);
        var after = new DateTimeOffset(2026, 9, 28, 6, 30, 0, TimeSpan.Zero);
        await Db(f, async db =>
        {
            db.NotificationPreferences.Add(new NotificationPreference { UserId = Admin, StartedAt = DateTimeOffset.UtcNow, WeeklyDigestEmail = true });
            await db.SaveChangesAsync();
        });

        await Refresh(f, Admin, before);
        await Db(f, async db => Assert.Empty(await db.UserNotifications.Where(x => x.Kind == NotificationKind.WeeklyDigest).ToListAsync()));

        await Refresh(f, Admin, after);
        await Refresh(f, Admin, after);
        await Db(f, async db =>
        {
            var digest = await db.UserNotifications.SingleAsync(x => x.Kind == NotificationKind.WeeklyDigest);
            Assert.StartsWith("digest:2026-W", digest.EventKey);
            Assert.NotNull(digest.EmailStatus);
            digest.CreatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        Assert.True(await Send(f));
        Assert.Contains("Haftalık yönetim özeti", sender.Messages[0].Body);
        Assert.Contains("/reports", sender.Messages[0].Body);
        Assert.False(await Send(f));
    }

    [Fact]
    public async Task Lead_timeout_queue_creates_one_task_and_stays_idempotent()
    {
        await using var f = new WorkflowApiFactory();
        var now = DateTimeOffset.UtcNow;
        Guid brandId = Guid.Empty;
        await Db(f, async db =>
        {
            var brand = new Brand { Name = "Aşamada bekleyen marka", Status = BrandStatus.Lead };
            db.Add(brand);
            db.Add(new BrandStageHistory { BrandId = brand.Id, Stage = LeadStage.New, EntryKnown = true, EnteredAt = now.AddDays(-20), EnteredBy = "admin@ovo.test" });
            db.Add(new BrandFollowUp { BrandId = brand.Id, OwnerId = Admin, Stage = LeadStage.New });
            await db.SaveChangesAsync();
            brandId = brand.Id;
        });

        await using var scope = f.Services.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<LeadTimeoutQueue>();
        Assert.Equal(1, await queue.RunDue(now));
        var task = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkTasks.SingleAsync(x => x.Kind == WorkKind.LeadTimeout);
        Assert.Equal(Admin, task.AssigneeId);
        Assert.Equal(brandId, task.BrandId);
        Assert.Equal("Sistem", task.CreatedBy);
        Assert.Null(task.CompletedAt);
        Assert.Contains("Aşama zaman aşımı", task.Title);
        await Db(f, async db =>
        {
            var history = await db.BrandStageHistories.SingleAsync();
            Assert.Equal(task.Id, history.TimeoutTaskId);
        });

        Assert.Equal(0, await queue.RunDue(now));
        await Db(f, async db => Assert.Equal(1, await db.WorkTasks.CountAsync(x => x.Kind == WorkKind.LeadTimeout)));
    }

    [Fact]
    public async Task Brand_health_endpoint_returns_score_bands_and_four_factors()
    {
        await using var f = new WorkflowApiFactory(); var brand = await SeedBrand(f); using var c = await Client(f);
        var health = await c.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/health");
        var score = health.GetProperty("score").GetInt32();
        Assert.InRange(score, 0, 100);
        Assert.Equal(85, score);
        Assert.Equal("Güçlü", health.GetProperty("band").GetString());
        Assert.Equal(4, health.GetProperty("factors").GetArrayLength());
        Assert.Equal("V6 Test Markası", health.GetProperty("brandName").GetString());
        var factors = health.GetProperty("factors").EnumerateArray().ToList();
        Assert.Contains(factors, x => x.GetProperty("code").GetString() == "deal" && x.GetProperty("effect").GetInt32() == -15);
        Assert.Contains(factors, x => x.GetProperty("code").GetString() == "target" && x.GetProperty("detail").GetString()!.Contains("hedef kaydı yok"));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/brands/{Guid.NewGuid()}/health")).StatusCode);
    }

    [Fact]
    public async Task Performance_suggestions_expose_gross_ciro_and_ad_platform_flags()
    {
        await using var p = new WorkflowApiFactory(); await using var f = AdsSetup(p, new FakeAds());
        var brand = await SeedBrand(f); using var c = await Client(f);
        await Db(f, async db =>
        {
            db.Add(new MonthlyTarget { BrandId = brand, Year = 2026, Month = 8, Currency = "TRY", NetRevenueGoal = 10000m, AdBudget = 500m, ContributionMarginGoal = 0.4m, OwnerId = Admin });
            db.Add(new BrandAdSettings { BrandId = brand, Platform = AdPlatform.Meta, AccountId = "123456789", ClientId = "", ProtectedSecret = "stored", Revision = 1 });
            db.AddRange(
                new StoreOrderStaging { BrandId = brand, SourceOrderId = "1", SourceStoreId = "", OrderNumber = 1,
                    PlacedOnUtc = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero), Currency = "TRY",
                    OrderTotal = 300m, PaidAmount = 300m, RefundedAmount = 0m, OrderStatus = StoreOrderStatus.Complete,
                    PaymentStatus = 30, ImportedAt = DateTimeOffset.UtcNow },
                new StoreOrderStaging { BrandId = brand, SourceOrderId = "2", SourceStoreId = "", OrderNumber = 2,
                    PlacedOnUtc = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero), Currency = "TRY",
                    OrderTotal = 100m, PaidAmount = 0m, RefundedAmount = 0m, OrderStatus = StoreOrderStatus.Cancelled,
                    PaymentStatus = 10, ImportedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        var body = await c.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/performance-suggestions?period=2026-08");
        var gross = body.GetProperty("gross");
        Assert.True(gross.GetProperty("available").GetBoolean());
        Assert.Equal(300m, gross.GetProperty("amount").GetDecimal());
        Assert.Equal("TRY", gross.GetProperty("currency").GetString());
        Assert.Equal(1, gross.GetProperty("orderCount").GetInt32());
        Assert.True(body.GetProperty("adSpend").GetProperty("metaConfigured").GetBoolean());
        Assert.False(body.GetProperty("adSpend").GetProperty("googleConfigured").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/brands/{brand}/performance-suggestions?period=2026-13")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/brands/{Guid.NewGuid()}/performance-suggestions?period=2026-08")).StatusCode);
    }

    [Fact]
    public async Task Sector_comparison_aggregates_only_final_periods_per_industry()
    {
        await using var f = new WorkflowApiFactory(); using var c = await Client(f);
        await Db(f, async db =>
        {
            foreach (var name in new[] { "Kozmetik Markası A", "Kozmetik Markası B" })
            {
                var brand = new Brand { Name = name, Industry = "Kozmetik" };
                var deal = new Deal { BrandId = brand.Id, Brand = brand, Currency = "TRY", Name = name + " anlaşması" };
                var performance = new MonthlyPerformance
                {
                    BrandId = brand.Id, Deal = deal, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Approved,
                    GrossSales = 1000m, GrossProfit = 400m, Refunds = 50m, NetRevenue = 900m,
                    OvoFee = 100m, OvoGrossProfit = 80m, CommissionBreakdownJson = "{}"
                };
                db.Add(brand);
                db.Add(deal);
                db.Add(performance);
                db.Add(new MonthlyTarget { BrandId = brand.Id, Year = 2026, Month = 8, Currency = "TRY", NetRevenueGoal = 900m, OwnerId = Admin });
            }
            var draftBrand = new Brand { Name = "Taslak Marka", Industry = "Kozmetik" };
            var draftDeal = new Deal { BrandId = draftBrand.Id, Brand = draftBrand, Currency = "TRY", Name = "Taslak anlaşma" };
            db.Add(draftBrand);
            db.Add(draftDeal);
            db.Add(new MonthlyPerformance { BrandId = draftBrand.Id, Deal = draftDeal, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Draft, NetRevenue = 1m });
            await db.SaveChangesAsync();
        });

        var body = await c.GetFromJsonAsync<JsonElement>("/api/reports/sector-comparison?year=2026&month=8");
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2026, body.GetProperty("year").GetInt32());
        var kosmetik = items.Single(x => x.GetProperty("industry").GetString() == "Kozmetik");
        Assert.Equal(2, kosmetik.GetProperty("brandCount").GetInt32());
        Assert.Equal(0.4m, kosmetik.GetProperty("grossMargin").GetDecimal());
        Assert.Equal(0.05m, kosmetik.GetProperty("returnShare").GetDecimal());
        Assert.Equal(1m, kosmetik.GetProperty("targetAchievement").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/reports/sector-comparison?year=2026&month=13")).StatusCode);
    }

    [Fact]
    public async Task Portal_period_approval_records_decisions_and_reports_them_to_staff()
    {
        await using var f = new WorkflowApiFactory();
        Guid brand = Guid.Empty;
        await Db(f, async db =>
        {
            brand = Guid.NewGuid();
            var finalBrand = new Brand { Id = brand, Name = "Onaylı Marka" };
            var deal = new Deal { BrandId = brand, Currency = "TRY", Name = "Onay anlaşması" };
            db.AddRange(finalBrand, deal,
                new MonthlyPerformance { BrandId = brand, Deal = deal, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Approved, NetRevenue = 500m },
                new MonthlyPerformance { BrandId = brand, Deal = deal, Year = 2026, Month = 7, Status = MonthlyPerformanceStatus.Draft, NetRevenue = 100m });
            await db.SaveChangesAsync();
        });

        using var admin = CustomerPortalTests.Staff(f);
        var (client, _) = await CustomerPortalTests.Customer(f, admin, brand, "approval-client@ovo.test");
        using var portal = client;

        var list = await portal.GetFromJsonAsync<JsonElement>("/api/portal/periods");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        var august = items.Single(x => x.GetProperty("month").GetInt32() == 8);
        Assert.Equal(2026, august.GetProperty("year").GetInt32());
        Assert.Equal("Ağustos 2026", august.GetProperty("label").GetString());
        Assert.Equal(JsonValueKind.Null, august.GetProperty("approval").ValueKind);

        var draftDecision = await portal.PostAsJsonAsync("/api/portal/periods/2026/7/decision", new { approved = true });
        Assert.Equal(HttpStatusCode.Conflict, draftDecision.StatusCode);
        Assert.Contains("kesinleşmedi", await draftDecision.Content.ReadAsStringAsync());

        var approve = await portal.PostAsJsonAsync("/api/portal/periods/2026/8/decision", new { approved = true, reason = "" });
        approve.EnsureSuccessStatusCode();
        var approved = await approve.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(approved.GetProperty("approved").GetBoolean());
        Assert.Contains("onaylandı", approved.GetProperty("message").GetString());

        var missingReason = await portal.PostAsJsonAsync("/api/portal/periods/2026/8/decision", new { approved = false, reason = "" });
        Assert.Equal(HttpStatusCode.Conflict, missingReason.StatusCode);
        Assert.Contains("gerekçe", await missingReason.Content.ReadAsStringAsync());

        var reject = await portal.PostAsJsonAsync("/api/portal/periods/2026/8/decision", new { approved = false, reason = "Hakediş tutarıyla ilgili itirazımız var." });
        reject.EnsureSuccessStatusCode();

        var after = await portal.GetFromJsonAsync<JsonElement>("/api/portal/periods");
        var augustAfter = after.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("month").GetInt32() == 8);
        Assert.False(augustAfter.GetProperty("approval").GetProperty("approved").GetBoolean());
        Assert.Contains("itirazımız", augustAfter.GetProperty("approval").GetProperty("reason").GetString());
        Assert.Equal(1, after.GetProperty("items").GetArrayLength());

        var staffList = await admin.GetFromJsonAsync<JsonElement>("/api/performance?page=1&year=2026&month=8");
        var staffItem = staffList.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("brand").GetProperty("id").GetGuid() == brand);
        Assert.False(staffItem.GetProperty("approval").GetProperty("approved").GetBoolean());

        await Db(f, async db => Assert.Equal(1, await db.PeriodApprovals.CountAsync(x => x.BrandId == brand)));
        Assert.Equal(HttpStatusCode.NotFound, (await portal.PostAsJsonAsync($"/api/portal/periods/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month}/decision", new { approved = true })).StatusCode);
    }
}
