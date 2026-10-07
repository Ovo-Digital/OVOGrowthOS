using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class SalesChannelApiTests
{
    [Fact]
    public async Task Channels_rates_and_period_lines_work_end_to_end()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);

        var web = await admin.PostAsJsonAsync($"/api/brands/{brand}/sales-channels", new { name = "Web sitesi" });
        Assert.Equal(HttpStatusCode.Created, web.StatusCode);
        var webId = (await web.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var trendyol = await admin.PostAsJsonAsync($"/api/brands/{brand}/sales-channels", new { name = "Trendyol" });
        Assert.Equal(HttpStatusCode.Created, trendyol.StatusCode);
        var trendyolId = (await trendyol.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/brands/{brand}/sales-channels", new { name = "trendyol" })).StatusCode);
        var listed = await admin.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/sales-channels");
        Assert.Equal(2, listed.GetArrayLength());

        Guid deal;
        {
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var d = new Deal { BrandId = brand, Name = "Kanallı anlaşma", Status = DealStatus.Active,
                DealType = DealType.FlatRevenueShare, RevenueShareRate = .05m, EstimatedMonthlyInternalCost = 10_000, Currency = "TRY" };
            db.Add(d); await db.SaveChangesAsync(); deal = d.Id;
        }

        var rates = await admin.PutAsJsonAsync($"/api/deals/{deal}/channel-rates",
            new { rates = new[] { new { salesChannelId = trendyolId, revenueShareRate = 0.03m } } });
        Assert.Equal(HttpStatusCode.OK, rates.StatusCode);
        var savedRates = await rates.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.03m, savedRates[0].GetProperty("revenueShareRate").GetDecimal());

        var stranger = await admin.PutAsJsonAsync($"/api/deals/{deal}/channel-rates",
            new { rates = new[] { new { salesChannelId = Guid.NewGuid(), revenueShareRate = 0.03m } } });
        Assert.Equal(HttpStatusCode.Conflict, stranger.StatusCode);

        var created = await admin.PostAsJsonAsync("/api/performance", new
        {
            brandId = brand, dealId = deal, year = 2026, month = 9,
            grossSales = 0, vat = 0, refunds = 0, cancellations = 0, chargebacks = 0, customerPaidShipping = 0, giftCardTopups = 0,
            orders = 0, sessions = 0, newCustomers = 0, returningCustomers = 0, cogs = 0, paymentFees = 0,
            fulfillmentCosts = 0, shippingSubsidy = 0, otherVariableCosts = 0, metaSpend = 0,
            googleSpend = 0, tikTokSpend = 0, influencerSpend = 0, otherAdSpend = 0,
            channels = new[]
            {
                new { salesChannelId = webId, grossSales = 700000, vat = 100000, refunds = 20000, cancellations = 0, chargebacks = 0, customerPaidShipping = 0, giftCardTopups = 0 },
                new { salesChannelId = trendyolId, grossSales = 500000, vat = 70000, refunds = 30000, cancellations = 0, chargebacks = 0, customerPaidShipping = 0, giftCardTopups = 0 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        // Başlık toplamları kanal satırlarından gelir: 1.200.000 brüt, 980.000 net.
        Assert.Equal(1200000, body.GetProperty("grossSales").GetDecimal());
        Assert.Equal(980000, body.GetProperty("netRevenue").GetDecimal());
        // Web 580.000 x %5 = 29.000, Trendyol 400.000 x %3 = 12.000.
        Assert.Equal(41000, body.GetProperty("ovoFee").GetDecimal());

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/performance/{id}");
        Assert.Equal(2, detail.GetProperty("channels").GetArrayLength());
        var breakdown = JsonDocument.Parse(detail.GetProperty("commissionBreakdownJson").GetString()!).RootElement;
        Assert.Equal(2, breakdown.GetProperty("channels").GetArrayLength());

        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.DeleteAsync($"/api/sales-channels/{webId}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_channel_is_rejected_and_unused_channel_can_be_deleted()
    {
        await using var f = new WorkflowApiFactory();
        var brand = await f.SeedAsync();
        using var admin = CustomerPortalTests.Staff(f);
        Guid deal;
        {
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var d = new Deal { BrandId = brand, Name = "Kanalsız anlaşma", Status = DealStatus.Active,
                DealType = DealType.FlatRevenueShare, RevenueShareRate = .05m, Currency = "TRY" };
            db.Add(d); await db.SaveChangesAsync(); deal = d.Id;
        }
        var bad = await admin.PostAsJsonAsync("/api/performance/calculate", new
        {
            brandId = brand, dealId = deal, year = 2026, month = 10,
            grossSales = 0, vat = 0, refunds = 0, cancellations = 0, chargebacks = 0, customerPaidShipping = 0, giftCardTopups = 0,
            orders = 0, sessions = 0, newCustomers = 0, returningCustomers = 0, cogs = 0, paymentFees = 0,
            fulfillmentCosts = 0, shippingSubsidy = 0, otherVariableCosts = 0, metaSpend = 0,
            googleSpend = 0, tikTokSpend = 0, influencerSpend = 0, otherAdSpend = 0,
            channels = new[]
            {
                new { salesChannelId = Guid.NewGuid(), grossSales = 1000, vat = 0, refunds = 0, cancellations = 0, chargebacks = 0, customerPaidShipping = 0, giftCardTopups = 0 },
            },
        });
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);

        var created = await admin.PostAsJsonAsync($"/api/brands/{brand}/sales-channels", new { name = "Kullanılmayan kanal" });
        var channelId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/sales-channels/{channelId}")).StatusCode);
    }
}
