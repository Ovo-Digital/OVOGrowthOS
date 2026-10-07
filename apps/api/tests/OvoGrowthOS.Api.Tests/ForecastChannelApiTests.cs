using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OvoGrowthOS.Api.Tests;

public sealed class ForecastChannelApiTests
{
    private static object DraftPayload(Guid brandId, object? revenueChannels) => new
    {
        brandId, currentStep = 6, status = "InProgress", averageMonthlyRevenue = 0m,
        revenueConfidence = "Verified", grossMarginRate = .55m, grossMarginConfidence = "Verified",
        cogsRate = .45m, cogsConfidence = "Verified", averageOrderValue = 2_000m, aovConfidence = "ProvidedByBrand",
        returnRate = .08m, returnRateConfidence = "Verified", currentAdSpend = 160_000m, adSpendConfidence = "Verified",
        currentCac = 400m, cacConfidence = "Estimated", averageCustomerLtv = 4_000m, ltvConfidence = "Estimated",
        stockCoverageDays = 75, stockCoverageConfidence = "ProvidedByBrand", monthlyOrders = 500,
        monthlySessions = 35_000, newCustomers = 400, returningCustomers = 100, variableCostRate = .08m,
        productMarketFit = 5, growthPotential = 4, operationalReadiness = 4, creativeCapability = 4,
        founderCooperation = 5, dataMaturity = 4, internalMonthlyCost = 30_000m, setupInvestment = 250_000m,
        revenueChannels,
    };

    private static object ScenarioPayload(object? revenueChannels) => new
    {
        name = "Kanallı", monthlyRevenue = 0m, grossMarginRate = .6m, adSpend = 150_000m, returnRate = .05m,
        averageOrderValue = 2_000m, newCustomers = 400, variableCostRate = .08m, ovoInternalMonthlyCost = 25_000m,
        minimumMonthlyFee = 40_000m, commissionModel = "FlatRevenueShare", revenueShareRate = .05m, monthlyRetainer = 0m,
        baselineRevenue = 0m, incrementalRate = .1m, profitShareRate = .1m, commissionTiers = Array.Empty<object>(),
        targetBrandContributionMargin = .15m, setupInvestment = 250_000m, contractMonths = 24,
        revenueChannels,
    };

    private static async Task<(Guid Web, Guid Trendyol)> SeedChannels(HttpClient admin, Guid brand)
    {
        async Task<Guid> Add(string name)
        {
            var response = await admin.PostAsJsonAsync($"/api/brands/{brand}/sales-channels", new { name });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        return (await Add("Web sitesi"), await Add("Trendyol"));
    }

    [Fact]
    public async Task Evaluation_channels_derive_total_and_persist()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);

        var created = await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand,
            new[] { new { salesChannelId = web, monthlyRevenue = 600_000m }, new { salesChannelId = trendyol, monthlyRevenue = 400_000m } }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(1000000, body.GetProperty("brand").GetProperty("economics").GetProperty("averageMonthlyRevenue").GetDecimal());

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{id}");
        var rows = detail.GetProperty("brand").GetProperty("revenueChannels");
        Assert.Equal(2, rows.GetArrayLength());

        // Kanalsız güncelleme eski satırları temizler, elle yazılan toplamı korur.
        var cleared = await admin.PutAsJsonAsync($"/api/evaluations/{id}", DraftPayload(brand, Array.Empty<object>()));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{id}");
        Assert.Equal(0, after.GetProperty("brand").GetProperty("revenueChannels").GetArrayLength());
        Assert.Equal(0, after.GetProperty("brand").GetProperty("economics").GetProperty("averageMonthlyRevenue").GetDecimal());

        // Başka markaya ait kanal reddedilir.
        var stranger = await admin.PutAsJsonAsync($"/api/evaluations/{id}", DraftPayload(brand,
            new[] { new { salesChannelId = Guid.NewGuid(), monthlyRevenue = 100m } }));
        Assert.Equal(HttpStatusCode.Conflict, stranger.StatusCode);
    }

    [Fact]
    public async Task Scenario_channels_derive_total_copy_and_calculate()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);
        var channels = new[] { new { salesChannelId = web, monthlyRevenue = 600_000m }, new { salesChannelId = trendyol, monthlyRevenue = 400_000m } };

        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand, channels))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var calculated = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios/calculate", ScenarioPayload(channels));
        calculated.EnsureSuccessStatusCode();
        // 1.000.000 x (1 - %5 iade) = 950.000 hesaplamaya esas ciro.
        Assert.Equal(950000, (await calculated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commissionableRevenue").GetDecimal());

        var saved = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios", ScenarioPayload(channels));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var savedBody = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var scenarioId = savedBody.GetProperty("scenario").GetProperty("id").GetGuid();
        Assert.Equal(1000000, savedBody.GetProperty("scenario").GetProperty("monthlyRevenue").GetDecimal());

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}/scenarios");
        var names = list[0].GetProperty("revenueChannels").EnumerateArray()
            .Select(r => r.GetProperty("salesChannel").GetProperty("name").GetString()).OrderBy(n => n).ToList();
        Assert.Equal(["Trendyol", "Web sitesi"], names);

        var duplicated = await admin.PostAsync($"/api/evaluations/{evaluationId}/scenarios/{scenarioId}/duplicate", null);
        duplicated.EnsureSuccessStatusCode();
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}/scenarios");
        Assert.Equal(2, after.GetArrayLength());
        Assert.All(after.EnumerateArray(), s => Assert.Equal(2, s.GetProperty("revenueChannels").GetArrayLength()));
    }
}
