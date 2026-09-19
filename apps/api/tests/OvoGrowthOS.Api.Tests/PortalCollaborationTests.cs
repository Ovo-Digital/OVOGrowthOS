using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortalCollaborationTests
{
    private static string Root(Guid brand) => $"/api/portal-management/brands/{brand}";
    private static async Task<Guid> Id(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> First(HttpClient c, string path) => (await c.GetFromJsonAsync<JsonElement>(path))[0];

    [Fact]
    public async Task Legacy_answer_and_new_messages_survive_tracking_reopening_and_stale_writes()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (c, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var client = c;
        var report = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        var question = await Id(await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(report, "İade neden arttı?")));
        var path = Root(s.BrandId) + $"/questions/{question}";
        (await admin.PostAsJsonAsync(path + "/answer", new PortalAnswerRequest("İlk açıklama korunur."))).EnsureSuccessStatusCode();
        var q = await First(c, "/api/portal/conversations"); Assert.Equal("AwaitingCustomer", q.GetProperty("status").GetString()); Assert.Equal(2, q.GetProperty("revision").GetInt32());
        (await c.PostAsJsonAsync($"/api/portal/questions/{question}/messages", new PortalMessageRequest("Hangi ürünler?", 2))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/messages", new PortalMessageRequest("Eski ekran", 2))).StatusCode);
        (await admin.PostAsJsonAsync(path + "/messages", new PortalMessageRequest("A ve B ürünleri.", 3))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync(path + "/tracking", new PortalTrackingRequest(PortalConversationStatus.Resolved, WorkflowApiFactory.AccountId("partner@ovo.test"), 4))).EnsureSuccessStatusCode();
        q = await First(c, "/api/portal/conversations"); Assert.Equal("Resolved", q.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, q.GetProperty("ownerId").ValueKind);
        Assert.Equal("İlk açıklama korunur.", q.GetProperty("answer").GetString()); Assert.Equal(2, q.GetProperty("messages").GetArrayLength());
        (await c.PostAsJsonAsync($"/api/portal/questions/{question}/messages", new PortalMessageRequest("Yeni bir ayrıntı var.", 5))).EnsureSuccessStatusCode();
        q = await First(admin, Root(s.BrandId) + "/conversations"); Assert.Equal("Open", q.GetProperty("status").GetString()); Assert.Equal("Partner", q.GetProperty("ownerName").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/answer", new PortalAnswerRequest("İlk yanıtı değiştir"))).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("İlk açıklama korunur.", (await db.PortalQuestions.FindAsync(question))!.Answer); Assert.Equal(3, await db.PortalMessages.CountAsync());
        Assert.Equal(MonthlyPerformanceStatus.Locked, (await db.MonthlyPerformances.FindAsync(s.PeriodId))!.Status);
    }

    [Fact]
    public async Task Conversations_are_private_to_account_and_brand_and_revoked_reports_accept_no_messages()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (c, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var client = c;
        var (colleague, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId, "colleague@ovo.test"); using var colleagueClient = colleague;
        var (other, _) = await CustomerPortalTests.Customer(f, admin, s.OtherBrandId, "other@ovo.test"); using var otherClient = other;
        var report = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        var question = await Id(await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(report, "Yalnız bana ait konu")));
        Assert.Empty((await colleague.GetFromJsonAsync<JsonElement>("/api/portal/conversations")).EnumerateArray());
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/portal/conversations?brandId=" + s.BrandId)).EnumerateArray());
        foreach (var denied in new[] { colleague, other }) Assert.Equal(HttpStatusCode.NotFound, (await denied.PostAsJsonAsync($"/api/portal/questions/{question}/messages", new PortalMessageRequest("Giremem", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(Root(s.OtherBrandId) + $"/questions/{question}/messages", new PortalMessageRequest("Yanlış marka", 1))).StatusCode);
        (await admin.PostAsync(Root(s.BrandId) + $"/reports/{report}/revoke", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/portal/questions/{question}/messages", new PortalMessageRequest("Paylaşım kapalı", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(s.BrandId) + $"/questions/{question}/answer", new PortalAnswerRequest("Paylaşım kapalı"))).StatusCode);
        var response = await c.GetAsync("/api/portal/conversations"); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("Yalnız bana ait konu", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_message_tracking_and_closed_or_ineligible_owner_are_rejected()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (c, userId) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var client = c;
        var report = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        var question = await Id(await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(report, "Soru")));
        var path = Root(s.BrandId) + $"/questions/{question}";
        foreach (var text in new[] { " ", new string('x', 4001) }) Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path + "/messages", new PortalMessageRequest(text, 1))).StatusCode);
        foreach (var owner in new[] { userId, Guid.NewGuid(), WorkflowApiFactory.AccountId("analyst@ovo.test") })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path + "/tracking", new PortalTrackingRequest(PortalConversationStatus.Open, owner, 1))).StatusCode);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.UserAccounts.FindAsync(WorkflowApiFactory.AccountId("partner@ovo.test")))!.IsActive = false; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path + "/tracking", new PortalTrackingRequest(PortalConversationStatus.Open, WorkflowApiFactory.AccountId("partner@ovo.test"), 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path + "/tracking", new PortalTrackingRequest((PortalConversationStatus)9, null, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path + "/tracking", new PortalTrackingRequest(PortalConversationStatus.Open, null, 2))).StatusCode);
    }

    [Fact]
    public async Task Readings_are_version_and_user_scoped_idempotent_and_not_financial_approval()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (c, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var client = c;
        var (colleague, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId, "colleague@ovo.test"); using var colleagueClient = colleague;
        var report = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        var path = $"/api/portal/reports/{report}";
        (await c.GetAsync(path)).EnsureSuccessStatusCode(); // Read-only API requests are not evidence of human review.
        Assert.Equal(JsonValueKind.Null, (await c.GetFromJsonAsync<JsonElement>(path + "/reading")).GetProperty("reviewedAt").ValueKind);
        (await c.PostAsync(path + "/viewed", null)).EnsureSuccessStatusCode();
        (await c.PostAsync(path + "/reviewed", null)).EnsureSuccessStatusCode();
        var first = await c.GetFromJsonAsync<JsonElement>(path + "/reading");
        (await c.PostAsync(path + "/reviewed", null)).EnsureSuccessStatusCode();
        Assert.Equal(first.GetProperty("reviewedAt").GetString(), (await c.GetFromJsonAsync<JsonElement>(path + "/reading")).GetProperty("reviewedAt").GetString());
        Assert.Equal(JsonValueKind.Null, (await colleague.GetFromJsonAsync<JsonElement>(path + "/reading")).GetProperty("reviewedAt").ValueKind);
        var second = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        Assert.Equal(JsonValueKind.Null, (await c.GetFromJsonAsync<JsonElement>($"/api/portal/reports/{second}/reading")).GetProperty("reviewedAt").ValueKind);
        Assert.Single((await admin.GetFromJsonAsync<JsonElement>(Root(s.BrandId) + "/readings")).EnumerateArray());
        (await admin.PostAsync(Root(s.BrandId) + $"/reports/{report}/revoke", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync(path + "/reviewed", null)).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.PortalReportReadings.ToListAsync()); Assert.Single(await db.AuditRecords.Where(x => x.Action == "PortalReportReviewed").ToListAsync());
        var p = (await db.MonthlyPerformances.FindAsync(s.PeriodId))!; Assert.Equal(MonthlyPerformanceStatus.Locked, p.Status); Assert.Equal(100000, p.OvoFee); Assert.Equal(1000000.1256m, p.NetRevenue);
    }

    [Fact]
    public async Task Requests_preserve_wording_have_audited_status_and_reject_stale_or_cross_brand_writes()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (c, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var client = c;
        var root = Root(s.BrandId); var id = await Id(await admin.PostAsJsonAsync(root + "/requests", new PortalDataRequestCreate("İade dökümü", "Ağustos listesini mevcut güvenli kanaldan iletin.", new(2026, 9, 30))));
        Assert.Equal("İade dökümü", (await First(c, "/api/portal/requests")).GetProperty("title").GetString());
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>(Root(s.OtherBrandId) + "/requests")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync(Root(s.OtherBrandId) + $"/requests/{id}", new PortalDataRequestUpdate(PortalRequestStatus.Received, "Geldi", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(root + $"/requests/{id}", new PortalDataRequestUpdate(PortalRequestStatus.Received, " ", 1))).StatusCode);
        (await admin.PutAsJsonAsync(root + $"/requests/{id}", new PortalDataRequestUpdate(PortalRequestStatus.Received, "Ekip e-postasından teslim alındı.", 1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(root + $"/requests/{id}", new PortalDataRequestUpdate(PortalRequestStatus.Cancelled, "Eski ekran", 1))).StatusCode);
        var item = await First(c, "/api/portal/requests"); Assert.Equal("Received", item.GetProperty("status").GetString()); Assert.Equal(2, item.GetProperty("revision").GetInt32());
        Assert.DoesNotContain("Ekip e-postasından", item.ToString());
        (await admin.PutAsJsonAsync(root + $"/requests/{id}", new PortalDataRequestUpdate(PortalRequestStatus.Requested, "Eksik dosya için yeniden istendi.", 2))).EnsureSuccessStatusCode();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("İade dökümü", (await db.PortalDataRequests.FindAsync(id))!.Title);
        Assert.Equal(2, await db.AuditRecords.CountAsync(x => x.Action == "PortalDataRequestChanged"));
    }

    [Fact]
    public async Task Partial_payment_and_void_change_the_warning_but_not_the_published_report()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = (await db.MonthlyPerformances.FindAsync(s.PeriodId))!;
            p.Status = MonthlyPerformanceStatus.Invoiced; p.Collection = new CollectionAccount { Currency = "TRY", ReceivableAmount = p.OvoFee }; await db.SaveChangesAsync();
        }
        var report = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        var path = Root(s.BrandId) + "/report-updates"; Assert.False((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
        var payment = new CollectionPayment { Id = Guid.NewGuid(), MonthlyPerformanceId = s.PeriodId, Amount = 1000.1234m, PaidOn = new(2026, 9, 19), Reference = "Test payment" };
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Add(payment); await db.SaveChangesAsync(); }
        Assert.True((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = (await db.CollectionPayments.FindAsync(payment.Id))!; p.VoidedAt = DateTimeOffset.UtcNow; p.VoidReason = "Yanlış kayıt"; p.VoidedBy = "admin@ovo.test"; await db.SaveChangesAsync();
            Assert.Single(await db.PortalReports.ToListAsync()); Assert.DoesNotContain("1000.1234", (await db.PortalReports.FindAsync(report))!.SnapshotJson);
        }
        Assert.False((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
    }

    [Fact]
    public async Task New_portal_actions_reject_internal_roles_anonymous_and_other_brand_readings()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f); using var analyst = CustomerPortalTests.Staff(f, "Analyst");
        var (c, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var client = c;
        var otherReport = await Id(await admin.PostAsJsonAsync(Root(s.OtherBrandId) + "/reports", new PortalPublishRequest(s.OtherPeriodId)));
        foreach (var suffix in new[] { "viewed", "reviewed" }) Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/portal/reports/{otherReport}/{suffix}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/portal/reports/{otherReport}/reading")).StatusCode);
        using var anonymous = f.CreateClient();
        foreach (var path in new[] { "/api/portal/conversations", "/api/portal/requests" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(path)).StatusCode);
        }
        foreach (var suffix in new[] { "conversations", "conversation-owners", "readings", "requests", "report-updates" })
            Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync(Root(s.BrandId) + "/" + suffix)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/requests", new PortalDataRequestCreate(" ", "Yönerge", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/requests", new PortalDataRequestCreate("Başlık", " ", null))).StatusCode);
    }

    [Fact]
    public async Task Stale_report_warning_compares_public_values_without_republishing_or_changing_snapshots()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var report = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        var path = Root(s.BrandId) + "/report-updates";
        Assert.False((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = (await db.MonthlyPerformances.FindAsync(s.PeriodId))!; p.OvoGrossProfit = 4321; await db.SaveChangesAsync(); }
        Assert.False((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = (await db.MonthlyPerformances.FindAsync(s.PeriodId))!; p.Status = MonthlyPerformanceStatus.Paid; await db.SaveChangesAsync(); }
        Assert.True((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Single(await db.PortalReports.ToListAsync()); Assert.DoesNotContain("4321", (await db.PortalReports.FindAsync(report))!.SnapshotJson); }
        var latest = await Id(await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId)));
        Assert.Equal(latest, (await First(admin, path)).GetProperty("id").GetGuid()); Assert.False((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
        (await admin.PostAsync(Root(s.BrandId) + $"/reports/{latest}/revoke", null)).EnsureSuccessStatusCode();
        Assert.True((await First(admin, path)).GetProperty("needsUpdate").GetBoolean());
    }
}
