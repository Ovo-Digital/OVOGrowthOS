using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class WorkflowApiTests : IClassFixture<WorkflowApiFactory>
{
    private readonly WorkflowApiFactory _factory;

    public WorkflowApiTests(WorkflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Protected_endpoint_rejects_anonymous_requests()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/brands");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_can_read_but_cannot_create_brand()
    {
        await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("analyst@ovo.test", "Analyst"));

        var read = await client.GetAsync("/api/brands");
        var write = await client.PostAsJsonAsync("/api/brands", new { name = "Unauthorized Brand" });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Draft_can_be_saved_resumed_and_analyzed_with_frozen_snapshots()
    {
        var brandId = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("analyst@ovo.test", "Analyst"));
        var draft = new
        {
            brandId, currentStep = 6, status = "InProgress", averageMonthlyRevenue = 1_000_000m,
            revenueConfidence = "Verified", grossMarginRate = .55m, grossMarginConfidence = "Verified",
            cogsRate = .45m, cogsConfidence = "Verified", averageOrderValue = 2_000m, aovConfidence = "ProvidedByBrand",
            returnRate = .08m, returnRateConfidence = "Verified", currentAdSpend = 160_000m, adSpendConfidence = "Verified",
            currentCac = 400m, cacConfidence = "Estimated", averageCustomerLtv = 4_000m, ltvConfidence = "Estimated",
            stockCoverageDays = 75, stockCoverageConfidence = "ProvidedByBrand", monthlyOrders = 500,
            monthlySessions = 35_000, newCustomers = 400, returningCustomers = 100, variableCostRate = .08m,
            productMarketFit = 5, growthPotential = 4, operationalReadiness = 4, creativeCapability = 4,
            founderCooperation = 5, dataMaturity = 4, internalMonthlyCost = 30_000m, setupInvestment = 250_000m
        };

        var created = await client.PostAsJsonAsync("/api/evaluations", draft);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var evaluationId = createdJson.GetProperty("id").GetGuid();

        var resumed = await client.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}");
        Assert.Equal(6, resumed.GetProperty("currentStep").GetInt32());
        Assert.Equal(1_000_000m, resumed.GetProperty("brand").GetProperty("economics").GetProperty("averageMonthlyRevenue").GetDecimal());

        var analyzed = await client.PostAsync($"/api/evaluations/{evaluationId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzed.StatusCode);
        var frozen = await client.GetFromJsonAsync<JsonElement>($"/api/evaluations/{evaluationId}");
        Assert.NotEqual("{}", frozen.GetProperty("inputSnapshotJson").GetString());
        Assert.NotEqual("{}", frozen.GetProperty("ruleSnapshotJson").GetString());
        Assert.NotEqual("{}", frozen.GetProperty("calculationSnapshotJson").GetString());
        Assert.NotEqual("{}", frozen.GetProperty("recommendationSnapshotJson").GetString());
        Assert.Equal(10, frozen.GetProperty("currentStep").GetInt32());
    }

    [Fact]
    public async Task Approved_evaluation_can_reach_paid_close_and_database_dashboard()
    {
        var brandId = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        var draft = new
        {
            brandId, currentStep = 9, status = "InProgress", averageMonthlyRevenue = 1_000_000m,
            revenueConfidence = "Verified", grossMarginRate = .55m, grossMarginConfidence = "Verified",
            cogsRate = .45m, cogsConfidence = "Verified", averageOrderValue = 2_000m, aovConfidence = "Verified",
            returnRate = .08m, returnRateConfidence = "Verified", currentAdSpend = 160_000m, adSpendConfidence = "Verified",
            currentCac = 400m, cacConfidence = "Estimated", averageCustomerLtv = 4_000m, ltvConfidence = "Estimated",
            stockCoverageDays = 75, stockCoverageConfidence = "Verified", monthlyOrders = 500, monthlySessions = 35_000,
            newCustomers = 400, returningCustomers = 100, variableCostRate = .08m, productMarketFit = 5,
            growthPotential = 4, operationalReadiness = 4, creativeCapability = 4, founderCooperation = 5,
            dataMaturity = 4, internalMonthlyCost = 20_000m, setupInvestment = 120_000m
        };
        var evaluationResponse = await client.PostAsJsonAsync("/api/evaluations", draft);
        var evaluation = await evaluationResponse.Content.ReadFromJsonAsync<JsonElement>();
        var evaluationId = evaluation.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/evaluations/{evaluationId}/analyze", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/evaluations/{evaluationId}/approve", null)).StatusCode);

        var dealResponse = await client.PostAsJsonAsync($"/api/deals/from-evaluation/{evaluationId}", new
        {
            name = "Integration fixed retainer", dealType = "FixedRetainer", contractMonths = 24,
            baselineRevenue = 1_000_000m, baselinePeriodStart = (string?)null, baselinePeriodEnd = (string?)null,
            baselineCalculationMethod = "Manual", monthlyRetainer = 50_000m, minimumMonthlyFee = 0m,
            revenueShareRate = 0m, incrementalRate = 0m, profitShareRate = 0m,
            commissionTiers = Array.Empty<object>(), setupInvestment = 120_000m, estimatedMonthlyInternalCost = 20_000m
        });
        Assert.Equal(HttpStatusCode.Created, dealResponse.StatusCode);
        var deal = await dealResponse.Content.ReadFromJsonAsync<JsonElement>();
        var dealId = deal.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/deals/{dealId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/deals/{dealId}/activate", null)).StatusCode);

        var periodResponse = await client.PostAsJsonAsync("/api/performance", new
        {
            brandId, dealId, year = 2026, month = 9, grossSales = 1_200_000m, vat = 200_000m,
            refunds = 40_000m, cancellations = 5_000m, chargebacks = 0m, customerPaidShipping = 0m,
            giftCardTopups = 0m, orders = 500, sessions = 25_000, newCustomers = 300, returningCustomers = 200,
            cogs = 420_000m, paymentFees = 20_000m, fulfillmentCosts = 30_000m, shippingSubsidy = 10_000m,
            otherVariableCosts = 5_000m, metaSpend = 100_000m, googleSpend = 50_000m, tikTokSpend = 0m,
            influencerSpend = 0m, otherAdSpend = 0m
        });
        Assert.Equal(HttpStatusCode.Created, periodResponse.StatusCode);
        var period = await periodResponse.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = period.GetProperty("id").GetGuid();
        foreach (var transition in new[] { "submit", "approve", "lock" })
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/performance/{periodId}/{transition}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/performance/{periodId}/adjustments", new { amount = 5_000m, reason = "Approved reconciliation" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/performance/{periodId}/invoice", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/performance/{periodId}/pay", null)).StatusCode);

        var closed = await client.GetFromJsonAsync<JsonElement>($"/api/performance/{periodId}");
        Assert.Equal("Paid", closed.GetProperty("status").GetString());
        Assert.Equal(55_000m, closed.GetProperty("ovoFee").GetDecimal());
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.True(dashboard.GetProperty("activeBrands").GetInt32() >= 1);
        Assert.True(dashboard.GetProperty("paidCommission").GetDecimal() >= 55_000m);
    }
}

public sealed class WorkflowApiFactory : WebApplicationFactory<Program>
{
    private const string JwtKey = "local-development-key-change-before-production-32chars";
    private readonly string _databaseName = $"workflow-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task<Guid> SeedAsync()
    {
        _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var existing = await db.Brands.Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (existing.HasValue) return existing.Value;

        var rules = new RuleSet { Name = "Test rules", Status = RuleSetStatus.Published, PublishedAt = DateTimeOffset.UtcNow };
        rules.Rules.Add(new Rule { Name = "Healthy margin", Category = RuleCategory.Financial, Field = RuleField.GrossMargin,
            Operator = RuleOperator.GreaterThanOrEqual, Value = .5m, Severity = RuleSeverity.Info,
            RecommendationEffect = RecommendationEffect.AllowFourToSixPercent });
        var settings = new GeneralSettings { DefaultRuleSetId = rules.Id };
        var brand = new Brand { Name = "Persisted Test Brand", Economics = new BrandEconomics() };
        db.AddRange(rules, settings, brand);
        await db.SaveChangesAsync();
        return brand.Id;
    }

    public static string Token(string email, string role)
    {
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, email), new Claim(JwtRegisteredClaimNames.Email, email), new Claim(ClaimTypes.Role, role) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("ovo-growth-os", "ovo-growth-os-web", claims,
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: credentials));
    }
}
