using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class DataQualityTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f, string role = "admin")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role + "@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }
    private static async Task Db(WorkflowApiFactory f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }

    private static async Task<(Guid Brand, Guid Deal)> SeedBrand(WorkflowApiFactory f, string name, DateOnly start)
    {
        var brandId = Guid.NewGuid(); var dealId = Guid.NewGuid();
        await Db(f, async db =>
        {
            var brand = new Brand { Id = brandId, Name = name };
            var evaluation = new BrandEvaluation { BrandId = brandId, Brand = brand, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
            var deal = new Deal { Id = dealId, BrandId = brandId, Brand = brand, EvaluationId = evaluation.Id, Name = "Anlaşma", Status = DealStatus.Active, StartDate = start };
            db.AddRange(brand, evaluation, deal);
            await db.SaveChangesAsync();
        });
        return (brandId, dealId);
    }

    private static async Task<Guid> SeedPeriod(WorkflowApiFactory f, Guid brandId, Guid dealId, int year, int month, Action<MonthlyPerformance> fill)
    {
        var id = Guid.NewGuid();
        await Db(f, async db =>
        {
            var row = new MonthlyPerformance { Id = id, BrandId = brandId, DealId = dealId, Year = year, Month = month };
            fill(row);
            row.NetRevenue = row.GrossSales - row.Vat - row.Refunds - row.Cancellations - row.Chargebacks;
            db.MonthlyPerformances.Add(row);
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = "admin@ovo.test", Action = "MonthlyPerformanceCreated", EntityType = "MonthlyPerformance",
                EntityId = id.ToString(), NewValueJson = "{}"
            });
            await db.SaveChangesAsync();
        });
        return id;
    }

    private static JsonElement Item(JsonDocument report, Guid brandId) =>
        report.RootElement.GetProperty("brands").EnumerateArray().Single(x => x.GetProperty("brandId").GetGuid() == brandId);

    [Fact]
    public async Task Board_reports_missing_and_review_states_and_never_changes_the_period()
    {
        await using var f = new WorkflowApiFactory();
        var (missing, _) = await SeedBrand(f, "Eksik Marka", new DateOnly(2026, 1, 1));
        var (watched, deal) = await SeedBrand(f, "İnceleme Markası", new DateOnly(2026, 1, 1));
        await SeedPeriod(f, watched, deal, 2026, 9, p => { p.GrossSales = 10_000m; p.Vat = 1_000m; p.Refunds = 4_000m; p.Cogs = 2_000m; p.Orders = 30; });
        using var admin = await Client(f);

        using var report = JsonDocument.Parse(await admin.GetStringAsync("/api/data-quality?year=2026&month=9"));
        var summary = report.RootElement.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("total").GetInt32());
        Assert.Equal(1, summary.GetProperty("missing").GetInt32());
        Assert.Equal(1, summary.GetProperty("attention").GetInt32());
        Assert.Equal(0, summary.GetProperty("ready").GetInt32());
        Assert.Equal("Eylül 2026", report.RootElement.GetProperty("label").GetString());

        var missingItem = Item(report, missing);
        Assert.Equal("missing", missingItem.GetProperty("readiness").GetString());
        Assert.All(missingItem.GetProperty("sources").EnumerateArray(), s => Assert.Equal("missing", s.GetProperty("state").GetString()));
        Assert.Contains("sıfır sayılmadı", missingItem.GetRawText());

        var watchedItem = Item(report, watched);
        Assert.Equal("attention", watchedItem.GetProperty("readiness").GetString());
        Assert.Equal("manual", watchedItem.GetProperty("origin").GetString());
        Assert.Equal("Admin", watchedItem.GetProperty("responsible").GetString());
        Assert.Contains("warning", watchedItem.GetProperty("alerts").GetRawText());

        await Db(f, async db =>
        {
            var row = await db.MonthlyPerformances.SingleAsync(x => x.BrandId == watched);
            Assert.Equal(10_000m, row.GrossSales);
            Assert.Equal(MonthlyPerformanceStatus.Draft, row.Status);
            Assert.Empty(row.ReviewedBy);
            Assert.Empty(await db.WorkTasks.ToListAsync());
        });
    }

    [Fact]
    public async Task Tracking_task_is_created_once_for_a_missing_source_and_cannot_be_duplicated()
    {
        await using var f = new WorkflowApiFactory();
        var (brand, deal) = await SeedBrand(f, "Eksik Marka", new DateOnly(2026, 1, 1));
        using var admin = await Client(f);

        var created = await admin.PostAsJsonAsync("/api/data-quality/track-task", new QualityTaskRequest(brand, 2026, 9, null, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await Db(f, async db =>
        {
            var task = await db.WorkTasks.SingleAsync(x => x.BrandId == brand);
            Assert.Equal(WorkKind.MonthlyClose, task.Kind);
            Assert.Equal(deal, task.DealId);
            Assert.Equal(2026, task.Year);
            Assert.Equal(9, task.Month);
            Assert.Equal(new DateOnly(2026, 10, 5), task.DueOn);
            Assert.Equal(WorkPriority.High, task.Priority);
            Assert.Contains("Eylül 2026 dönemi veri kalitesi bulguları", task.Description);
            Assert.Contains("Bu görev yalnız takip içindir", task.Description);
            Assert.Contains("/data-quality?year=2026&month=9", task.Description);
            Assert.True(await db.AuditRecords.AnyAsync(x => x.Action == "WorkTaskCreated" && x.EntityId == task.Id.ToString()));
        });

        var second = await admin.PostAsJsonAsync("/api/data-quality/track-task", new QualityTaskRequest(brand, 2026, 9, null, null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var report = JsonDocument.Parse(await admin.GetStringAsync("/api/data-quality?year=2026&month=9"));
        Assert.NotEqual(JsonValueKind.Null, Item(report, brand).GetProperty("taskId").ValueKind);
    }

    [Fact]
    public async Task A_brand_without_findings_and_a_bad_period_are_rejected()
    {
        await using var f = new WorkflowApiFactory();
        var (brand, deal) = await SeedBrand(f, "Hazır Marka", new DateOnly(2026, 1, 1));
        await SeedPeriod(f, brand, deal, 2026, 9, p =>
        {
            p.GrossSales = 10_000m; p.Vat = 2_000m; p.Refunds = 100m; p.TotalAdSpend = 500m; p.Cogs = 3_000m; p.Orders = 12;
        });
        using var admin = await Client(f);

        using var report = JsonDocument.Parse(await admin.GetStringAsync("/api/data-quality?year=2026&month=9"));
        Assert.Equal("ready", Item(report, brand).GetProperty("readiness").GetString());

        var rejected = await admin.PostAsJsonAsync("/api/data-quality/track-task", new QualityTaskRequest(brand, 2026, 9, null, null));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("eksik kaynak bulunmuyor", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/data-quality?year=1999&month=13")).StatusCode);
        await Db(f, async db => Assert.Empty(await db.WorkTasks.ToListAsync()));
    }

    [Fact]
    public async Task Analyst_reads_the_board_but_cannot_create_a_tracking_task()
    {
        await using var f = new WorkflowApiFactory();
        var (brand, _) = await SeedBrand(f, "Eksik Marka", new DateOnly(2026, 1, 1));
        using var analyst = await Client(f, "analyst");
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync("/api/data-quality?year=2026&month=9")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync("/api/data-quality/track-task", new QualityTaskRequest(brand, 2026, 9, null, null))).StatusCode);
        await Db(f, async db => Assert.Empty(await db.WorkTasks.ToListAsync()));
    }
}
