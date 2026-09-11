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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Auth;
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
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/performance/{periodId}/adjustments", new { amount = 5_000m, reason = "Approved reconciliation" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("partner@ovo.test", "Partner"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/performance/{periodId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/performance/{periodId}/approve", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/performance/{periodId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/performance/{periodId}/lock", null)).StatusCode);
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/performance/{periodId}/collection/invoice", new { reference = "TEST-FATURA", invoiceOn = today, dueOn = today, reason = "", revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/performance/{periodId}/collection/payments", new { id = Guid.NewGuid(), amount = 55_000m, paidOn = today, reference = "TEST-ODEME", note = "", revision = 1 })).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/performance/{periodId}/adjustments", new { amount = 1_000m, reason = "Ödeme sonrası değişiklik" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/performance/{periodId}/unlock", new { reason = "Ödeme sonrası açma denemesi" })).StatusCode);

        var closed = await client.GetFromJsonAsync<JsonElement>($"/api/performance/{periodId}");
        Assert.Equal("Paid", closed.GetProperty("status").GetString());
        Assert.Equal(55_000m, closed.GetProperty("ovoFee").GetDecimal());
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.True(dashboard.GetProperty("activeBrands").GetInt32() >= 1);
        Assert.True(dashboard.GetProperty("paidCommission").GetDecimal() >= 55_000m);
    }

    [Fact]
    public async Task Rejected_evaluation_cannot_be_approved()
    {
        var evaluationId = await _factory.SeedRejectedEvaluationAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));

        var response = await client.PostAsync($"/api/evaluations/{evaluationId}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_monthly_performance_is_rejected_before_database_write()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));

        var response = await client.PostAsJsonAsync("/api/performance", new
        {
            brandId = Guid.Empty, dealId = Guid.Empty, year = 2010, month = 13, grossSales = -1m,
            vat = 0m, refunds = 0m, cancellations = 0m, chargebacks = 0m, customerPaidShipping = 0m,
            giftCardTopups = 0m, orders = -1, sessions = 0, newCustomers = 0, returningCustomers = 0,
            cogs = 0m, paymentFees = 0m, fulfillmentCosts = 0m, shippingSubsidy = 0m,
            otherVariableCosts = 0m, metaSpend = 0m, googleSpend = 0m, tikTokSpend = 0m,
            influencerSpend = 0m, otherAdSpend = 0m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_one_deal_can_be_active_for_a_brand()
    {
        var (firstId, secondId) = await _factory.SeedAcceptedDealsAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/deals/{firstId}/activate", null)).StatusCode);
        var second = await client.PostAsync($"/api/deals/{secondId}/activate", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Condition_resolution_and_document_upload_are_auditable()
    {
        var brandId = await _factory.SeedAsync();
        var conditionId = await _factory.SeedConditionAsync(brandId);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("partner@ovo.test", "Partner"));

        var condition = await client.PutAsJsonAsync($"/api/conditions/{conditionId}", new { status = "Satisfied", reason = "Erişim kontrol edildi", evidenceUrl = "https://example.test/evidence" });
        Assert.Equal(HttpStatusCode.OK, condition.StatusCode);
        var conditionJson = await condition.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("partner@ovo.test", conditionJson.GetProperty("resolvedBy").GetString());

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "sozlesme.pdf");
        content.Add(new StringContent("İmzalı sözleşme"), "note");
        var upload = await client.PostAsync($"/api/documents/Brand/{brandId}", content);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var download = await client.GetAsync($"/api/documents/{uploaded.GetProperty("id").GetGuid()}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
    }

    [Fact]
    public async Task Active_deal_can_create_renewal_and_be_terminated_with_reason()
    {
        var (firstId, _) = await _factory.SeedAcceptedDealsAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token("admin@ovo.test", "Admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/deals/{firstId}/activate", null)).StatusCode);

        var renewal = await client.PostAsJsonAsync($"/api/deals/{firstId}/renew", new { reason = "Yeni dönem görüşmesi başladı" });
        Assert.Equal(HttpStatusCode.Created, renewal.StatusCode);
        var terminate = await client.PostAsJsonAsync($"/api/deals/{firstId}/terminate", new { reason = "Tarafların karşılıklı mutabakatı" });
        Assert.Equal(HttpStatusCode.OK, terminate.StatusCode);
    }
}

public sealed class WorkflowApiFactory : WebApplicationFactory<Program>
{
    private const string JwtKey = "local-development-key-change-before-production-32chars";
    private readonly string _databaseName = $"workflow-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtKey, ["Jwt:Issuer"] = "ovo-growth-os", ["Jwt:Audience"] = "ovo-growth-os-web",
            ["DefaultAdmin:Email"] = "admin@ovo.test", ["DefaultAdmin:PasswordHash"] = JwtTokenService.HashPassword(TestPassword)
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public const string TestPassword = "Test-only-password-2026!";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        foreach (var role in new[] { "Admin", "Partner", "Analyst" })
        {
            var email = $"{role.ToLowerInvariant()}@ovo.test";
            db.UserAccounts.Add(new UserAccount { Id = AccountId(email), Email = email, Name = role,
                Role = role, PasswordHash = JwtTokenService.HashPassword(TestPassword) });
        }
        db.SaveChanges();
        return host;
    }

    public static Guid AccountId(string email) => email switch
    {
        "admin@ovo.test" => Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "partner@ovo.test" => Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "analyst@ovo.test" => Guid.Parse("33333333-3333-3333-3333-333333333333"),
        _ => throw new ArgumentOutOfRangeException(nameof(email))
    };

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

    public async Task<Guid> SeedRejectedEvaluationAsync()
    {
        _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var evaluation = new BrandEvaluation
        {
            BrandId = Guid.NewGuid(),
            Brand = new Brand { Name = $"Reddedilen Marka {Guid.NewGuid():N}", Economics = new BrandEconomics() },
            Status = EvaluationStatus.Rejected,
            Decision = DecisionStatus.Reject,
            CreatedBy = "test"
        };
        evaluation.BrandId = evaluation.Brand.Id;
        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();
        return evaluation.Id;
    }

    public async Task<(Guid FirstId, Guid SecondId)> SeedAcceptedDealsAsync()
    {
        _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var brand = new Brand { Name = $"Tek Anlaşmalı Marka {Guid.NewGuid():N}", Economics = new BrandEconomics() };
        var evaluation = new BrandEvaluation { BrandId = brand.Id, Brand = brand, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
        var first = new Deal { BrandId = brand.Id, Brand = brand, EvaluationId = evaluation.Id, Name = "Birinci", Status = DealStatus.Accepted };
        var second = new Deal { BrandId = brand.Id, Brand = brand, EvaluationId = evaluation.Id, Name = "İkinci", Status = DealStatus.Accepted };
        db.AddRange(evaluation, first, second);
        await db.SaveChangesAsync();
        return (first.Id, second.Id);
    }

    public async Task<Guid> SeedConditionAsync(Guid brandId)
    {
        _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var evaluation = new BrandEvaluation { BrandId = brandId, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
        var condition = new PartnershipCondition { EvaluationId = evaluation.Id, Code = $"TEST-{Guid.NewGuid():N}", Title = "Test koşulu" };
        evaluation.Conditions.Add(condition);
        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();
        return condition.Id;
    }

    public static string Token(string email, string role, bool includeSession = true)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, email), new(JwtRegisteredClaimNames.Email, email), new(ClaimTypes.Role, role) };
        if (includeSession) claims.AddRange([new("uid", AccountId(email).ToString()), new("session_version", "0")]);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("ovo-growth-os", "ovo-growth-os-web", claims,
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: credentials));
    }
}
