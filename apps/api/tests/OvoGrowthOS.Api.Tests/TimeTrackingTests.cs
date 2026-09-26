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

public sealed class TimeTrackingTests
{
    private static readonly Guid Analyst = WorkflowApiFactory.AccountId("analyst@ovo.test");
    private static readonly Guid Partner = WorkflowApiFactory.AccountId("partner@ovo.test");
    private static readonly DateOnly Week = new(2026, 8, 10);
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin")
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role));
        return c;
    }
    private static async Task<(Guid Brand, Guid Period)> Seed(WorkflowApiFactory f)
    {
        var brand = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brand, Name = "Emek denemesi", Status = DealStatus.Active, Currency = "TRY" };
        var period = new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = 2026, Month = 8,
            Status = MonthlyPerformanceStatus.Locked, OvoFee = 100_000, OvoInternalCost = 30_000, OvoGrossProfit = 70_000 };
        db.AddRange(deal, period); await db.SaveChangesAsync(); return (brand, period.Id);
    }
    private static async Task<Guid> CreateTask(HttpClient admin, Guid brand, Guid assignee = default)
    {
        var id = Guid.NewGuid();
        var task = new WorkTaskRequest(id, brand, assignee == default ? Analyst : assignee, "Aylık veri hazırlığı",
            "Kaynakları topla", WorkPriority.Normal, WorkKind.General, null, null, null, new DateOnly(2026, 8, 20));
        (await admin.PostAsJsonAsync("/api/work-tasks", task)).EnsureSuccessStatusCode(); return id;
    }
    private static async Task<int> CostRevision(HttpClient admin, Guid period)
    {
        var data = await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{period}/costs");
        return data.GetProperty("revision").GetInt32();
    }

    [Fact]
    public async Task Actual_hours_are_logged_by_the_worker_and_shown_next_to_planned_hours()
    {
        await using var f = new WorkflowApiFactory(); var (brand, _) = await Seed(f);
        using var admin = Client(f); using var analyst = Client(f, "Analyst");
        var task = await CreateTask(admin, brand);
        (await admin.PutAsJsonAsync($"/api/work-tasks/{task}/hour-plan", new TaskHourPlanRequest(Week, 12, 0, 0, "Haftalık plan"))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries",
            new TimeEntryRequest(Guid.NewGuid(), new DateOnly(2026, 8, 11), 8, "Salı"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries",
            new TimeEntryRequest(Guid.NewGuid(), Week, 0, "Sıfır"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries",
            new TimeEntryRequest(Guid.NewGuid(), Week, 200, "Fazla"))).StatusCode);

        var entry = new TimeEntryRequest(Guid.NewGuid(), Week, 8, "Kaynak raporları hazırlandı");
        Assert.Equal(HttpStatusCode.Created, (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries", entry)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries", entry)).StatusCode);

        var read = await analyst.GetFromJsonAsync<JsonElement>($"/api/work-tasks/{task}/time-entries");
        var summary = read.GetProperty("summary");
        Assert.Equal(12m, summary.GetProperty("plannedHours").GetDecimal());
        Assert.Equal(8m, summary.GetProperty("actualHours").GetDecimal());
        Assert.Equal(4m, summary.GetProperty("remainingHours").GetDecimal());
        Assert.Equal(0m, summary.GetProperty("voidedHours").GetDecimal());
        Assert.True(summary.GetProperty("complete").GetBoolean());
        Assert.True(read.GetProperty("canLog").GetBoolean());
        Assert.Equal(Analyst, read.GetProperty("entries")[0].GetProperty("userId").GetGuid());
        var body = await analyst.GetStringAsync($"/api/work-tasks/{task}/time-entries");
        Assert.DoesNotContain("hourlyCost", body);

        var otherTask = await CreateTask(admin, brand, Partner);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/work-tasks/{otherTask}/time-entries",
            new TimeEntryRequest(Guid.NewGuid(), Week, 2, "Başkasının işi"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/work-tasks/{otherTask}/time-entries",
            new TimeEntryRequest(Guid.NewGuid(), Week, 2, "Yönetici girişi"))).StatusCode);
    }

    [Fact]
    public async Task Actual_hours_transfer_to_the_cost_ledger_once_and_voiding_is_guarded()
    {
        await using var f = new WorkflowApiFactory(); var (brand, period) = await Seed(f);
        using var admin = Client(f); using var analyst = Client(f, "Analyst");
        var task = await CreateTask(admin, brand);
        var entryId = Guid.NewGuid();
        (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries",
            new TimeEntryRequest(entryId, Week, 8, "Ağustos emeği"))).EnsureSuccessStatusCode();

        var transfer = new TimeToCostRequest(Guid.NewGuid(), entryId, 1500, new DateOnly(2026, 8, 20),
            "SAAT-1", "Ağustos gerçek emek aktarımı", true, 0);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/performance/{period}/costs/entries/from-time", transfer)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/from-time", transfer with { Confirmed = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/from-time", transfer with { Id = Guid.NewGuid(), IncurredOn = new DateOnly(2026, 8, 5) })).StatusCode);

        var first = await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/from-time", transfer);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var costId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entries")[0].GetProperty("id").GetGuid();
        Assert.Equal(12_000m, (await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{period}/costs"))
            .GetProperty("summary").GetProperty("teamCost").GetDecimal());

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/from-time",
            transfer with { Id = Guid.NewGuid(), Revision = await CostRevision(admin, period), Reference = "SAAT-2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries/{entryId}/void",
            new VoidPaymentRequest("İptal etmek istiyorum", 0))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/{costId}/void",
            new VoidPaymentRequest("Yanlış aktarım", await CostRevision(admin, period)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/from-time",
            transfer with { Id = Guid.NewGuid(), Revision = await CostRevision(admin, period), Reference = "SAAT-3" })).StatusCode);
        var active = await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{period}/costs");
        var activeId = active.GetProperty("entries").EnumerateArray()
            .Single(x => x.GetProperty("voidedAt").ValueKind == JsonValueKind.Null).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries/{entryId}/void",
            new VoidPaymentRequest("Yine de iptal", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries/{entryId}/void",
            new VoidPaymentRequest("Analist iptali", 0))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/performance/{period}/costs/entries/{activeId}/void",
            new VoidPaymentRequest("İkinci iptal", await CostRevision(admin, period)))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/work-tasks/{task}/time-entries/{entryId}/void",
            new VoidPaymentRequest("Yanlış girilmişti", 0))).StatusCode);

        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var summary = await db.ServiceCostEntries.Where(x => x.MonthlyPerformanceId == period).ToListAsync();
        Assert.Equal(2, summary.Count);
        Assert.All(summary, x => Assert.NotNull(x.SourceTimeEntryId));
        Assert.Equal(0, await db.ServiceCostEntries.CountAsync(x => x.MonthlyPerformanceId == period && x.VoidedAt == null));
        var entry = await db.TaskTimeEntries.SingleAsync(x => x.Id == entryId);
        Assert.NotNull(entry.VoidedAt);
    }

    [Fact]
    public async Task Time_entries_are_read_only_for_customers_and_costs_stay_private()
    {
        await using var f = new WorkflowApiFactory(); var (brand, _) = await Seed(f);
        using var admin = Client(f); using var analyst = Client(f, "Analyst");
        var task = await CreateTask(admin, brand);
        var read = await analyst.GetAsync($"/api/work-tasks/{task}/time-entries");
        read.EnsureSuccessStatusCode();
        var body = await read.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hourlyCost", body);
    }
}
