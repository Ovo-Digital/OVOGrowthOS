using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class TeamWorkTests
{
    private static readonly Guid Analyst = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Admin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static HttpClient Client(WorkflowApiFactory factory, string role = "Admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role));
        return client;
    }
    private static WorkTaskRequest TaskRequest(Guid brandId) => new(Guid.NewGuid(), brandId, Analyst, "Aylık bilgileri iste", "Ciro ve iade dökümünü isteyin.", WorkPriority.Normal,
        WorkKind.General, null, null, null, TeamWork.Today(DateTimeOffset.UtcNow).AddDays(-1));

    [Fact]
    public async Task Assignment_completion_reopen_and_history_preserve_one_record()
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync();
        using var admin = Client(factory); using var analyst = Client(factory, "Analyst");
        var request = TaskRequest(brandId);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/work-tasks", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/work-tasks", request)).StatusCode);
        var mine = await analyst.GetFromJsonAsync<JsonElement>("/api/work-tasks?view=mine");
        Assert.Equal(1, mine.GetProperty("total").GetInt32());
        Assert.True(mine.GetProperty("items")[0].GetProperty("overdue").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await analyst.PutAsJsonAsync($"/api/work-tasks/{request.Id}/completion", new WorkCompletionRequest(true, 0))).StatusCode);
        mine = await analyst.GetFromJsonAsync<JsonElement>("/api/work-tasks?view=mine"); Assert.Equal(0, mine.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/work-tasks/{request.Id}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await analyst.PutAsJsonAsync($"/api/work-tasks/{request.Id}/completion", new WorkCompletionRequest(false, 1))).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.WorkTasks.CountAsync());
        Assert.Equal(3, await db.AuditRecords.CountAsync(x => x.EntityType == "WorkTask"));
    }

    [Fact]
    public async Task Changing_deadline_updates_overdue_and_stale_revision_cannot_overwrite()
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync(); using var client = Client(factory);
        var request = TaskRequest(brandId); await client.PostAsJsonAsync("/api/work-tasks", request);
        var changed = request with { DueOn = TeamWork.Today(DateTimeOffset.UtcNow).AddDays(2) };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/work-tasks/{request.Id}", changed)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/work-tasks/{request.Id}", request)).StatusCode);
        var overdue = await client.GetFromJsonAsync<JsonElement>("/api/work-tasks?view=overdue");
        Assert.Equal(0, overdue.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Analyst_cannot_assign_or_complete_another_persons_task()
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync(); using var admin = Client(factory); using var analyst = Client(factory, "Analyst");
        var request = TaskRequest(brandId) with { AssigneeId = Admin };
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync("/api/work-tasks", request)).StatusCode);
        await admin.PostAsJsonAsync("/api/work-tasks", request);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync($"/api/work-tasks/{request.Id}/completion", new WorkCompletionRequest(true, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PutAsJsonAsync($"/api/brands/{brandId}/follow-up", new FollowUpRequest(Analyst, LeadStage.New, "", null, "", 0))).StatusCode);
    }

    [Fact]
    public async Task Unknown_inactive_and_invalid_targets_are_rejected()
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync(); using var client = Client(factory);
        var request = TaskRequest(brandId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/work-tasks", request with { AssigneeId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/work-tasks", request with { BrandId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/work-tasks", request with { Title = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/work-tasks", request with { Kind = WorkKind.MonthlyClose })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserAccounts.FindAsync(Analyst))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/work-tasks", request)).StatusCode);
    }

    [Fact]
    public async Task Notes_and_followup_are_persisted_without_changing_commercial_state()
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync(); using var client = Client(factory);
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var follow = new FollowUpRequest(Admin, LeadStage.WaitingForInformation, "İade dökümü bekleniyor", today, "Markayı arayın", 0);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/brands/{brandId}/follow-up", follow)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/brands/{brandId}/follow-up", follow)).StatusCode);
        var note = new ContactNoteRequest(Guid.NewGuid(), today, "Marka dökümü yarın iletecek.");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/brands/{brandId}/contact-notes", note)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/brands/{brandId}/contact-notes", note)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/brands/{brandId}/contact-notes", note with { Id = Guid.NewGuid(), ContactOn = today.AddDays(1) })).StatusCode);
        var persisted = await client.GetFromJsonAsync<JsonElement>($"/api/brands/{brandId}/follow-up");
        Assert.Equal(today.ToString("yyyy-MM-dd"), persisted.GetProperty("lastContactOn").GetString());
        var reminders = await client.GetFromJsonAsync<JsonElement>("/api/tasks");
        Assert.Contains(reminders.EnumerateArray(), x => x.GetProperty("kind").GetString() == "Görüşme takibi");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.MonthlyPerformances.ToListAsync()); Assert.Empty(await db.Deals.ToListAsync());
        Assert.Equal(1, await db.BrandContactNotes.CountAsync());
        Assert.DoesNotContain(reminders.EnumerateArray(), x => x.GetProperty("kind").GetString() == "Eksik dönem");
    }

    [Fact]
    public async Task Monthly_close_and_renewal_targets_are_unique_and_brand_scoped()
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync(); using var client = Client(factory);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brandId, Name = "Test anlaşma" };
        db.Add(deal); await db.SaveChangesAsync();
        var request = TaskRequest(brandId) with { Kind = WorkKind.MonthlyClose, DealId = deal.Id, Year = 2026, Month = 9 };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/work-tasks", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/work-tasks", request with { Id = Guid.NewGuid() })).StatusCode);
        var renewal = request with { Id = Guid.NewGuid(), Kind = WorkKind.ContractRenewal, Year = null, Month = null };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/work-tasks", renewal)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/work-tasks", renewal with { Id = Guid.NewGuid() })).StatusCode);
        var other = new Brand { Name = "Başka marka" }; db.Add(other); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/work-tasks", request with { Id = Guid.NewGuid(), BrandId = other.Id })).StatusCode);
    }

    [Fact]
    public async Task Lead_list_and_staff_directory_are_readable_without_passwords()
    {
        await using var factory = new WorkflowApiFactory(); await factory.SeedAsync(); using var client = Client(factory, "Analyst");
        var leads = await client.GetAsync("/api/lead-follow-ups"); Assert.Equal(HttpStatusCode.OK, leads.StatusCode);
        var team = await client.GetStringAsync("/api/team"); Assert.DoesNotContain("password", team, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("token", team, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(LeadStage.WaitingForInformation, "", null, "")]
    [InlineData(LeadStage.MeetingPlanned, "", null, "Arayın")]
    [InlineData(LeadStage.Contacted, "", "2026-09-15", "")]
    public async Task Followup_requires_waiting_reason_meeting_date_and_next_step(LeadStage stage, string reason, string? date, string nextStep)
    {
        await using var factory = new WorkflowApiFactory(); var brandId = await factory.SeedAsync(); using var client = Client(factory);
        var request = new FollowUpRequest(Admin, stage, reason, date is null ? null : DateOnly.Parse(date), nextStep, 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/brands/{brandId}/follow-up", request)).StatusCode);
    }
}
