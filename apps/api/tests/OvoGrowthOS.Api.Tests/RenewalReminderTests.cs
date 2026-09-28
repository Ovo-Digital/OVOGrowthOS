using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class RenewalReminderTests
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

    private static async Task<Guid> SeedDeal(WorkflowApiFactory f, DateOnly end, DealStatus status = DealStatus.Active, string brandName = "Lale")
    {
        var dealId = Guid.NewGuid(); var brandId = Guid.NewGuid();
        await Db(f, async db =>
        {
            var brand = new Brand { Id = brandId, Name = brandName };
            var evaluation = new BrandEvaluation { BrandId = brandId, Brand = brand, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
            var deal = new Deal { Id = dealId, BrandId = brandId, Brand = brand, EvaluationId = evaluation.Id, Name = "Anlaşma", Status = status, StartDate = new DateOnly(2026, 1, 1), EndDate = end };
            db.AddRange(brand, evaluation, deal);
            await db.SaveChangesAsync();
        });
        return dealId;
    }

    private static async Task<JsonElement[]> Items(HttpClient c)
    {
        await c.PostAsync("/api/notifications/refresh", null);
        using var doc = JsonDocument.Parse(await c.GetStringAsync("/api/notifications"));
        return doc.RootElement.GetProperty("items").EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    [Fact]
    public async Task Active_deal_reminds_managers_between_30_and_7_days_and_is_idempotent()
    {
        await using var f = new WorkflowApiFactory();
        var end = TeamWork.Today(DateTimeOffset.UtcNow).AddDays(20);
        var deal = await SeedDeal(f, end);
        using var admin = await Client(f);

        var items = await Items(admin);
        var renewal = items.Single(x => x.GetProperty("title").GetString()!.Contains("gün kaldı"));
        Assert.Contains("20 gün kaldı: Lale", renewal.GetProperty("title").GetString());
        Assert.Equal($"/deals/{deal}", renewal.GetProperty("href").GetString());
        Assert.Equal(JsonValueKind.Null, renewal.GetProperty("emailStatus").ValueKind);

        await Items(admin);
        await Db(f, async db => Assert.Equal(1, await db.UserNotifications.CountAsync(x => x.UserId == WorkflowApiFactory.AccountId("admin@ovo.test") && x.Kind == NotificationKind.RenewalDue)));

        using var analyst = await Client(f, "analyst");
        Assert.DoesNotContain(await Items(analyst), x => x.GetProperty("title").GetString()!.Contains("gün kaldı"));
        using var partner = await Client(f, "partner");
        Assert.Contains(await Items(partner), x => x.GetProperty("title").GetString()!.Contains("gün kaldı"));
    }

    [Theory]
    [InlineData(4, "4 gün kaldı")]
    [InlineData(0, "bitiş günü")]
    public async Task Window_switches_to_the_final_week_and_hides_finished_deals(int daysOut, string expected)
    {
        await using var f = new WorkflowApiFactory();
        await SeedDeal(f, TeamWork.Today(DateTimeOffset.UtcNow).AddDays(daysOut));
        using var admin = await Client(f);
        var items = await Items(admin);
        Assert.Contains(items, x => x.GetProperty("title").GetString()!.Contains(expected));
    }

    [Fact]
    public async Task Far_away_finished_and_missing_end_dates_produce_no_reminder()
    {
        await using var f = new WorkflowApiFactory();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        await SeedDeal(f, today.AddDays(45));
        await SeedDeal(f, today.AddDays(3), DealStatus.Terminated, "Bitmiş");
        await SeedDeal(f, today.AddDays(3), DealStatus.Active, "Tarihsiz");
        await Db(f, async db =>
        {
            foreach (var row in await db.Deals.Where(x => x.Brand!.Name == "Tarihsiz").ToListAsync()) row.EndDate = null;
            await db.SaveChangesAsync();
        });
        using var admin = await Client(f);
        Assert.DoesNotContain(await Items(admin), x => x.GetProperty("title").GetString()!.Contains("gün kaldı"));
        await Db(f, async db => Assert.False(await db.UserNotifications.AnyAsync(x => x.Kind == NotificationKind.RenewalDue)));
    }
}
