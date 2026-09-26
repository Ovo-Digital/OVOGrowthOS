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

public sealed class RenewalSummaryTests
{
    private static readonly Guid Analyst = WorkflowApiFactory.AccountId("analyst@ovo.test");
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin")
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role));
        return c;
    }
    private static async Task<Guid> Seed(WorkflowApiFactory f)
    {
        var brand = await f.SeedAsync(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brand, Name = "Yenileme denemesi", Status = DealStatus.Active, Currency = "TRY",
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 8, 31), ContractMonths = 12, MonthlyRetainer = 40_000, EstimatedMonthlyInternalCost = 12_000 };
        var period = new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = 2026, Month = 8,
            Status = MonthlyPerformanceStatus.Locked, NetRevenue = 250_000, OvoFee = 80_000, OvoInternalCost = 20_000, OvoGrossProfit = 60_000 };
        var target = new MonthlyTarget { BrandId = brand, Year = 2026, Month = 8, Currency = "TRY", NetRevenueGoal = 300_000, ContributionMarginGoal = 0.2m, OwnerId = Analyst };
        var collection = new CollectionAccount { MonthlyPerformanceId = period.Id, ReceivableAmount = 80_000, Currency = "TRY", DueOn = new DateOnly(2026, 9, 10) };
        db.AddRange(deal, period, target, collection);
        db.DealScopeItems.Add(new DealScopeItem { DealId = deal.Id, Title = "Performans pazarlaması", Description = "Aylık yönetim", CreatedBy = "admin@ovo.test" });
        await db.SaveChangesAsync();
        var task = new WorkTask { BrandId = brand, DealId = deal.Id, AssigneeId = Analyst, Title = "Yenileme hazırlığı",
            Description = "Özeti hazırla", Priority = WorkPriority.Normal, Kind = WorkKind.ContractRenewal, DueOn = new DateOnly(2026, 8, 25) };
        db.Add(task); await db.SaveChangesAsync();
        db.TaskHourPlans.Add(new TaskHourPlan { TaskId = task.Id, WeekStart = new DateOnly(2026, 8, 10), Hours = 10 });
        db.TaskTimeEntries.Add(new TaskTimeEntry { TaskId = task.Id, UserId = Analyst, WeekStart = new DateOnly(2026, 8, 10), Hours = 6, CreatedBy = "analyst@ovo.test" });
        db.ServiceCostEntries.Add(new ServiceCostEntry { MonthlyPerformanceId = period.Id, Kind = ServiceCostKind.TeamWork,
            Amount = 9_000m, Hours = 6, HourlyCost = 1_500m, IncurredOn = new DateOnly(2026, 8, 20), Reference = "SAAT-99", Description = "Gerçek emek", CreatedBy = "admin@ovo.test" });
        await db.SaveChangesAsync();
        return deal.Id;
    }

    [Fact]
    public async Task Renewal_summary_is_manager_only_read_only_and_never_returns_hourly_rates()
    {
        await using var f = new WorkflowApiFactory(); var deal = await Seed(f);
        using var analyst = Client(f, "Analyst"); using var admin = Client(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync($"/api/deals/{deal}/renewal-summary")).StatusCode);

        var before = await admin.GetAsync($"/api/deals/{deal}"); var beforeBody = await before.Content.ReadAsStringAsync();
        var response = await admin.GetAsync($"/api/deals/{deal}/renewal-summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("Yenileme denemesi", data.GetProperty("deal").GetProperty("name").GetString());
        Assert.Equal(1, data.GetProperty("scope").GetProperty("activeItems").GetInt32());
        Assert.Equal(1, data.GetProperty("effort").GetProperty("taskCount").GetInt32());
        Assert.Equal(10m, data.GetProperty("effort").GetProperty("plannedHours").GetDecimal());
        Assert.Equal(6m, data.GetProperty("effort").GetProperty("actualHours").GetDecimal());
        Assert.Equal(9_000m, data.GetProperty("costs").GetProperty("recorded").GetDecimal());
        Assert.Equal(6m, data.GetProperty("costs").GetProperty("hours").GetDecimal());
        Assert.Equal(80_000m, data.GetProperty("collections").GetProperty("receivable").GetDecimal());
        Assert.Equal(1, data.GetProperty("months").GetArrayLength());
        Assert.Equal(300_000m, data.GetProperty("months")[0].GetProperty("target").GetDecimal());
        Assert.Equal(250_000m, data.GetProperty("months")[0].GetProperty("netRevenue").GetDecimal());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("renewal").GetProperty("taskTitle").GetString()));
        var notes = data.GetProperty("notes").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        Assert.Contains(notes, x => x.Contains("otomatik ücret artışı"));
        Assert.DoesNotContain("hourlyCost", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(data.GetProperty("costs").TryGetProperty("hourlyCost", out _));

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Deals.AsNoTracking().SingleAsync(x => x.Id == deal);
            var auditCount = await db.AuditRecords.CountAsync();
            var after = await admin.GetAsync($"/api/deals/{deal}"); var afterBody = await after.Content.ReadAsStringAsync();
            Assert.Equal(beforeBody, afterBody);
            Assert.Equal(40_000m, stored.MonthlyRetainer);
            Assert.Equal(0, await db.AuditRecords.CountAsync() - auditCount);
        }
    }
}
