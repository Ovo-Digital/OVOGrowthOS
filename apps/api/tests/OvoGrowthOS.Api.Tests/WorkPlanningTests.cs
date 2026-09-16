using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class WorkPlanningTests
{
    private static readonly Guid Owner = WorkflowApiFactory.AccountId("partner@ovo.test");
    private static readonly DateOnly Week = new(2026, 9, 14);
    private static TemplateScope Start => new(WorkTemplateKind.BrandStart, null, 0, 0, new DateOnly(2026, 9, 17));
    private static string TemplatePath(Guid brand, string action) => $"/api/brands/{brand}/work-template/{action}";
    private static async Task<TemplateApplyRequest> Preview(HttpClient c, Guid brand, TemplateScope scope)
    {
        var response = await c.PostAsJsonAsync(TemplatePath(brand, "preview"), scope); response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(scope, data.GetProperty("items").EnumerateArray().Select(x => {
            var existing = x.GetProperty("existing"); var reuse = existing.ValueKind != JsonValueKind.Null;
            return new TemplateAssignment(x.GetProperty("key").GetString()!, reuse ? existing.GetProperty("assigneeId").GetGuid() : Owner,
                DateOnly.Parse((reuse ? existing : x).GetProperty("dueOn").GetString()!), reuse ? existing.GetProperty("id").GetGuid() : null,
                reuse ? existing.GetProperty("revision").GetInt32() : null);
        }).ToArray());
    }
    private static JsonElement Row(JsonElement data, Guid user) => data.GetProperty("rows").EnumerateArray().Single(x => x.GetProperty("user").GetProperty("id").GetGuid() == user);
    private static async Task<Guid> CreateTask(HttpClient c, Guid brand)
    {
        var id = Guid.NewGuid(); var r = new WorkTaskRequest(id, brand, Owner, "Saat planı denemesi", "Test işi", WorkPriority.Normal, WorkKind.General, null, null, null, Week.AddDays(3));
        (await c.PostAsJsonAsync("/api/work-tasks", r)).EnsureSuccessStatusCode(); return id;
    }
    [Fact]
    public async Task Template_preview_is_read_only_and_apply_is_once_per_brand_scope_without_financial_changes()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f);
        var before = await c.GetStringAsync($"/api/performance/{s.PeriodId}"); var apply = await Preview(c, s.BrandId, Start);
        using (var check = f.Services.CreateScope()) Assert.Empty(await check.ServiceProvider.GetRequiredService<AppDbContext>().WorkTasks.ToListAsync());
        var saved = await c.PostAsJsonAsync(TemplatePath(s.BrandId, "apply"), apply); saved.EnsureSuccessStatusCode();
        Assert.Equal(3, (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(TemplatePath(s.BrandId, "apply"), apply)).StatusCode);
        using (var check = f.Services.CreateScope()) {
            var db = check.ServiceProvider.GetRequiredService<AppDbContext>(); foreach (var task in await db.WorkTasks.ToListAsync()) task.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
            Assert.Single(await db.WorkTemplateRuns.ToListAsync()); Assert.Equal(3, await db.WorkTemplateTasks.CountAsync());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(TemplatePath(s.BrandId, "preview"), Start)).StatusCode);
        Assert.Equal(before, await c.GetStringAsync($"/api/performance/{s.PeriodId}"));
    }
    [Fact]
    public async Task Monthly_template_reuses_existing_close_task_and_rejects_stale_preview()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f);
        var period = await c.GetFromJsonAsync<JsonElement>($"/api/performance/{s.PeriodId}"); var deal = period.GetProperty("dealId").GetGuid(); var id = Guid.NewGuid();
        var task = new WorkTaskRequest(id, s.BrandId, Owner, "Mevcut kapanış işi", "Korunacak açıklama", WorkPriority.High, WorkKind.MonthlyClose, deal, 2026, 8, Week);
        (await c.PostAsJsonAsync("/api/work-tasks", task)).EnsureSuccessStatusCode();
        var scope = new TemplateScope(WorkTemplateKind.MonthlyClose, deal, 2026, 8, Week); var oldPreview = await Preview(c, s.BrandId, scope);
        (await c.PutAsJsonAsync($"/api/work-tasks/{id}/completion", new WorkCompletionRequest(true, 0))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(TemplatePath(s.BrandId, "apply"), oldPreview)).StatusCode);
        var fresh = await Preview(c, s.BrandId, scope); var result = await c.PostAsJsonAsync(TemplatePath(s.BrandId, "apply"), fresh); result.EnsureSuccessStatusCode();
        var counts = await result.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(2, counts.GetProperty("created").GetInt32()); Assert.Equal(1, counts.GetProperty("reused").GetInt32());
        using var check = f.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>(); var existing = (await db.WorkTasks.FindAsync(id))!;
        Assert.Equal("Mevcut kapanış işi", existing.Title); Assert.Equal("Korunacak açıklama", existing.Description); Assert.NotNull(existing.CompletedAt); Assert.Equal(Week, existing.DueOn);
        Assert.Equal(3, await db.WorkTasks.CountAsync()); Assert.Single(await db.WorkTasks.Where(x => x.Kind == WorkKind.MonthlyClose).ToListAsync());
    }
    [Fact]
    public async Task Invalid_or_unauthorized_template_never_partially_creates_tasks()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); using var c = CustomerPortalTests.Staff(f); using var analyst = CustomerPortalTests.Staff(f, "Analyst");
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync(TemplatePath(brand, "preview"), Start)).StatusCode);
        var valid = await Preview(c, brand, Start); var assignments = valid.Items.ToArray(); assignments[2] = assignments[2] with { AssigneeId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(TemplatePath(brand, "apply"), valid with { Items = assignments })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(TemplatePath(brand, "apply"), valid with { Items = [valid.Items[0], valid.Items[0], valid.Items[2]] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(TemplatePath(brand, "preview"), Start with { StartOn = new DateOnly(2100, 12, 31) })).StatusCode);
        using var check = f.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.WorkTasks.ToListAsync()); Assert.Empty(await db.WorkTemplateRuns.ToListAsync());
    }
    [Fact]
    public async Task Capacity_plan_preview_and_completion_never_invent_actual_hours_or_change_money()
    {
        await using var f = new WorkflowApiFactory(); var s = await CustomerPortalTests.Seed(f); using var c = CustomerPortalTests.Staff(f);
        var before = await c.GetStringAsync($"/api/performance/{s.PeriodId}"); var task = await CreateTask(c, s.BrandId);
        var input = new TaskHourPlanRequest(Week, 20, 0, 0, "Haftalık görev planı"); (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", input)).EnsureSuccessStatusCode();
        var path = $"/api/work-planning?weekStart={Week:yyyy-MM-dd}"; var missing = Row(await c.GetFromJsonAsync<JsonElement>(path), Owner);
        Assert.Equal(JsonValueKind.Null, missing.GetProperty("result").GetProperty("remainingHours").ValueKind); Assert.Equal(20, missing.GetProperty("result").GetProperty("plannedHours").GetDecimal());
        var capacity = new WeeklyCapacityRequest(Week, 40, 8, 0, "Haftalık kullanılabilir saat"); (await c.PutAsJsonAsync($"/api/team/{Owner}/capacity", capacity)).EnsureSuccessStatusCode();
        var preview = Row(await c.GetFromJsonAsync<JsonElement>(path + $"&previewUserId={Owner}&additionalHours=15"), Owner);
        Assert.Equal(12, preview.GetProperty("result").GetProperty("remainingHours").GetDecimal()); Assert.Equal(-3, preview.GetProperty("preview").GetProperty("remainingHours").GetDecimal());
        Assert.True(preview.GetProperty("preview").GetProperty("overloaded").GetBoolean());
        (await c.PutAsJsonAsync($"/api/work-tasks/{task}/completion", new WorkCompletionRequest(true, 0))).EnsureSuccessStatusCode();
        var completed = Row(await c.GetFromJsonAsync<JsonElement>(path), Owner).GetProperty("result");
        Assert.Equal(20, completed.GetProperty("completedTaskPlannedHours").GetDecimal()); Assert.Equal(12, completed.GetProperty("remainingHours").GetDecimal()); Assert.False(completed.TryGetProperty("actualHours", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", input with { Revision = 1, TaskRevision = 1 })).StatusCode);
        (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", input with { Revision = 1, TaskRevision = 1, Hours = 0 })).EnsureSuccessStatusCode();
        Assert.Equal(before, await c.GetStringAsync($"/api/performance/{s.PeriodId}"));
        using var check = f.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Single(await db.TaskHourPlans.ToListAsync()); Assert.Single(await db.WeeklyCapacities.ToListAsync());
    }
    [Fact]
    public async Task Task_reassignment_moves_plan_to_current_owner_and_old_task_revision_is_rejected()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); using var c = CustomerPortalTests.Staff(f); var taskId = await CreateTask(c, brand);
        var plan = new TaskHourPlanRequest(Week, 7.25m, 0, 0, "Plan sorumlusu kontrolü");
        (await c.PutAsJsonAsync($"/api/work-tasks/{taskId}/hour-plan", plan)).EnsureSuccessStatusCode();
        var newOwner = WorkflowApiFactory.AccountId("analyst@ovo.test");
        var changed = new WorkTaskRequest(taskId, brand, newOwner, "Saat planı denemesi", "Yeni sorumlu", WorkPriority.Normal, WorkKind.General, null, null, null, Week.AddDays(3), 0);
        (await c.PutAsJsonAsync($"/api/work-tasks/{taskId}", changed)).EnsureSuccessStatusCode();
        var data = await c.GetFromJsonAsync<JsonElement>($"/api/work-planning?weekStart={Week:yyyy-MM-dd}");
        Assert.Equal(0, Row(data, Owner).GetProperty("result").GetProperty("plannedHours").GetDecimal());
        Assert.Equal(7.25m, Row(data, newOwner).GetProperty("result").GetProperty("plannedHours").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/work-tasks/{taskId}/hour-plan", plan with { Revision = 1 })).StatusCode);
    }
    [Fact]
    public async Task Invalid_missing_stale_or_inactive_capacity_and_task_plans_are_rejected()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); using var c = CustomerPortalTests.Staff(f); using var analyst = CustomerPortalTests.Staff(f, "Analyst"); var task = await CreateTask(c, brand);
        var capacity = new WeeklyCapacityRequest(Week, 40, 8, 0, "Çalışma planı"); var path = $"/api/team/{Owner}/capacity";
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync(path, capacity)).StatusCode);
        foreach (var bad in new[] { capacity with { WeekStart = Week.AddDays(1) }, capacity with { WorkingHours = -1 }, capacity with { UnavailableHours = 41 }, capacity with { WorkingHours = 40.001m } })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(path, bad)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(path, new { weekStart = Week, revision = 0, reason = "Eksik alan" })).StatusCode);
        (await c.PutAsJsonAsync(path, capacity)).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(path, capacity)).StatusCode);
        var plan = new TaskHourPlanRequest(Week, 4, 0, 0, "Görev planı");
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", plan)).StatusCode);
        (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", plan)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", plan)).StatusCode);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.UserAccounts.FindAsync(Owner))!.IsActive = false; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(path, capacity with { Revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", plan with { Revision = 1 })).StatusCode);
        (await c.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", plan with { Revision = 1, Hours = 0 })).EnsureSuccessStatusCode();
    }
}
