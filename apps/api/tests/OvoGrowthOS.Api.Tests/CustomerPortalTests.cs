using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class CustomerPortalTests
{
    internal sealed record Fixture(Guid BrandId, Guid OtherBrandId, Guid PeriodId, Guid OtherPeriodId, Guid DocumentId, Guid OtherDocumentId);
    internal static async Task<Fixture> Seed(WorkflowApiFactory factory)
    {
        var brandId = await factory.SeedAsync(); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var other = new Brand { Name = "OTHER_BRAND_PRIVATE" }; db.Add(other);
        var deal = new Deal { BrandId = brandId, Name = "PRIVATE_DEAL", Status = DealStatus.Active, EstimatedMonthlyInternalCost = 98765, Currency = "TRY" };
        var otherDeal = new Deal { BrandId = other.Id, Name = "Other", Status = DealStatus.Active };
        db.AddRange(deal, otherDeal);
        var p = new MonthlyPerformance { BrandId = brandId, DealId = deal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Locked,
            NetRevenue = 1000000.1256m, GrossSales = 1200000, OvoFee = 100000, TotalAdSpend = 150000, BrandContributionProfit = 300000, OvoGrossProfit = 98765 };
        var otherP = new MonthlyPerformance { BrandId = other.Id, DealId = otherDeal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Locked, NetRevenue = 777 };
        var doc = new DocumentAttachment { EntityType = "Brand", EntityId = brandId.ToString(), FileName = "marka.pdf", ContentType = "application/pdf", Content = Encoding.UTF8.GetBytes("PUBLIC_FILE"), Note = "PRIVATE_NOTE" };
        var otherDoc = new DocumentAttachment { EntityType = "Brand", EntityId = other.Id.ToString(), FileName = "private.pdf", ContentType = "application/pdf", Content = Encoding.UTF8.GetBytes("OTHER_PRIVATE_FILE") };
        db.AddRange(p, otherP, doc, otherDoc); await db.SaveChangesAsync(); return new(brandId, other.Id, p.Id, otherP.Id, doc.Id, otherDoc.Id);
    }
    private static string Root(Guid brand) => $"/api/portal-management/brands/{brand}";
    internal static HttpClient Staff(WorkflowApiFactory f, string role = "Admin")
    {
        var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", WorkflowApiFactory.Token(role.ToLowerInvariant() + "@ovo.test", role)); return c;
    }
    internal static async Task<(HttpClient Client, Guid Id)> Customer(WorkflowApiFactory f, HttpClient admin, Guid brand, string email = "client@ovo.test")
    {
        var created = await admin.PostAsJsonAsync(Root(brand) + "/accounts", new PortalAccountRequest(email, "Marka Yetkilisi", WorkflowApiFactory.TestPassword)); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var client = f.CreateClient(); var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword }); login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("BrandClient", json.GetProperty("user").GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new("Bearer", json.GetProperty("token").GetString()); return (client, id);
    }
    private static async Task<Guid> Publish(HttpClient admin, Guid brand, Guid period)
    {
        var response = await admin.PostAsJsonAsync(Root(brand) + "/reports", new PortalPublishRequest(period)); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<Guid> Share(HttpClient admin, Guid brand, Guid doc)
    {
        var response = await admin.PostAsJsonAsync(Root(brand) + "/documents", new PortalShareRequest(doc)); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task Customer_cannot_access_any_internal_api_route_or_management_action()
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var admin = Staff(f); var (client, _) = await Customer(f, admin, s.BrandId); using var c = client;
        var endpoints = f.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Where(e =>
            e.RoutePattern.RawText!.StartsWith("/api/") && !e.RoutePattern.RawText.StartsWith("/api/auth/") && !e.RoutePattern.RawText.StartsWith("/api/portal/"));
        var checkedCount = 0;
        foreach (var endpoint in endpoints)
        {
            var path = Regex.Replace(endpoint.RoutePattern.RawText!, @"\{[^}]+\}", m => m.Value.Contains(":guid") ? s.BrandId.ToString() : "test");
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
                var response = await c.SendAsync(request);
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {path}: {response.StatusCode}"); checkedCount++;
            }
        }
        Assert.True(checkedCount > 70); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Only_explicitly_shared_own_brand_content_is_visible_in_list_detail_download_and_export()
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var admin = Staff(f); var (client, _) = await Customer(f, admin, s.BrandId); using var c = client;
        var empty = await c.GetFromJsonAsync<JsonElement>("/api/portal"); Assert.Empty(empty.GetProperty("reports").EnumerateArray()); Assert.Empty(empty.GetProperty("documents").EnumerateArray());
        var own = await Publish(admin, s.BrandId, s.PeriodId); var other = await Publish(admin, s.OtherBrandId, s.OtherPeriodId);
        var shared = await Share(admin, s.BrandId, s.DocumentId); var otherShared = await Share(admin, s.OtherBrandId, s.OtherDocumentId);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.OtherPeriodId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/documents", new PortalShareRequest(s.OtherDocumentId))).StatusCode);
        var summary = await c.GetAsync("/api/portal?brandId=" + s.OtherBrandId); var text = await summary.Content.ReadAsStringAsync();
        Assert.DoesNotContain("OTHER_", text); Assert.DoesNotContain("PRIVATE_NOTE", text); Assert.True(summary.Headers.CacheControl!.NoStore);
        var data = await summary.Content.ReadFromJsonAsync<JsonElement>(); Assert.Single(data.GetProperty("reports").EnumerateArray()); Assert.Single(data.GetProperty("documents").EnumerateArray());
        var reportText = await c.GetStringAsync($"/api/portal/reports/{own}?audience=internal");
        foreach (var secret in new[] { "98765", "PRIVATE_DEAL", "internal", "ovoGrossProfit", "estimatedMonthlyInternalCost", "Password" }) Assert.DoesNotContain(secret, reportText);
        var snapshot = JsonSerializer.Deserialize<JsonElement>(reportText); Assert.Equal(1000000.1256m, snapshot.GetProperty("metrics").GetProperty("netRevenue").GetDecimal());
        Assert.Contains("yalnız seçili ayı içerir", snapshot.GetProperty("explanations")[0].GetProperty("whatHappened").GetString());
        Assert.DoesNotContain("kayıt yok", snapshot.GetProperty("explanations")[0].GetProperty("whatHappened").GetString());
        var csv = await c.GetStringAsync($"/api/portal/reports/{own}/csv"); Assert.Contains("1000000,1256", csv); Assert.DoesNotContain("98765", csv);
        Assert.Equal("PUBLIC_FILE", await c.GetStringAsync($"/api/portal/documents/{shared}/download"));
        foreach (var path in new[] { $"reports/{other}", $"reports/{other}/csv", $"documents/{otherShared}/download", $"reports/{Guid.NewGuid()}" })
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/portal/" + path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(other, "Other brand"))).StatusCode);
    }
    [Fact]
    public async Task Published_versions_are_immutable_and_revoke_removes_access_without_deleting_history()
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var admin = Staff(f); var (client, _) = await Customer(f, admin, s.BrandId); using var c = client;
        var first = await Publish(admin, s.BrandId, s.PeriodId); var original = await c.GetStringAsync($"/api/portal/reports/{first}");
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.MonthlyPerformances.FindAsync(s.PeriodId))!.NetRevenue = 42; await db.SaveChangesAsync(); }
        var second = await Publish(admin, s.BrandId, s.PeriodId); Assert.Equal(original, await c.GetStringAsync($"/api/portal/reports/{first}"));
        var next = await c.GetFromJsonAsync<JsonElement>($"/api/portal/reports/{second}"); Assert.Equal(2, next.GetProperty("version").GetInt32()); Assert.Equal(42, next.GetProperty("metrics").GetProperty("netRevenue").GetDecimal());
        (await admin.PostAsync(Root(s.BrandId) + $"/reports/{first}/revoke", null)).EnsureSuccessStatusCode();
        foreach (var suffix in new[] { "", "/csv" }) Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/portal/reports/{first}{suffix}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(first, "Withdrawn"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Root(s.BrandId) + $"/reports/{first}")).StatusCode);
        var shared = await Share(admin, s.BrandId, s.DocumentId); Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/documents", new PortalShareRequest(s.DocumentId))).StatusCode);
        (await admin.PostAsync(Root(s.BrandId) + $"/documents/{shared}/revoke", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/portal/documents/{shared}/download")).StatusCode);
        using var check = f.Services.CreateScope(); Assert.Equal(2, await check.ServiceProvider.GetRequiredService<AppDbContext>().PortalReports.CountAsync());
    }
    [Theory]
    [InlineData("disable")][InlineData("password")][InlineData("email")]
    public async Task Customer_account_changes_revoke_old_sessions_and_cannot_reassign_brand(string change)
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var admin = Staff(f); var (client, id) = await Customer(f, admin, s.BrandId); using var c = client;
        var request = new PortalAccountRequest(change == "email" ? "new@ovo.test" : "client@ovo.test", "Client", change == "password" ? "Replacement-test-password!" : null, change != "disable");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync(Root(s.OtherBrandId) + $"/accounts/{id}", request)).StatusCode);
        (await admin.PutAsJsonAsync(Root(s.BrandId) + $"/accounts/{id}", request)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/portal")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/users/{id}", new UserAccountRequest("client@ovo.test", "Client", "Admin", null))).StatusCode);
        Assert.DoesNotContain(id.ToString(), await admin.GetStringAsync("/api/team")); Assert.DoesNotContain(id.ToString(), await admin.GetStringAsync("/api/users"));
        using var scope = f.Services.CreateScope(); var audits = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditRecords.ToListAsync();
        Assert.DoesNotContain("Replacement-test-password", JsonSerializer.Serialize(audits)); Assert.DoesNotContain("PasswordHash", JsonSerializer.Serialize(audits));
    }
    [Fact]
    public async Task Questions_are_author_scoped_and_answers_are_explicit_and_immutable()
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var admin = Staff(f); var (client, _) = await Customer(f, admin, s.BrandId); using var c = client;
        var (otherClient, _) = await Customer(f, admin, s.BrandId, "colleague@ovo.test"); using var colleague = otherClient;
        var report = await Publish(admin, s.BrandId, s.PeriodId);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(report, " "))).StatusCode);
        var response = await c.PostAsJsonAsync("/api/portal/questions", new PortalQuestionRequest(report, "İade tutarını açıklar mısınız?")); response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Empty((await colleague.GetFromJsonAsync<JsonElement>("/api/portal")).GetProperty("questions").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(Root(s.OtherBrandId) + $"/questions/{id}/answer", new PortalAnswerRequest("wrong"))).StatusCode);
        (await admin.PostAsJsonAsync(Root(s.BrandId) + $"/questions/{id}/answer", new PortalAnswerRequest("Bu tutar dönem iadelerini içerir."))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(s.BrandId) + $"/questions/{id}/answer", new PortalAnswerRequest("changed"))).StatusCode);
        Assert.Contains("Bu tutar", (await c.GetFromJsonAsync<JsonElement>("/api/portal")).GetProperty("questions")[0].GetProperty("answer").GetString());
    }
    [Theory]
    [InlineData(MonthlyPerformanceStatus.Draft)][InlineData(MonthlyPerformanceStatus.UnderReview)][InlineData(MonthlyPerformanceStatus.Approved)]
    public async Task Unclosed_periods_cannot_be_published(MonthlyPerformanceStatus status)
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var admin = Staff(f);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.MonthlyPerformances.FindAsync(s.PeriodId))!.Status = status; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId))).StatusCode);
    }
    [Fact]
    public async Task Staff_roles_cannot_create_external_access_unless_admin_and_unbound_customers_cannot_log_in()
    {
        await using var f = new WorkflowApiFactory(); var s = await Seed(f); using var partner = Staff(f, "Partner"); using var analyst = Staff(f, "Analyst"); using var admin = Staff(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsJsonAsync(Root(s.BrandId) + "/accounts", new PortalAccountRequest("blocked@ovo.test", "Client", WorkflowApiFactory.TestPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync(Root(s.BrandId) + "/reports", new PortalPublishRequest(s.PeriodId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/accounts", new PortalAccountRequest("client@ovo.test", "Client", "short"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(s.BrandId) + "/accounts", new PortalAccountRequest("admin@ovo.test", "Client", WorkflowApiFactory.TestPassword))).StatusCode);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Add(new UserAccount { Email = "unbound@ovo.test", Name = "Unbound", Role = "BrandClient", PasswordHash = JwtTokenService.HashPassword(WorkflowApiFactory.TestPassword) }); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsJsonAsync("/api/auth/login", new { email = "unbound@ovo.test", password = WorkflowApiFactory.TestPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/portal")).StatusCode);
    }
}
