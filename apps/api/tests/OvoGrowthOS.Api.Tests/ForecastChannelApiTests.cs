using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

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

    private static object ScenarioRatePayload(object? revenueChannels) => new
    {
        name = "Kanallı pay", monthlyRevenue = 0m, grossMarginRate = .6m, adSpend = 150_000m, returnRate = .05m,
        averageOrderValue = 2_000m, newCustomers = 400, variableCostRate = .08m, ovoInternalMonthlyCost = 25_000m,
        minimumMonthlyFee = 0m, commissionModel = "FlatRevenueShare", revenueShareRate = .05m, monthlyRetainer = 0m,
        baselineRevenue = 0m, incrementalRate = .1m, profitShareRate = .1m, commissionTiers = Array.Empty<object>(),
        targetBrandContributionMargin = .15m, setupInvestment = 250_000m, contractMonths = 24,
        revenueChannels,
    };

    [Fact]
    public async Task Scenario_channel_rates_calculate_persist_and_copy()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);
        var channels = new[]
        {
            new { salesChannelId = web, monthlyRevenue = 600_000m, revenueShareRate = (decimal?).05m },
            new { salesChannelId = trendyol, monthlyRevenue = 400_000m, revenueShareRate = (decimal?).03m },
        };
        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand, null))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // %5 iade sonrası web 570.000 x %5 + Trendyol 380.000 x %3 = 39.900.
        var calculated = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios/calculate", ScenarioRatePayload(channels));
        calculated.EnsureSuccessStatusCode();
        var calcBody = await calculated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(39900, calcBody.GetProperty("ovoFee").GetDecimal());
        Assert.Equal(2, calcBody.GetProperty("channelShares").GetArrayLength());

        var saved = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios", ScenarioRatePayload(channels));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var scenarioId = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("scenario").GetProperty("id").GetGuid();

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}/scenarios");
        var rates = list[0].GetProperty("revenueChannels").EnumerateArray()
            .ToDictionary(r => r.GetProperty("salesChannel").GetProperty("name").GetString()!, r => r.GetProperty("revenueShareRate").GetDecimal());
        Assert.Equal(.05m, rates["Web sitesi"]);
        Assert.Equal(.03m, rates["Trendyol"]);

        (await admin.PostAsync($"/api/evaluations/{evaluationId}/scenarios/{scenarioId}/duplicate", null)).EnsureSuccessStatusCode();
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}/scenarios");
        Assert.All(after.EnumerateArray(), s => Assert.Equal(2, s.GetProperty("revenueChannels").EnumerateArray()
            .Count(r => r.TryGetProperty("revenueShareRate", out var rate) && rate.ValueKind == JsonValueKind.Number)));
    }

    [Fact]
    public async Task Scenario_channel_rates_rejected_for_unsupported_model_and_bad_rate()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);
        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand, null))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var tiered = ScenarioRatePayload(new[] { new { salesChannelId = web, monthlyRevenue = 600_000m, revenueShareRate = (decimal?).05m } });
        var tieredBody = new Dictionary<string, object?>(tiered.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(tiered))) { ["commissionModel"] = "TieredRevenueShare" };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios/calculate", tieredBody)).StatusCode);

        var badRate = ScenarioRatePayload(new[] { new { salesChannelId = trendyol, monthlyRevenue = 400_000m, revenueShareRate = (decimal?)1.5m } });
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios", badRate)).StatusCode);
    }

    [Fact]
    public async Task Scenario_incremental_baselines_calculate_and_persist()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);
        var channels = new[]
        {
            new { salesChannelId = web, monthlyRevenue = 500_000m, revenueShareRate = (decimal?).05m, baselineRevenue = 300_000m },
            new { salesChannelId = trendyol, monthlyRevenue = 8_000_000m, revenueShareRate = (decimal?).02m, baselineRevenue = 7_000_000m },
        };
        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand, null))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var payload = new Dictionary<string, object?>(ScenarioRatePayload(channels).GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(ScenarioRatePayload(channels))))
        {
            ["commissionModel"] = "IncrementalRevenueShare",
            ["incrementalRate"] = 0.02m,
            ["returnRate"] = 0m,
        };
        var calculated = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios/calculate", payload);
        calculated.EnsureSuccessStatusCode();
        var calcBody = await calculated.Content.ReadFromJsonAsync<JsonElement>();
        // (500.000-300.000) x %5 + (8.000.000-7.000.000) x %2 = 30.000.
        Assert.Equal(30000, calcBody.GetProperty("ovoFee").GetDecimal());

        var saved = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios", payload);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}/scenarios");
        var baselines = list[0].GetProperty("revenueChannels").EnumerateArray()
            .ToDictionary(r => r.GetProperty("salesChannel").GetProperty("name").GetString()!, r => r.GetProperty("baselineRevenue").GetDecimal());
        Assert.Equal(300000, baselines["Web sitesi"]);
        Assert.Equal(7000000, baselines["Trendyol"]);
    }

    [Fact]
    public async Task Scenario_total_share_calculates_from_full_revenue()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);
        var channels = new[]
        {
            new { salesChannelId = web, monthlyRevenue = 500_000m, revenueShareRate = (decimal?).05m, baselineRevenue = 300_000m, totalRevenueShare = false },
            new { salesChannelId = trendyol, monthlyRevenue = 8_000_000m, revenueShareRate = (decimal?).02m, baselineRevenue = 7_000_000m, totalRevenueShare = true },
        };
        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand, null))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var payload = new Dictionary<string, object?>(ScenarioRatePayload(channels).GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(ScenarioRatePayload(channels))))
        {
            ["commissionModel"] = "IncrementalRevenueShare",
            ["incrementalRate"] = 0.02m,
            ["returnRate"] = 0m,
        };
        var calculated = await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios/calculate", payload);
        calculated.EnsureSuccessStatusCode();
        // Web büyümeden: (500.000-300.000) x %5 = 10.000; Trendyol toplamdan: 8.000.000 x %2 = 160.000.
        Assert.Equal(170000, (await calculated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ovoFee").GetDecimal());

        var flatPayload = new Dictionary<string, object?>(ScenarioRatePayload(channels).GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(ScenarioRatePayload(channels))))
        {
            ["commissionModel"] = "FlatRevenueShare",
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/evaluations/{evaluationId}/scenarios/calculate", flatPayload)).StatusCode);
    }

    [Fact]
    public async Task Analysis_recommends_channel_shares_from_minimum_fee()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        var (web, trendyol) = await SeedChannels(admin, brand);
        var channels = new[] { new { salesChannelId = web, monthlyRevenue = 600_000m }, new { salesChannelId = trendyol, monthlyRevenue = 400_000m } };
        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand, channels))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.RuleSets.SelectMany(x => x.Rules).FirstAsync()).RecommendationEffect = RecommendationEffect.AllowTieredHybrid;
            await db.SaveChangesAsync();
        }
        (await admin.PostAsync($"/api/evaluations/{evaluationId}/analyze", null)).EnsureSuccessStatusCode();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}");
        var snapshotProp = detail.GetProperty("recommendationSnapshotJson");
        var snapshot = snapshotProp.ValueKind == JsonValueKind.String
            ? JsonSerializer.Deserialize<JsonElement>(snapshotProp.GetString()!)
            : snapshotProp;
        // Öneri anlık görüntüsünde enum'lar sayı olarak saklanır (mevcut davranış).
        Assert.Equal(3, snapshot.GetProperty("recommendedDealType").GetInt32());
        var shares = snapshot.GetProperty("recommendedChannelShares");
        Assert.Equal(2, shares.GetArrayLength());
        var byName = shares.EnumerateArray().ToDictionary(s => s.GetProperty("channelName").GetString()!);
        // 30.000 iç maliyet x 1,8 = 54.000 asgari ücret; 1.000.000 ciroya oranla %5,4.
        Assert.Equal(.054m, byName["Web sitesi"].GetProperty("recommendedRate").GetDecimal());
        Assert.Equal(32_400, byName["Web sitesi"].GetProperty("estimatedFee").GetDecimal());
        Assert.Equal(21_600, byName["Trendyol"].GetProperty("estimatedFee").GetDecimal());
    }

    [Fact]
    public async Task Evaluation_create_without_economics_row_persists_total()
    {
        await using var f = new WorkflowApiFactory();
        using var admin = CustomerPortalTests.Staff(f);
        Guid brand;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bare = new Brand { Name = "Ekonomisiz Marka" };
            db.Brands.Add(bare);
            await db.SaveChangesAsync();
            brand = bare.Id;
        }
        var (web, trendyol) = await SeedChannels(admin, brand);
        var created = await admin.PostAsJsonAsync("/api/evaluations", DraftPayload(brand,
            new[] { new { salesChannelId = web, monthlyRevenue = 600_000m }, new { salesChannelId = trendyol, monthlyRevenue = 400_000m } }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/evaluations/{id}");
        Assert.Equal(1000000, detail.GetProperty("brand").GetProperty("economics").GetProperty("averageMonthlyRevenue").GetDecimal());
        Assert.Equal(2, detail.GetProperty("brand").GetProperty("revenueChannels").GetArrayLength());
    }

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
