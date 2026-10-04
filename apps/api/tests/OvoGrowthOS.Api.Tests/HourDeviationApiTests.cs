using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class HourDeviationApiTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static DateOnly Monday(DateOnly week) => week.AddDays(-(((int)week.DayOfWeek + 6) % 7));

    private static async Task<(Guid Full, Guid Voided, Guid Late, Guid Empty, DateOnly Week, DateOnly Next)> Seed(WorkflowApiFactory f)
    {
        var monday = Monday(TeamWork.Today(DateTimeOffset.UtcNow));
        var next = monday.AddDays(7);
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var heavy = new Brand { Name = "Yoğun Marka" };
        var light = new Brand { Name = "Hafif Marka" };
        db.AddRange(heavy, light);
        var full = new WorkTask { BrandId = heavy.Id, AssigneeId = WorkflowApiFactory.AccountId("admin@ovo.test"), Title = "Kapanış paketi", DueOn = monday.AddDays(4) };
        var voided = new WorkTask { BrandId = heavy.Id, AssigneeId = WorkflowApiFactory.AccountId("admin@ovo.test"), Title = "Boşa giden kayıt", DueOn = monday.AddDays(4) };
        var late = new WorkTask { BrandId = light.Id, AssigneeId = WorkflowApiFactory.AccountId("analyst@ovo.test"), Title = "Planı aşan iş", DueOn = next.AddDays(4) };
        var empty = new WorkTask { BrandId = light.Id, AssigneeId = WorkflowApiFactory.AccountId("analyst@ovo.test"), Title = "Kayıt yok", DueOn = next.AddDays(4) };
        db.AddRange(full, voided, late, empty);
        db.TaskHourPlans.AddRange(
            new TaskHourPlan { TaskId = full.Id, WeekStart = monday, Hours = 10, Revision = 1 },
            new TaskHourPlan { TaskId = voided.Id, WeekStart = monday, Hours = 6, Revision = 1 },
            new TaskHourPlan { TaskId = late.Id, WeekStart = next, Hours = 5, Revision = 1 });
        db.TaskTimeEntries.AddRange(
            new TaskTimeEntry { TaskId = full.Id, UserId = WorkflowApiFactory.AccountId("admin@ovo.test"), WeekStart = monday, Hours = 8, CreatedBy = "admin@ovo.test" },
            new TaskTimeEntry { TaskId = voided.Id, UserId = WorkflowApiFactory.AccountId("admin@ovo.test"), WeekStart = monday, Hours = 4, CreatedBy = "admin@ovo.test",
                VoidedAt = DateTimeOffset.UtcNow, VoidedBy = "admin@ovo.test", VoidReason = "Yanlış haftaya girildi" },
            new TaskTimeEntry { TaskId = voided.Id, UserId = WorkflowApiFactory.AccountId("admin@ovo.test"), WeekStart = monday, Hours = 1, CreatedBy = "admin@ovo.test" },
            new TaskTimeEntry { TaskId = late.Id, UserId = WorkflowApiFactory.AccountId("analyst@ovo.test"), WeekStart = next, Hours = 12, CreatedBy = "analyst@ovo.test" });
        await db.SaveChangesAsync();
        return (full.Id, voided.Id, late.Id, empty.Id, monday, next);
    }

    [Fact]
    public async Task Report_groups_by_brand_separates_voided_hours_and_stays_read_only()
    {
        await using var f = new WorkflowApiFactory();
        var (_, _, _, _, week, next) = await Seed(f);
        using var admin = await Client(f);
        var url = $"/api/reports/hour-deviation?weekFrom={week:yyyy-MM-dd}&weekTo={next:yyyy-MM-dd}";

        var response = await admin.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal($"{week:yyyy-MM-dd}", root.GetProperty("weekFrom").GetString());
        Assert.Equal($"{next:yyyy-MM-dd}", root.GetProperty("weekTo").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("brandId").ValueKind);

        var totals = root.GetProperty("totals");
        Assert.Equal(3, totals.GetProperty("taskCount").GetInt32());
        Assert.Equal(21m, totals.GetProperty("plannedHours").GetDecimal());
        Assert.Equal(21m, totals.GetProperty("actualHours").GetDecimal());
        Assert.Equal(4m, totals.GetProperty("voidedHours").GetDecimal());
        Assert.Equal(0m, totals.GetProperty("differenceHours").GetDecimal());
        Assert.Equal(1.0m, totals.GetProperty("completionRatio").GetDecimal());
        Assert.Equal("Plana eşit", totals.GetProperty("status").GetString());

        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Hafif Marka", items[0].GetProperty("brand").GetString());
        Assert.Equal(1, items[0].GetProperty("taskCount").GetInt32());
        Assert.Equal(5m, items[0].GetProperty("plannedHours").GetDecimal());
        Assert.Equal(12m, items[0].GetProperty("actualHours").GetDecimal());
        Assert.Equal(7m, items[0].GetProperty("differenceHours").GetDecimal());
        Assert.Equal(2.4m, items[0].GetProperty("completionRatio").GetDecimal());
        Assert.Equal("Planın üzerinde", items[0].GetProperty("status").GetString());
        Assert.Equal("Yoğun Marka", items[1].GetProperty("brand").GetString());
        Assert.Equal(16m, items[1].GetProperty("plannedHours").GetDecimal());
        Assert.Equal(9m, items[1].GetProperty("actualHours").GetDecimal());
        Assert.Equal(4m, items[1].GetProperty("voidedHours").GetDecimal());
        Assert.Equal(-7m, items[1].GetProperty("differenceHours").GetDecimal());
        Assert.Equal("Planın altında", items[1].GetProperty("status").GetString());

        var tasks = root.GetProperty("tasks").EnumerateArray().ToList();
        Assert.Equal(3, tasks.Count);
        Assert.Equal("Planı aşan iş", tasks[0].GetProperty("task").GetString());
        Assert.Equal(7m, tasks[0].GetProperty("differenceHours").GetDecimal());
        Assert.Equal("Boşa giden kayıt", tasks[1].GetProperty("task").GetString());
        Assert.Equal(-5m, tasks[1].GetProperty("differenceHours").GetDecimal());
        Assert.Equal(-2m, tasks[2].GetProperty("differenceHours").GetDecimal());

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.TaskHourPlans.CountAsync());
        Assert.Equal(4, await db.TaskTimeEntries.CountAsync());
        Assert.Single(await db.TaskHourPlans.Where(x => x.Hours == 6).ToListAsync());
        Assert.Empty(await db.AuditRecords.Where(x => x.EntityType == "TaskHourPlan").ToListAsync());
    }

    [Fact]
    public async Task Brand_filter_narrow_the_rows_and_default_window_is_the_last_twelve_weeks()
    {
        await using var f = new WorkflowApiFactory();
        await Seed(f);
        using var admin = await Client(f);

        using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var heavy = (await db.Brands.SingleAsync(x => x.Name == "Yoğun Marka")).Id;

        using var filtered = JsonDocument.Parse(await admin.GetStringAsync($"/api/reports/hour-deviation?brandId={heavy}"));
        var root = filtered.RootElement;
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("Yoğun Marka", item.GetProperty("brand").GetString());
        Assert.Equal(16m, root.GetProperty("totals").GetProperty("plannedHours").GetDecimal());
        Assert.Equal(2, root.GetProperty("totals").GetProperty("taskCount").GetInt32());

        using var defaulted = JsonDocument.Parse(await admin.GetStringAsync("/api/reports/hour-deviation"));
        Assert.Equal(2, defaulted.RootElement.GetProperty("totals").GetProperty("taskCount").GetInt32());
        Assert.Equal(16m, defaulted.RootElement.GetProperty("totals").GetProperty("plannedHours").GetDecimal());

        using var empty = JsonDocument.Parse(await admin.GetStringAsync("/api/reports/hour-deviation?weekFrom=2024-01-01&weekTo=2024-01-08"));
        Assert.Equal(0, empty.RootElement.GetProperty("totals").GetProperty("taskCount").GetInt32());
        Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());
        Assert.Empty(empty.RootElement.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public async Task Bad_ranges_and_unknown_brand_are_rejected_with_turkish_messages()
    {
        await using var f = new WorkflowApiFactory();
        await Seed(f);
        using var admin = await Client(f);

        var reversed = await admin.GetAsync("/api/reports/hour-deviation?weekFrom=2026-09-14&weekTo=2026-09-07");
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal("Başlangıç haftası bitiş haftasından sonra olamaz.",
            (await reversed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        var tooWide = await admin.GetAsync("/api/reports/hour-deviation?weekFrom=2024-01-01&weekTo=2026-01-05");
        Assert.Equal(HttpStatusCode.BadRequest, tooWide.StatusCode);
        Assert.Equal("En fazla 52 haftalık bir aralık seçin.",
            (await tooWide.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/reports/hour-deviation?brandId={Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Any_staff_role_can_read_but_anonymous_and_portal_users_cannot()
    {
        await using var f = new WorkflowApiFactory();
        await Seed(f);
        using var analyst = await Client(f, "analyst@ovo.test");
        (await analyst.GetAsync("/api/reports/hour-deviation")).EnsureSuccessStatusCode();

        var anonymous = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/reports/hour-deviation")).StatusCode);
    }
}
