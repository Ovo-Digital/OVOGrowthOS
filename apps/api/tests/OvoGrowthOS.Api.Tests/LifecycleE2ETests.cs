using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

// Locks the product's critical path with a single brand travelling from evaluation
// to portal publication: evaluate → approve → deal → activate → close → invoice →
// pay → promise → publish → customer reads. Feature-level edge cases live in their
// own test classes; this file only proves the chain holds together.
public sealed class LifecycleE2ETests
{
    [Fact]
    public async Task Brand_travels_from_evaluation_to_published_report_without_breaking_finances()
    {
        await using var factory = new WorkflowApiFactory();
        var brandId = await factory.SeedAsync();
        using var admin = CustomerPortalTests.Staff(factory);
        using var partner = CustomerPortalTests.Staff(factory, "Partner");

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
        var evaluationId = (await (await admin.PostAsJsonAsync("/api/evaluations", draft)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/evaluations/{evaluationId}/analyze", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/evaluations/{evaluationId}/approve", null)).EnsureSuccessStatusCode();

        var dealResponse = await admin.PostAsJsonAsync($"/api/deals/from-evaluation/{evaluationId}", new
        {
            name = "Uçtan uca sabit ücret", dealType = "FixedRetainer", contractMonths = 24,
            baselineRevenue = 1_000_000m, baselinePeriodStart = (string?)null, baselinePeriodEnd = (string?)null,
            baselineCalculationMethod = "Manual", monthlyRetainer = 50_000m, minimumMonthlyFee = 0m,
            revenueShareRate = 0m, incrementalRate = 0m, profitShareRate = 0m,
            commissionTiers = Array.Empty<object>(), setupInvestment = 120_000m, estimatedMonthlyInternalCost = 20_000m
        });
        dealResponse.EnsureSuccessStatusCode();
        var dealId = (await dealResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/deals/{dealId}/accept", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/deals/{dealId}/activate", null)).EnsureSuccessStatusCode();

        var periodResponse = await admin.PostAsJsonAsync("/api/performance", new
        {
            brandId, dealId, year = 2026, month = 9, grossSales = 1_200_000m, vat = 200_000m,
            refunds = 40_000m, cancellations = 5_000m, chargebacks = 0m, customerPaidShipping = 0m,
            giftCardTopups = 0m, orders = 500, sessions = 25_000, newCustomers = 300, returningCustomers = 200,
            cogs = 420_000m, paymentFees = 20_000m, fulfillmentCosts = 30_000m, shippingSubsidy = 10_000m,
            otherVariableCosts = 5_000m, metaSpend = 100_000m, googleSpend = 50_000m, tikTokSpend = 0m,
            influencerSpend = 0m, otherAdSpend = 0m
        });
        periodResponse.EnsureSuccessStatusCode();
        var periodId = (await periodResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await PeriodEditingTests.SetVersion(partner, periodId);
        (await partner.PostAsync($"/api/performance/{periodId}/submit", null)).EnsureSuccessStatusCode();
        await PeriodEditingTests.SetVersion(admin, periodId);
        (await admin.PostAsync($"/api/performance/{periodId}/approve", null)).EnsureSuccessStatusCode();
        await PeriodEditingTests.SetVersion(admin, periodId);
        (await admin.PostAsync($"/api/performance/{periodId}/lock", null)).EnsureSuccessStatusCode();

        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        (await admin.PutAsJsonAsync($"/api/performance/{periodId}/collection/invoice",
            new { reference = "E2E-FATURA", invoiceOn = today, dueOn = today.AddDays(10), reason = "", revision = 0 })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/performance/{periodId}/collection/payments",
            new { id = Guid.NewGuid(), amount = 20_000m, paidOn = today, reference = "E2E-ODEME", note = "", revision = 1 })).EnsureSuccessStatusCode();

        var plan = await admin.GetFromJsonAsync<JsonElement>($"/api/brands/{brandId}/payment-plan?amount=30000&currency=TRY");
        Assert.Equal(30_000, plan.GetProperty("allocated").GetDecimal());
        Assert.Equal(0, plan.GetProperty("leftover").GetDecimal());

        Guid noteId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var note = new BrandContactNote { BrandId = brandId, ContactOn = today.AddDays(-1), Text = "Uçtan uca ödeme sözü görüşmesi." };
            db.Add(note); await db.SaveChangesAsync(); noteId = note.Id;
        }
        var adminId = WorkflowApiFactory.AccountId("admin@ovo.test");
        (await admin.PutAsJsonAsync($"/api/performance/{periodId}/collection/promise",
            new CollectionPromiseRequest(10_000m, today.AddDays(3), noteId, adminId, "Uçtan uca söz.", 0, 2))).EnsureSuccessStatusCode();

        var (client, _) = await CustomerPortalTests.Customer(factory, admin, brandId);
        using var customer = client;
        var publish = await admin.PostAsJsonAsync($"/api/portal-management/brands/{brandId}/reports", new PortalPublishRequest(periodId));
        publish.EnsureSuccessStatusCode();
        var reportId = (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await customer.GetAsync($"/api/portal/reports/{reportId}")).EnsureSuccessStatusCode();
        var customerPdf = await customer.GetAsync($"/api/portal/reports/{reportId}/pdf");
        customerPdf.EnsureSuccessStatusCode();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(await customerPdf.Content.ReadAsByteArrayAsync(), 0, 4));

        foreach (var path in new[] { $"/api/deals/{dealId}/pdf", $"/api/performance/{periodId}/statement-pdf" })
        {
            var pdf = await admin.GetAsync(path);
            pdf.EnsureSuccessStatusCode();
            Assert.Equal("%PDF", Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(), 0, 4));
        }
        var brandPdf = await admin.GetAsync($"/api/reports/brands/{brandId}/pdf?year=2026&month=9&audience=brand");
        brandPdf.EnsureSuccessStatusCode();

        var closed = await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{periodId}");
        Assert.Equal(50_000, closed.GetProperty("ovoFee").GetDecimal());
        var dashboard = await admin.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.True(dashboard.GetProperty("paidCommission").GetDecimal() >= 20_000m);
        var profit = await admin.GetFromJsonAsync<JsonElement>("/api/reports/brand-profitability?currency=TRY");
        var row = profit.GetProperty("rows").EnumerateArray().Single(x => x.GetProperty("brandId").GetGuid() == brandId);
        Assert.Equal(50_000, row.GetProperty("contribution").GetDecimal());
    }
}
