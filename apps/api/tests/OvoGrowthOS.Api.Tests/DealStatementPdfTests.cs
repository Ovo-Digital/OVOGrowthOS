using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class DealStatementPdfTests
{
    [Fact]
    public void Build_deal_renders_terms_and_conditions_as_a_pdf()
    {
        var deal = new Deal
        {
            Name = "Büyüme ortaklığı", Status = DealStatus.Active, DealType = DealType.RetainerPlusRevenueShare,
            StartDate = new(2026, 1, 1), EndDate = new(2027, 12, 31), ContractMonths = 24,
            MonthlyRetainer = 50_000, RevenueShareRate = 0.05m, SetupInvestment = 100_000,
            EstimatedMonthlyInternalCost = 30_000, Currency = "TRY", StatusReason = "Müzakerede karar verildi.",
            Conditions =
            [
                new() { Code = "STOK", Title = "Stok yeterliliği", Required = true, Status = ConditionStatus.Satisfied, ResolutionReason = "Depo fotoğrafları alındı." },
                new() { Code = "ICERIK", Title = "İçerik planı", Required = false, Status = ConditionStatus.Pending },
            ]
        };
        var bytes = DealStatementPdf.BuildDeal(deal, "Örnek Marka");
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Build_statement_renders_balance_and_payments_as_a_pdf()
    {
        var period = new MonthlyPerformance
        {
            Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Invoiced, OvoFee = 100_000,
            NetRevenue = 1_000_000, TotalAdSpend = 200_000, BrandContributionProfit = 150_000,
            Deal = new Deal { Name = "Büyüme ortaklığı", Currency = "TRY" },
            Collection = new CollectionAccount
            {
                ReceivableAmount = 100_000, Currency = "TRY", InvoiceReference = "FAT-8",
                InvoiceOn = new(2026, 9, 1), DueOn = new(2026, 9, 15), Revision = 2,
                Payments = [new() { Amount = 40_000, PaidOn = new(2026, 9, 10), Reference = "BANKA-1" }]
            }
        };
        var bytes = DealStatementPdf.BuildStatement(period, "Örnek Marka", Collections.Balance(period, TeamWork.Today(DateTimeOffset.UtcNow)), period.Collection.Payments);
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Deal_and_statement_download_as_pdf_with_the_same_visibility_as_their_screens()
    {
        await using var factory = new WorkflowApiFactory();
        var brand = await factory.SeedAsync();
        Guid dealId, periodId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deal = new Deal { BrandId = brand, Name = "PDF anlaşması", Status = DealStatus.Active, DealType = DealType.FlatRevenueShare, Currency = "TRY", RevenueShareRate = 0.08m };
            db.Add(deal);
            var period = new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = 2026, Month = 8, Status = MonthlyPerformanceStatus.Locked, OvoFee = 100_000, NetRevenue = 1_000_000, OvoGrossProfit = 70_000, CommissionBreakdownJson = "{}" };
            db.Add(period);
            await db.SaveChangesAsync();
            dealId = deal.Id; periodId = period.Id;
        }
        using var admin = CustomerPortalTests.Staff(factory);
        using var analyst = CustomerPortalTests.Staff(factory, "Analyst");

        var dealPdf = await admin.GetAsync($"/api/deals/{dealId}/pdf");
        dealPdf.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", dealPdf.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".pdf", dealPdf.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(await dealPdf.Content.ReadAsByteArrayAsync(), 0, 4));

        var statement = await admin.GetAsync($"/api/performance/{periodId}/statement-pdf");
        statement.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", statement.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(await statement.Content.ReadAsByteArrayAsync(), 0, 4));

        // Same visibility as the screens: analysts read both screens, outsiders read neither.
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync($"/api/deals/{dealId}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync($"/api/performance/{periodId}/statement-pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/deals/{Guid.NewGuid()}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/performance/{Guid.NewGuid()}/statement-pdf")).StatusCode);
    }
}
