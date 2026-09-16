using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class MonthlyTargetTests
{
    private static string Path(Guid brand) => $"/api/brands/{brand}/targets/2026/8/TRY";
    private static MonthlyTargetRequest Input(int revision = 0) => new(1200000, 100000, .4m, WorkflowApiFactory.AccountId("partner@ovo.test"), "Aylık iş planına göre hedef belirlendi", revision);

    [Fact]
    public async Task Missing_target_is_null_and_reasoned_revisions_preserve_closed_financials()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f);
        var before = await c.GetStringAsync($"/api/performance/{s.PeriodId}");
        Assert.Equal(JsonValueKind.Null, (await c.GetFromJsonAsync<JsonElement>(Path(s.BrandId))).GetProperty("target").ValueKind);
        var response = await c.PutAsJsonAsync(Path(s.BrandId), Input()); response.EnsureSuccessStatusCode();
        var target = (await response.Content.ReadFromJsonAsync<MonthlyTarget>())!;
        Assert.Equal(1, target.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(s.BrandId), Input())).StatusCode);
        (await c.PutAsJsonAsync(Path(s.BrandId), Input(1) with { NetRevenueGoal = 1300000, Reason = "Yeni kampanya planı görüşüldü" })).EnsureSuccessStatusCode();
        var history = await c.GetFromJsonAsync<JsonElement>($"/api/targets/{target.Id}/history");
        Assert.Equal(2, history.GetProperty("total").GetInt32());
        Assert.Contains("Yeni kampanya planı", history.ToString());
        var comparison = (await c.GetFromJsonAsync<JsonElement>(Path(s.BrandId))).GetProperty("comparison");
        Assert.True(comparison.GetProperty("isClosed").GetBoolean());
        Assert.Equal(-299999.8744m, comparison.GetProperty("metrics")[0].GetProperty("difference").GetDecimal());
        Assert.Equal(before, await c.GetStringAsync($"/api/performance/{s.PeriodId}"));
    }

    [Fact]
    public async Task Validation_roles_inactive_owners_and_currency_are_enforced()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f); using var analyst = CustomerPortalTests.Staff(f, "Analyst");
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync(Path(s.BrandId), Input())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync(Path(s.BrandId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path(s.BrandId), new { netRevenueGoal = 1000, ownerId = Input().OwnerId, reason = "Eksik bütçe veya marj sıfıra dönüşmemeli", revision = 0 })).StatusCode);
        foreach (var bad in new[] { Input() with { Reason = " " }, Input() with { AdBudget = -1 }, Input() with { NetRevenueGoal = 1.12345m }, Input() with { ContributionMarginGoal = 1.01m }, Input() with { AdBudget = decimal.MaxValue } })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path(s.BrandId), bad)).StatusCode);
        var (client, clientId) = await CustomerPortalTests.Customer(f, c, s.BrandId); using var customer = client;
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(s.BrandId), Input() with { OwnerId = clientId })).StatusCode);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.UserAccounts.FindAsync(Input().OwnerId))!.IsActive = false; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path(s.BrandId), Input())).StatusCode);
        var valid = Input() with { OwnerId = WorkflowApiFactory.AccountId("admin@ovo.test") };
        (await c.PutAsJsonAsync(Path(s.BrandId).Replace("TRY", "USD"), valid)).EnsureSuccessStatusCode();
        var otherCurrency = await c.GetFromJsonAsync<JsonElement>(Path(s.BrandId).Replace("TRY", "USD"));
        Assert.Equal(JsonValueKind.Null, otherCurrency.GetProperty("comparison").GetProperty("metrics")[0].GetProperty("actual").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path(s.BrandId).Replace("TRY", "try"), valid)).StatusCode);
    }

    [Fact]
    public async Task Follow_up_is_version_checked_traceable_and_never_duplicated_even_after_completion_or_revision()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f);
        var saved = await c.PutAsJsonAsync(Path(s.BrandId), Input()); saved.EnsureSuccessStatusCode(); var target = (await saved.Content.ReadFromJsonAsync<MonthlyTarget>())!;
        var period = await c.GetFromJsonAsync<JsonElement>($"/api/performance/{s.PeriodId}");
        var action = new TargetActionRequest(TargetMetric.NetRevenue, Input().OwnerId, new DateOnly(2026, 9, 30), "İade nedenlerini kaynak rapordan incele", 1, period.GetProperty("updatedAt").GetDateTimeOffset());
        var path = $"/api/targets/{target.Id}/actions";
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(path, action with { TargetRevision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(path, action with { PerformanceUpdatedAt = action.PerformanceUpdatedAt.AddSeconds(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(path, action with { Metric = (TargetMetric)9 })).StatusCode);
        var created = await c.PostAsJsonAsync(path, action); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var taskId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(path, action)).StatusCode);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var task = (await db.WorkTasks.FindAsync(taskId))!; task.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); }
        (await c.PutAsJsonAsync(Path(s.BrandId), Input(1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(path, action with { TargetRevision = 2 })).StatusCode);
        using var verify = f.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await context.WorkTasks.ToListAsync()); var link = Assert.Single(await context.TargetActions.ToListAsync());
        Assert.Equal(s.PeriodId, link.PerformanceId); Assert.Equal(1, link.TargetRevision);
        Assert.Equal(100000m, (await context.MonthlyPerformances.FindAsync(s.PeriodId))!.OvoFee);
    }

    [Fact]
    public async Task No_actuals_or_no_deviation_cannot_create_a_follow_up()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f);
        var saved = await c.PutAsJsonAsync(Path(s.BrandId), Input() with { NetRevenueGoal = 0, AdBudget = 0 }); saved.EnsureSuccessStatusCode();
        var target = (await saved.Content.ReadFromJsonAsync<MonthlyTarget>())!;
        var period = await c.GetFromJsonAsync<JsonElement>($"/api/performance/{s.PeriodId}");
        var action = new TargetActionRequest(TargetMetric.NetRevenue, Input().OwnerId, new DateOnly(2026, 9, 30), "Örnek takip görevi", 1, period.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/targets/{target.Id}/actions", action)).StatusCode);
        (await c.PostAsJsonAsync($"/api/targets/{target.Id}/actions", action with { Metric = TargetMetric.AdSpend })).EnsureSuccessStatusCode();
        var future = await c.PutAsJsonAsync(Path(s.BrandId).Replace("/8/", "/9/"), Input()); future.EnsureSuccessStatusCode();
        var futureTarget = (await future.Content.ReadFromJsonAsync<MonthlyTarget>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/targets/{futureTarget.Id}/actions", action)).StatusCode);
    }
}
