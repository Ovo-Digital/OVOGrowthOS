using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortfolioStressApiTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f, string email = "admin@ovo.test")
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task Seed(WorkflowApiFactory f)
    {
        await f.SeedAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var big = new Brand { Name = "Büyük Marka" };
        var small = new Brand { Name = "Küçük Marka" };
        var usd = new Brand { Name = "Dolar Markası" };
        var bigDeal = new Deal { BrandId = big.Id, Brand = big, Name = "Büyüme Anlaşması", Status = DealStatus.Active, Currency = "TRY", DealType = DealType.FlatRevenueShare, RevenueShareRate = .10m };
        var smallDeal = new Deal { BrandId = small.Id, Brand = small, Name = "Küçük Anlaşma", Status = DealStatus.Active, Currency = "TRY", DealType = DealType.FixedRetainer, MonthlyRetainer = 10_000 };
        var usdDeal = new Deal { BrandId = usd.Id, Brand = usd, Name = "Dolar Anlaşması", Status = DealStatus.Active, Currency = "USD", DealType = DealType.FlatRevenueShare, RevenueShareRate = .10m };
        db.AddRange(big, small, usd, bigDeal, smallDeal, usdDeal);
        db.MonthlyPerformances.AddRange(
            new MonthlyPerformance { BrandId = big.Id, DealId = bigDeal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Paid,
                NetRevenue = 300_000, CommissionableRevenue = 300_000, ContributionBeforeOvo = 100_000, OvoFee = 30_000, OvoGrossProfit = 20_000 },
            new MonthlyPerformance { BrandId = small.Id, DealId = smallDeal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Paid,
                NetRevenue = 100_000, CommissionableRevenue = 100_000, ContributionBeforeOvo = 50_000, OvoFee = 10_000, OvoGrossProfit = 8_000 },
            new MonthlyPerformance { BrandId = usd.Id, DealId = usdDeal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Paid,
                NetRevenue = 900_000, CommissionableRevenue = 900_000, ContributionBeforeOvo = 300_000, OvoFee = 90_000, OvoGrossProfit = 60_000 },
            new MonthlyPerformance { BrandId = big.Id, DealId = bigDeal.Id, Year = 2026, Month = 9, Status = MonthlyPerformanceStatus.Draft,
                NetRevenue = 400_000, CommissionableRevenue = 400_000, ContributionBeforeOvo = 120_000, OvoFee = 40_000, OvoGrossProfit = 30_000 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Stress_hits_the_largest_brand_of_the_selected_currency_and_period_without_writing()
    {
        await using var f = new WorkflowApiFactory(); await Seed(f);
        using var c = await Client(f);
        var before = await (await c.GetAsync("/api/dashboard?year=2026&month=8&scope=All&currency=TRY")).Content.ReadAsStringAsync();

        var response = await c.GetAsync("/api/dashboard/stress?year=2026&month=8&scope=All&currency=TRY&shockRate=-0.2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var stress = data.GetProperty("stress");

        Assert.Equal("Büyük Marka", stress.GetProperty("brandName").GetString());
        Assert.Equal(2, stress.GetProperty("recordCount").GetInt32());
        Assert.Equal(-60_000m, stress.GetProperty("revenueDelta").GetDecimal());
        Assert.Equal(40_000m, stress.GetProperty("baseFee").GetDecimal());
        Assert.Equal(-6_000m, stress.GetProperty("feeDelta").GetDecimal());
        Assert.Equal(34_000m, stress.GetProperty("simFee").GetDecimal());
        Assert.Equal(22_000m, stress.GetProperty("simProfit").GetDecimal());
        Assert.Equal(240_000m, stress.GetProperty("brandSimRevenue").GetDecimal());
        Assert.Equal(-0.2m, stress.GetProperty("shockRate").GetDecimal());
        var notes = stress.GetProperty("notes").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        Assert.Contains(notes, x => x.Contains("kaydetmez"));

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditCount = await db.AuditRecords.CountAsync();
            var after = await (await c.GetAsync("/api/dashboard?year=2026&month=8&scope=All&currency=TRY")).Content.ReadAsStringAsync();
            Assert.Equal(before, after);
            Assert.Equal(0, await db.AuditRecords.CountAsync() - auditCount);
            Assert.Equal(170_000m, await db.MonthlyPerformances.AsNoTracking().SumAsync(x => x.OvoFee));
        }
    }

    [Fact]
    public async Task Draft_periods_and_other_currencies_stay_out_of_the_shock()
    {
        await using var f = new WorkflowApiFactory(); await Seed(f);
        using var c = await Client(f);

        var draft = JsonSerializer.Deserialize<JsonElement>(
            await (await c.GetAsync("/api/dashboard/stress?year=2026&month=9&scope=All&currency=TRY&shockRate=-0.2")).Content.ReadAsStringAsync());
        Assert.Equal("Büyük Marka", draft.GetProperty("stress").GetProperty("brandName").GetString());
        Assert.Equal(-80_000m, draft.GetProperty("stress").GetProperty("revenueDelta").GetDecimal());

        var usd = JsonSerializer.Deserialize<JsonElement>(
            await (await c.GetAsync("/api/dashboard/stress?year=2026&month=8&scope=All&currency=USD&shockRate=-0.2")).Content.ReadAsStringAsync());
        Assert.Equal("Dolar Markası", usd.GetProperty("stress").GetProperty("brandName").GetString());
        Assert.Equal(1, usd.GetProperty("stress").GetProperty("recordCount").GetInt32());
    }

    [Fact]
    public async Task Invalid_shocks_periods_and_roles_are_answered_with_turkish_messages()
    {
        await using var f = new WorkflowApiFactory(); await Seed(f);
        using var admin = await Client(f);

        var shock = await admin.GetAsync("/api/dashboard/stress?year=2026&month=8&scope=All&currency=TRY&shockRate=1");
        Assert.Equal(HttpStatusCode.BadRequest, shock.StatusCode);
        Assert.Contains("%90", JsonSerializer.Deserialize<JsonElement>(await shock.Content.ReadAsStringAsync()).GetProperty("error").GetString());

        var period = await admin.GetAsync("/api/dashboard/stress?year=2026&month=13&scope=All&currency=TRY&shockRate=-0.1");
        Assert.Equal(HttpStatusCode.BadRequest, period.StatusCode);

        var currency = await admin.GetAsync("/api/dashboard/stress?year=2026&month=8&scope=All&currency=TL&shockRate=-0.1");
        Assert.Equal(HttpStatusCode.BadRequest, currency.StatusCode);

        var brand = await admin.GetAsync($"/api/dashboard/stress?year=2026&month=8&scope=All&currency=TRY&shockRate=-0.1&brandId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, brand.StatusCode);

        using var analyst = await Client(f, "analyst@ovo.test");
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync("/api/dashboard/stress?year=2026&month=8&scope=All&currency=TRY&shockRate=-0.1")).StatusCode);
    }
}
