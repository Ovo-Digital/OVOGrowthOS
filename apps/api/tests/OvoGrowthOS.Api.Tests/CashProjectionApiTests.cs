using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class CashProjectionApiTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task SeedPeriod(WorkflowApiFactory f, decimal fee, DateOnly due, DateOnly? promisedOn, string currency = "TRY",
        MonthlyPerformanceStatus status = MonthlyPerformanceStatus.Invoiced)
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var brandId = Guid.NewGuid(); var dealId = Guid.NewGuid(); var evaluationId = Guid.NewGuid();
        db.Brands.Add(new Brand { Id = brandId, Name = "Nakit Markası", Status = BrandStatus.Active });
        db.Evaluations.Add(new BrandEvaluation { Id = evaluationId, BrandId = brandId, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" });
        db.Deals.Add(new Deal { Id = dealId, BrandId = brandId, EvaluationId = evaluationId, Name = "Anlaşma", Status = DealStatus.Active, Currency = currency, StartDate = new DateOnly(2026, 1, 1) });
        var collection = new CollectionAccount { ReceivableAmount = fee, Currency = currency, DueOn = due };
        if (promisedOn is { } promised)
            collection.Promise = new CollectionPromise
            {
                Amount = fee / 2, PromisedOn = promised, ContactNoteId = Guid.NewGuid(), OwnerId = Guid.NewGuid()
            };
        db.MonthlyPerformances.Add(new MonthlyPerformance
        {
            BrandId = brandId, DealId = dealId, Year = 2026, Month = 8, Status = status,
            NetRevenue = fee * 4, OvoFee = fee, CommissionBreakdownJson = "{}", Collection = collection
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Cash_projection_buckets_recorded_vades_and_promises_without_changing_records()
    {
        await using var f = new WorkflowApiFactory();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        await SeedPeriod(f, 1000, today.AddDays(5), today.AddDays(4));
        await SeedPeriod(f, 500, today.AddDays(-10), null);
        await SeedPeriod(f, 250, today.AddDays(400), null);
        await SeedPeriod(f, 700, today.AddDays(3), null, "USD");
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync("/api/cash-projection"));
        var root = doc.RootElement;
        var projection = root.GetProperty("projection");
        Assert.Contains("TRY", root.GetProperty("currencies").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(13, projection.GetProperty("weeks").GetInt32());
        Assert.Equal(13, projection.GetProperty("rows").GetArrayLength());
        Assert.Equal(1000, projection.GetProperty("dueTotal").GetDecimal());
        Assert.Equal(500, projection.GetProperty("promisedTotal").GetDecimal());
        Assert.Equal(500, projection.GetProperty("overdue").GetDecimal());
        Assert.Equal(250, projection.GetProperty("beyondHorizon").GetDecimal());
        Assert.Equal(0, projection.GetProperty("unknownDue").GetDecimal());
        Assert.Equal(3, projection.GetProperty("closedPeriods").GetInt32());
        Assert.Equal(4, projection.GetProperty("notes").GetArrayLength());
        var first = projection.GetProperty("rows")[0];
        Assert.Equal(1000, first.GetProperty("due").GetDecimal());
        Assert.Equal(500, first.GetProperty("promised").GetDecimal());

        using var usd = JsonDocument.Parse(await admin.GetStringAsync("/api/cash-projection?currency=USD"));
        var usdProjection = usd.RootElement.GetProperty("projection");
        Assert.Equal(700, usdProjection.GetProperty("dueTotal").GetDecimal());
        Assert.Equal(1, usdProjection.GetProperty("closedPeriods").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/cash-projection?currency=try")).StatusCode);
        using var anon = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/cash-projection")).StatusCode);
    }
}
