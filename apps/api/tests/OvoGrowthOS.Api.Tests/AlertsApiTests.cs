using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class AlertsApiTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task Db(WorkflowApiFactory f, Func<AppDbContext, Task> action)
    {
        await using var scope = f.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<(Guid Brand, Guid Deal, Guid Evaluation)> SeedBrand(WorkflowApiFactory f, string name, decimal? targetMer = null)
    {
        var brandId = Guid.NewGuid(); var dealId = Guid.NewGuid(); var evaluationId = Guid.NewGuid();
        await Db(f, async db =>
        {
            var brand = new Brand { Id = brandId, Name = name, Currency = "TRY" };
            var evaluation = new BrandEvaluation
            {
                Id = evaluationId, BrandId = brandId, Brand = brand, Status = EvaluationStatus.Approved,
                Decision = DecisionStatus.Accept, CreatedBy = "test", RecommendedTargetMer = targetMer ?? 0m
            };
            var deal = new Deal
            {
                Id = dealId, BrandId = brandId, Brand = brand, EvaluationId = evaluationId,
                Name = "Anlaşma", Status = DealStatus.Active, StartDate = new DateOnly(2026, 1, 1), Currency = "TRY"
            };
            db.AddRange(brand, evaluation, deal);
            await db.SaveChangesAsync();
        });
        return (brandId, dealId, evaluationId);
    }

    private static async Task SeedClosedPeriod(WorkflowApiFactory f, Guid brandId, Guid dealId, int year, int month,
        decimal mer, decimal adSpend, decimal netRevenue, MonthlyPerformanceStatus status = MonthlyPerformanceStatus.Locked)
    {
        await Db(f, async db =>
        {
            db.MonthlyPerformances.Add(new MonthlyPerformance
            {
                Id = Guid.NewGuid(), BrandId = brandId, DealId = dealId, Year = year, Month = month,
                Status = status, NetRevenue = netRevenue, TotalAdSpend = adSpend, Mer = mer
            });
            await db.SaveChangesAsync();
        });
    }

    private static IEnumerable<JsonElement> Items(JsonDocument doc) =>
        doc.RootElement.GetProperty("items").EnumerateArray().ToList();

    [Fact]
    public async Task Alerts_board_reports_missing_close_and_deep_mer_breach()
    {
        await using var f = new WorkflowApiFactory();
        await SeedBrand(f, "Kayıpsız Marka"); // yalnız anlaşma, aylık kayıt yok
        var (weak, deal, _) = await SeedBrand(f, "Zayıf MER Markası", targetMer: 2m);
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        await SeedClosedPeriod(f, weak, deal, today.Year, today.Month, mer: 1.4m, adSpend: 50000m, netRevenue: 70000m);
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync("/api/alerts"));
        var summary = doc.RootElement.GetProperty("summary");
        Assert.True(summary.GetProperty("total").GetInt32() >= 2);
        Assert.Equal(1, summary.GetProperty("critical").GetInt32());
        Assert.Equal(1, summary.GetProperty("warning").GetInt32());

        var items = Items(doc).ToList();
        var missing = items.Single(x => x.GetProperty("code").GetString() == "missing_close");
        Assert.Contains("Kayıpsız Marka", missing.GetProperty("detail").GetString());
        Assert.Equal("/data-quality", missing.GetProperty("link").GetString());

        var mer = items.Single(x => x.GetProperty("code").GetString() == "mer_below_breakeven");
        Assert.Equal("critical", mer.GetProperty("severity").GetString());
        Assert.Contains("1,40x", mer.GetProperty("detail").GetString());
        Assert.Contains("2,00x", mer.GetProperty("detail").GetString());
        Assert.NotEmpty(doc.RootElement.GetProperty("notes").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Alerts_board_is_read_only_and_requires_access()
    {
        await using var f = new WorkflowApiFactory();
        using var anon = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/alerts")).StatusCode);
        using var admin = await Client(f);
        var before = await admin.GetStringAsync("/api/alerts");
        var second = await admin.GetStringAsync("/api/alerts");
        Assert.Equal(before, second);
    }

    [Fact]
    public async Task Ad_efficiency_card_reads_closed_periods_only()
    {
        await using var f = new WorkflowApiFactory();
        var (brand, deal, _) = await SeedBrand(f, "Verimli Marka", targetMer: 2m);
        await SeedClosedPeriod(f, brand, deal, 2026, 9, mer: 2.25m, adSpend: 20000m, netRevenue: 45000m);
        await SeedClosedPeriod(f, brand, deal, 2026, 8, mer: 1.5m, adSpend: 20000m, netRevenue: 30000m);
        await SeedClosedPeriod(f, brand, deal, 2026, 7, mer: 0.5m, adSpend: 1000m, netRevenue: 500m,
            status: MonthlyPerformanceStatus.Draft);
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync($"/api/brands/{brand}/ad-efficiency"));
        var root = doc.RootElement;
        Assert.Equal("strong", root.GetProperty("bandCode").GetString());
        Assert.Equal("Güçlü", root.GetProperty("bandLabel").GetString());
        Assert.Equal(2.25m, root.GetProperty("latestMer").GetDecimal());
        Assert.Equal(2m, root.GetProperty("breakEvenMer").GetDecimal());
        Assert.Equal("09/2026", root.GetProperty("periodLabel").GetString());
        var trend = root.GetProperty("trend").EnumerateArray().ToList();
        Assert.Equal(2, trend.Count); // taslak dönem dahil edilmez
        Assert.Contains("kilitlenmiş", root.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Ad_efficiency_card_is_unknown_without_target_and_missing_404()
    {
        await using var f = new WorkflowApiFactory();
        var (brand, deal, _) = await SeedBrand(f, "Hedefsiz Marka");
        await SeedClosedPeriod(f, brand, deal, 2026, 9, mer: 1.1m, adSpend: 10000m, netRevenue: 11000m);
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync($"/api/brands/{brand}/ad-efficiency"));
        Assert.Equal("unknown", doc.RootElement.GetProperty("bandCode").GetString());
        Assert.Contains("değerlendirme analizi gerekli", doc.RootElement.GetProperty("bandDetail").GetString());

        var missing = await admin.GetAsync($"/api/brands/{Guid.NewGuid()}/ad-efficiency");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Quality_queue_opens_one_follow_up_task_per_missing_brand_and_runs_once()
    {
        await using var f = new WorkflowApiFactory();
        var (missing, _, _) = await SeedBrand(f, "Eksik Marka");
        var (complete, completeDeal, _) = await SeedBrand(f, "Kaydı Dolu Marka");
        await Db(f, async db =>
        {
            db.MonthlyPerformances.Add(new MonthlyPerformance
            {
                Id = Guid.NewGuid(), BrandId = complete, DealId = completeDeal, Year = 2026, Month = 10,
                Status = MonthlyPerformanceStatus.Locked, NetRevenue = 10000m, TotalAdSpend = 1000m, Mer = 10m
            });
            await db.SaveChangesAsync();
        });

        await using var scope = f.Services.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<QualityAutoTaskQueue>();
        var earlyNovember = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(3));
        var lateNovember = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(0, await queue.RunDue(lateNovember)); // ayın ilk günleri dışında çalışmaz
        Assert.Equal(1, await queue.RunDue(earlyNovember));

        await Db(f, async db =>
        {
            var task = await db.WorkTasks.SingleAsync();
            Assert.Equal(missing, task.BrandId);
            Assert.Equal(WorkKind.MonthlyClose, task.Kind);
            Assert.Equal(WorkPriority.High, task.Priority);
            Assert.Equal(new DateOnly(2026, 11, 5), task.DueOn);
            Assert.Equal(2026, task.Year);
            Assert.Equal(10, task.Month);
            Assert.Equal("sistem (otomatik)", task.CreatedBy);
            Assert.Equal(WorkflowApiFactory.AccountId("admin@ovo.test"), task.AssigneeId);
            Assert.Contains("Ekim 2026", task.Description);
            Assert.True(await db.AuditRecords.AnyAsync(x => x.Action == "QualityAutoTaskSummary" && x.EntityId == "2026-10"));
        });

        Assert.Equal(0, await queue.RunDue(earlyNovember)); // aynı dönem ikinci kez işlenmez
    }
}
