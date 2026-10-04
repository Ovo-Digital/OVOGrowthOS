using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortalSubmissionTests
{
    private static string Root(Guid brand) => $"/api/portal-management/brands/{brand}";
    private static object Body(int year, int month, decimal? gross = 125000.5m, decimal? refunds = 3500m,
        decimal? meta = 20000m, decimal? google = 8000m, string note = "Kampanya dönemi") =>
        new { year, month, grossSales = gross, refunds = refunds, metaSpend = meta, googleSpend = google, note };

    [Fact]
    public async Task Customer_submits_a_period_and_the_staff_sees_it_with_the_revision_history_in_audit()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;

        var created = await c.PutAsJsonAsync("/api/portal/submissions", Body(2026, 9));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, first.GetProperty("revision").GetInt32());

        var mine = await c.GetFromJsonAsync<JsonElement>("/api/portal/submissions");
        var rows = mine.EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal(125000.5m, rows[0].GetProperty("grossSales").GetDecimal());
        Assert.Equal("Kampanya dönemi", rows[0].GetProperty("note").GetString());

        var revised = await c.PutAsJsonAsync("/api/portal/submissions", Body(2026, 9, gross: 130000m, refunds: null, note: ""));
        revised.EnsureSuccessStatusCode();
        Assert.Equal(2, (await revised.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32());
        var after = (await c.GetFromJsonAsync<JsonElement>("/api/portal/submissions")).EnumerateArray().Single();
        Assert.Equal(130000m, after.GetProperty("grossSales").GetDecimal());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("refunds").ValueKind);

        var staff = await admin.GetFromJsonAsync<JsonElement>(Root(s.BrandId) + "/submissions");
        Assert.Single(staff.EnumerateArray());
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.AuditRecords.CountAsync(x => x.Action == "PortalPeriodSubmitted"));
        // The submission never writes into the monthly performance.
        var period = await db.MonthlyPerformances.SingleAsync(x => x.BrandId == s.BrandId);
        Assert.Equal(1000000.1256m, period.NetRevenue);
    }

    [Fact]
    public async Task Submissions_never_leak_between_brands_and_the_customer_sees_only_own_brand()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        (await c.PutAsJsonAsync("/api/portal/submissions", Body(2026, 9))).EnsureSuccessStatusCode();

        Assert.Single((await admin.GetFromJsonAsync<JsonElement>(Root(s.BrandId) + "/submissions")).EnumerateArray());
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>(Root(s.OtherBrandId) + "/submissions")).EnumerateArray());
        Assert.Single((await c.GetFromJsonAsync<JsonElement>("/api/portal/submissions")).EnumerateArray());
    }

    [Theory]
    [InlineData(2026, 13, 100d, 10d, "ayı 1–12")]
    [InlineData(2019, 9, 100d, 10d, "2020–2100")]
    [InlineData(2026, 9, -5d, 10d, "sıfır veya üzerinde")]
    [InlineData(2026, 9, null, null, "En az bir tutar")]
    public async Task Invalid_submissions_are_rejected_with_a_turkish_message(int year, int month, double? gross, double? refunds, string expected)
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        var response = await c.PutAsJsonAsync("/api/portal/submissions",
            new { year, month, grossSales = (decimal?)gross, refunds = (decimal?)refunds, metaSpend = (decimal?)null, googleSpend = (decimal?)null, note = expected == "En az bir tutar" ? "" : "not" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;
        Assert.Contains(expected, message);
    }

    [Fact]
    public async Task Applying_the_submission_records_the_approval_and_never_writes_the_period()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        var submission = await c.PutAsJsonAsync("/api/portal/submissions", Body(2026, 8));
        var id = (await submission.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using (var scope = f.Services.CreateAsyncScope())
        {
            // Seed the period as Draft so the approval path is exercised before the lock check.
            var setupDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await setupDb.MonthlyPerformances.SingleAsync(x => x.BrandId == s.BrandId)).Status = MonthlyPerformanceStatus.Draft;
            await setupDb.SaveChangesAsync();
        }

        var applied = await admin.PostAsJsonAsync($"{Root(s.BrandId)}/submissions/{id}/apply", new { reason = "Taslak doldurulacak" });
        applied.EnsureSuccessStatusCode();
        var values = await applied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(125000.5m, values.GetProperty("grossSales").GetDecimal());
        Assert.Equal(8000m, values.GetProperty("googleSpend").GetDecimal());

        await using var scope2 = f.Services.CreateAsyncScope();
        var db = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditRecords.SingleAsync(x => x.Action == "PortalPeriodSubmissionApplied");
        Assert.Contains("Taslak doldurulacak", audit.Reason);
        Assert.Contains("125000.5", audit.NewValueJson);
        Assert.Equal(2, await db.MonthlyPerformances.CountAsync(x => x.BrandId == s.BrandId || x.BrandId == s.OtherBrandId));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Root(s.BrandId)}/submissions/{id}/apply", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"{Root(s.OtherBrandId)}/submissions/{id}/apply", new { reason = "x" })).StatusCode);

        var period = await db.MonthlyPerformances.SingleAsync(x => x.BrandId == s.BrandId);
        period.Status = MonthlyPerformanceStatus.Locked;
        await db.SaveChangesAsync();
        var locked = await admin.PostAsJsonAsync($"{Root(s.BrandId)}/submissions/{id}/apply", new { reason = "kilitli" });
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("kilitli", (await locked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        // The locked period still carries the values it had; the approval never wrote anything.
        Assert.Equal(1000000.1256m, period.NetRevenue);
    }

    [Fact]
    public async Task Customers_and_analysts_cannot_list_or_apply_submissions()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f);
        using var admin = CustomerPortalTests.Staff(f); var analyst = CustomerPortalTests.Staff(f, "Analyst");
        var (client, _) = await CustomerPortalTests.Customer(f, admin, s.BrandId); using var c = client;
        var submission = await c.PutAsJsonAsync("/api/portal/submissions", Body(2026, 9));
        var id = (await submission.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"{Root(s.BrandId)}/submissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync($"{Root(s.BrandId)}/submissions/{id}/apply", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"{Root(s.BrandId)}/submissions/{id}/apply", new { reason = "x" })).StatusCode);
    }
}
