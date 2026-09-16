using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PeriodEditingTests
{
    internal static async Task SetVersion(HttpClient client, Guid id)
    {
        var p = await client.GetFromJsonAsync<JsonElement>($"/api/performance/{id}");
        client.DefaultRequestHeaders.Remove("If-Match");
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{p.GetProperty("updatedAt").GetString()}\"");
    }
    private static async Task<Guid> Seed(WorkflowApiFactory factory)
    {
        var brand = await factory.SeedAsync(); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brand, Name = "Düzenleme denemesi", Status = DealStatus.Active, DealType = DealType.FixedRetainer, MonthlyRetainer = 50000, EstimatedMonthlyInternalCost = 20000, Currency = "TRY" };
        var p = new MonthlyPerformance { BrandId = brand, Deal = deal, DealId = deal.Id, Year = 2026, Month = 8,
            GrossSales = 1200000, Vat = 200000, Refunds = 40000, Cogs = 400000, MetaSpend = 100000, Orders = 500, Sessions = 25000, NewCustomers = 300, ReturningCustomers = 200 };
        MonthlyPerformanceCalculator.Calculate(p, deal); db.Add(p); await db.SaveChangesAsync(); return p.Id;
    }
    private static async Task<HttpResponseMessage> Move(HttpClient c, Guid id, string action, string reason = "Kaynak raporla yeniden kontrol gerekli")
    {
        await SetVersion(c, id); return await c.PostAsJsonAsync($"/api/performance/{id}/{action}", new { reason });
    }

    [Fact]
    public async Task Return_edit_resubmit_approve_and_lock_preserve_two_person_review_and_financials()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var admin = CustomerPortalTests.Staff(f); using var partner = CustomerPortalTests.Staff(f, "Partner");
        Assert.Equal(HttpStatusCode.OK, (await Move(partner, id, "submit")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Move(partner, id, "approve")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(admin, id, "approve")).StatusCode);
        var input = (await admin.GetFromJsonAsync<PerformanceRequest>($"/api/performance/{id}"))! with { Refunds = 50000 };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(admin, id, "return")).StatusCode);
        var draft = await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{id}");
        Assert.Equal("Draft", draft.GetProperty("status").GetString()); Assert.Equal("", draft.GetProperty("reviewedBy").GetString());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("approvedAt").ValueKind);
        await SetVersion(partner, id);
        var preview = await partner.PostAsJsonAsync($"/api/performance/{id}/preview", input); preview.EnsureSuccessStatusCode();
        Assert.Equal(950000, (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("netRevenue").GetDecimal());
        Assert.Equal(960000, (await partner.GetFromJsonAsync<JsonElement>($"/api/performance/{id}")).GetProperty("netRevenue").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await partner.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(partner, id, "submit")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Move(partner, id, "approve")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(admin, id, "approve")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(admin, id, "lock")).StatusCode);
        var locked = await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{id}");
        Assert.Equal("Locked", locked.GetProperty("status").GetString()); Assert.Equal(50000, locked.GetProperty("ovoFee").GetDecimal());
        Assert.Equal(30000, locked.GetProperty("ovoGrossProfit").GetDecimal()); Assert.Equal(400000, locked.GetProperty("brandContributionProfit").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(admin, id, "unlock")).StatusCode);
        Assert.Equal("Draft", (await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{id}")).GetProperty("status").GetString());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditRecords.AnyAsync(a => a.EntityId == id.ToString() && a.Action == "MonthlyPerformanceReturned" && a.Reason == "Kaynak raporla yeniden kontrol gerekli"));
    }

    [Fact]
    public async Task Missing_and_stale_versions_cannot_write_preview_or_transition()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var c = CustomerPortalTests.Staff(f);
        var input = (await c.GetFromJsonAsync<PerformanceRequest>($"/api/performance/{id}"))!;
        Assert.Equal((HttpStatusCode)428, (await c.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await c.PostAsync($"/api/performance/{id}/submit", null)).StatusCode);
        await SetVersion(c, id);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/performance/{id}", input with { Refunds = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/performance/{id}/preview", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/performance/{id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/performance/{id}/adjustments", new { amount = 1, reason = "Eski ekran" })).StatusCode);
        await SetVersion(c, id);
        foreach (var changed in new[] { input with { BrandId = Guid.NewGuid() }, input with { DealId = Guid.NewGuid() }, input with { Month = 9 }, input with { Year = 2025 } })
            Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/performance/{id}", changed)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/performance/{id}", input with { Refunds = -1 })).StatusCode);
        Assert.Equal(1000000, (await c.GetFromJsonAsync<JsonElement>($"/api/performance/{id}")).GetProperty("netRevenue").GetDecimal());
    }

    [Fact]
    public async Task Return_requires_reason_and_permissions_and_never_opens_paid_periods()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var admin = CustomerPortalTests.Staff(f); using var analyst = CustomerPortalTests.Staff(f, "Analyst");
        var input = (await admin.GetFromJsonAsync<PerformanceRequest>($"/api/performance/{id}"))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        await Move(admin, id, "submit");
        Assert.Equal(HttpStatusCode.BadRequest, (await Move(admin, id, "return", " ")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Move(analyst, id, "return")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/performance/{id}/adjustments", new { amount = 1, reason = "Onaysız değişiklik" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(admin, id, "return")).StatusCode);
        foreach (var status in new[] { MonthlyPerformanceStatus.Invoiced, MonthlyPerformanceStatus.Paid })
        {
            using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.MonthlyPerformances.FindAsync(id); p!.Status = status; await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.Conflict, (await Move(admin, id, "return")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Move(admin, id, "unlock")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/performance/{id}", input)).StatusCode);
        }
    }

    [Fact]
    public async Task Submitted_identity_survives_email_change_and_still_requires_another_approver()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var admin = CustomerPortalTests.Staff(f);
        (await Move(admin, id, "submit")).EnsureSuccessStatusCode();
        Guid accountId;
        using (var scope = f.Services.CreateScope()) accountId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserAccounts.SingleAsync(u => u.Email == "admin@ovo.test")).Id;
        (await admin.PutAsJsonAsync($"/api/users/{accountId}", new UserAccountRequest("changed@ovo.test", "Yönetici", "Admin", null))).EnsureSuccessStatusCode();
        using var changed = f.CreateClient();
        var login = await changed.PostAsJsonAsync("/api/auth/login", new { email = "changed@ovo.test", password = WorkflowApiFactory.TestPassword }); login.EnsureSuccessStatusCode();
        changed.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Move(changed, id, "approve")).StatusCode);
        using var partner = CustomerPortalTests.Staff(f, "Partner");
        Assert.Equal(HttpStatusCode.OK, (await Move(partner, id, "approve")).StatusCode);
    }

    [Fact]
    public async Task Unlock_and_edit_never_change_the_published_customer_version()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var customer = client;
        var publish = await admin.PostAsJsonAsync($"/api/portal-management/brands/{s.BrandId}/reports", new PortalPublishRequest(s.PeriodId)); publish.EnsureSuccessStatusCode();
        var reportId = (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var before = await customer.GetStringAsync($"/api/portal/reports/{reportId}");
        (await Move(admin, s.PeriodId, "unlock")).EnsureSuccessStatusCode();
        var input = (await admin.GetFromJsonAsync<PerformanceRequest>($"/api/performance/{s.PeriodId}"))! with { Refunds = 12345.6789m };
        await SetVersion(admin, s.PeriodId);
        (await admin.PutAsJsonAsync($"/api/performance/{s.PeriodId}", input)).EnsureSuccessStatusCode();
        Assert.Equal(before, await customer.GetStringAsync($"/api/portal/reports/{reportId}"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/portal-management/brands/{s.BrandId}/reports", new PortalPublishRequest(s.PeriodId))).StatusCode);
    }

    [Fact]
    public async Task Precision_overflow_and_count_overflow_are_rejected_without_losing_existing_adjustments()
    {
        await using var f = new WorkflowApiFactory(); var id = await Seed(f); using var admin = CustomerPortalTests.Staff(f);
        var input = (await admin.GetFromJsonAsync<PerformanceRequest>($"/api/performance/{id}"))!;
        await SetVersion(admin, id);
        foreach (var invalid in new[] { input with { Refunds = 1.12345m }, input with { Cogs = decimal.MaxValue }, input with { Orders = int.MaxValue, NewCustomers = int.MaxValue, ReturningCustomers = int.MaxValue } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/performance/{id}", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/performance/{id}/adjustments", new { amount = 1.12345m, reason = "Hassasiyet" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/performance/{id}/adjustments", new { amount = -500.1256m, reason = "Örnek düzeltme" })).EnsureSuccessStatusCode();
        await SetVersion(admin, id);
        var preview = await admin.PostAsJsonAsync($"/api/performance/{id}/preview", input with { Refunds = 0 }); preview.EnsureSuccessStatusCode();
        Assert.Equal(49499.8744m, (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ovoFee").GetDecimal());
        (await admin.PutAsJsonAsync($"/api/performance/{id}", input with { Refunds = 0 })).EnsureSuccessStatusCode();
        Assert.Single((await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{id}")).GetProperty("adjustments").EnumerateArray());
    }
}
