using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PortfolioCurrencyTotalsTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task Seed(WorkflowApiFactory f, decimal tryNet, decimal tryFee, decimal usdNet, decimal usdFee)
    {
        await f.SeedAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tryBrand = new Brand { Name = "Lale" };
        var usdBrand = new Brand { Name = "Gül" };
        var ev1 = new BrandEvaluation { BrandId = tryBrand.Id, Brand = tryBrand, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
        var ev2 = new BrandEvaluation { BrandId = usdBrand.Id, Brand = usdBrand, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
        var deal1 = new Deal { BrandId = tryBrand.Id, Brand = tryBrand, EvaluationId = ev1.Id, Name = "Anlaşma TRY", Status = DealStatus.Active, Currency = "TRY" };
        var deal2 = new Deal { BrandId = usdBrand.Id, Brand = usdBrand, EvaluationId = ev2.Id, Name = "Anlaşma USD", Status = DealStatus.Active, Currency = "USD" };
        db.AddRange(tryBrand, usdBrand, ev1, ev2, deal1, deal2);
        db.MonthlyPerformances.AddRange(
            new MonthlyPerformance { BrandId = tryBrand.Id, DealId = deal1.Id, Year = 2026, Month = 8, NetRevenue = tryNet, OvoFee = tryFee, Status = MonthlyPerformanceStatus.Paid },
            new MonthlyPerformance { BrandId = usdBrand.Id, DealId = deal2.Id, Year = 2026, Month = 8, NetRevenue = usdNet, OvoFee = usdFee, Status = MonthlyPerformanceStatus.Paid });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Subtotals_are_grouped_by_currency_and_manual_rates_convert_them()
    {
        await using var f = new WorkflowApiFactory();
        await Seed(f, 1000m, 100m, 50m, 10m);
        using var c = await Client(f);

        var json = await c.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=8&scope=All&currency=TRY");
        var totals = json.GetProperty("currencyTotals").EnumerateArray()
            .ToDictionary(x => x.GetProperty("currency").GetString()!, x => x);
        Assert.Equal(1000m, totals["TRY"].GetProperty("netRevenue").GetDecimal());
        Assert.Equal(100m, totals["TRY"].GetProperty("ovoFee").GetDecimal());
        Assert.Equal(50m, totals["USD"].GetProperty("netRevenue").GetDecimal());
        Assert.Equal(10m, totals["USD"].GetProperty("ovoFee").GetDecimal());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("conversion").ValueKind);

        var conv = await c.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=8&scope=All&currency=TRY&convertTo=TRY&manualRates=USD%3D40");
        var conversion = conv.GetProperty("conversion");
        Assert.Equal("TRY", conversion.GetProperty("target").GetString());
        Assert.Equal(40m, conversion.GetProperty("rates").GetProperty("USD").GetDecimal());
        Assert.Equal(0, conversion.GetProperty("missing").GetArrayLength());
        Assert.Equal(3000m, conversion.GetProperty("netRevenue").GetDecimal());
        Assert.Equal(500m, conversion.GetProperty("ovoFee").GetDecimal());
    }

    [Fact]
    public async Task Missing_and_invalid_rates_are_reported_without_a_grand_total()
    {
        await using var f = new WorkflowApiFactory();
        await Seed(f, 1000m, 100m, 50m, 10m);
        using var c = await Client(f);

        var missing = await c.GetFromJsonAsync<JsonElement>("/api/dashboard?year=2026&month=8&scope=All&currency=TRY&convertTo=TRY");
        var conversion = missing.GetProperty("conversion");
        Assert.Single(conversion.GetProperty("missing").EnumerateArray(), x => x.GetString() == "USD");
        Assert.Equal(JsonValueKind.Null, conversion.GetProperty("netRevenue").ValueKind);

        var invalid = await c.GetAsync("/api/dashboard?year=2026&month=8&scope=All&currency=TRY&convertTo=TRY&manualRates=USD%3Dabc");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
