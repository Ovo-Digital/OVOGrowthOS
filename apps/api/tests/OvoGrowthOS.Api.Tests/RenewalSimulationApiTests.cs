using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class RenewalSimulationApiTests
{
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin")
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role));
        return c;
    }

    private static async Task<Guid> Seed(WorkflowApiFactory f)
    {
        var brand = await f.SeedAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { BrandId = brand, Name = "Yenileme simülasyonu", Status = DealStatus.Active, Currency = "TRY",
            DealType = DealType.RetainerPlusRevenueShare, MonthlyRetainer = 40_000, RevenueShareRate = 0.05m,
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 8, 31), ContractMonths = 12 };
        var locked = new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = 2026, Month = 8,
            Status = MonthlyPerformanceStatus.Locked, NetRevenue = 250_000, CommissionableRevenue = 250_000,
            ContributionBeforeOvo = 100_000, OvoFee = 52_500, OvoInternalCost = 20_000 };
        var draft = new MonthlyPerformance { BrandId = brand, DealId = deal.Id, Year = 2026, Month = 9,
            Status = MonthlyPerformanceStatus.Draft, NetRevenue = 900_000, CommissionableRevenue = 900_000,
            ContributionBeforeOvo = 300_000 };
        db.AddRange(deal, locked, draft);
        await db.SaveChangesAsync();
        return deal.Id;
    }

    [Fact]
    public async Task Simulation_returns_basis_from_the_latest_locked_period_and_never_writes()
    {
        await using var f = new WorkflowApiFactory(); var deal = await Seed(f);
        using var admin = Client(f);
        var before = await admin.GetAsync($"/api/deals/{deal}");
        var beforeBody = await before.Content.ReadAsStringAsync();

        var response = await admin.GetAsync($"/api/deals/{deal}/renewal-simulation?revenueRate=-0.5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        var basis = data.GetProperty("basis");
        Assert.Equal(2026, basis.GetProperty("year").GetInt32());
        Assert.Equal(8, basis.GetProperty("month").GetInt32());
        Assert.Equal(250_000m, basis.GetProperty("commissionableRevenue").GetDecimal());

        var simulation = data.GetProperty("simulation");
        Assert.Equal(125_000m, simulation.GetProperty("simRevenue").GetDecimal());
        Assert.Equal(46_250m, simulation.GetProperty("baseOvoFee").GetDecimal() + simulation.GetProperty("feeDelta").GetDecimal());
        Assert.Equal(-6_250m, simulation.GetProperty("feeDelta").GetDecimal());
        Assert.Equal(52_500m, simulation.GetProperty("baseOvoFee").GetDecimal());
        Assert.Equal(3_750m, simulation.GetProperty("simBrandContribution").GetDecimal());
        Assert.Equal(0.05m, simulation.GetProperty("simulatedShareRate").GetDecimal());
        Assert.Equal(40_000m, simulation.GetProperty("simulatedRetainer").GetDecimal());
        Assert.True(simulation.GetProperty("retainerApplied").GetBoolean());
        var notes = data.GetProperty("notes").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        Assert.Contains(notes, x => x.Contains("kaydetmez"));

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Deals.AsNoTracking().SingleAsync(x => x.Id == deal);
            var auditCount = await db.AuditRecords.CountAsync();
            var after = await admin.GetAsync($"/api/deals/{deal}"); var afterBody = await after.Content.ReadAsStringAsync();
            Assert.Equal(beforeBody, afterBody);
            Assert.Equal(40_000m, stored.MonthlyRetainer);
            Assert.Equal(0.05m, stored.RevenueShareRate);
            Assert.Equal(0, await db.AuditRecords.CountAsync() - auditCount);
        }
    }

    [Fact]
    public async Task Draft_periods_are_ignored_and_missing_periods_yield_zero_basis_notes()
    {
        await using var f = new WorkflowApiFactory(); var deal = await Seed(f);
        using var admin = Client(f);
        var data = JsonSerializer.Deserialize<JsonElement>(
            await (await admin.GetAsync($"/api/deals/{deal}/renewal-simulation")).Content.ReadAsStringAsync());
        Assert.Equal(250_000m, data.GetProperty("simulation").GetProperty("baseRevenue").GetDecimal());

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var solo = new Brand { Name = "Dönemsiz Marka" };
        var only = new Deal { BrandId = solo.Id, Name = "Dönemsiz", Status = DealStatus.Active, Currency = "TRY",
            MonthlyRetainer = 0, RevenueShareRate = 0, StartDate = new DateOnly(2026, 1, 1) };
        db.AddRange(solo, only); await db.SaveChangesAsync();
        var empty = JsonSerializer.Deserialize<JsonElement>(
            await (await admin.GetAsync($"/api/deals/{only.Id}/renewal-simulation")).Content.ReadAsStringAsync());
        Assert.Equal(0, empty.GetProperty("simulation").GetProperty("baseRevenue").GetDecimal());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("basis").ValueKind);
        var notes = empty.GetProperty("notes").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        Assert.Contains(notes, x => x.Contains("kilitlenmiş dönem yok"));
    }

    [Fact]
    public async Task Out_of_range_values_are_rejected_with_turkish_messages_and_operations_write_gates_access()
    {
        await using var f = new WorkflowApiFactory(); var deal = await Seed(f);
        using var analyst = Client(f, "Analyst"); using var admin = Client(f);

        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync($"/api/deals/{deal}/renewal-simulation")).StatusCode);

        var revenue = await admin.GetAsync($"/api/deals/{deal}/renewal-simulation?revenueRate=-1");
        Assert.Equal(HttpStatusCode.BadRequest, revenue.StatusCode);
        var message = JsonSerializer.Deserialize<JsonElement>(await revenue.Content.ReadAsStringAsync()).GetProperty("error").GetString();
        Assert.Contains("-%90", message);

        var share = await admin.GetAsync($"/api/deals/{deal}/renewal-simulation?sharePoints=50");
        Assert.Equal(HttpStatusCode.BadRequest, share.StatusCode);
        message = JsonSerializer.Deserialize<JsonElement>(await share.Content.ReadAsStringAsync()).GetProperty("error").GetString();
        Assert.Contains("+20 puan", message);

        var retainer = await admin.GetAsync($"/api/deals/{deal}/renewal-simulation?retainerChange=-99999");
        Assert.Equal(HttpStatusCode.BadRequest, retainer.StatusCode);
        message = JsonSerializer.Deserialize<JsonElement>(await retainer.Content.ReadAsStringAsync()).GetProperty("error").GetString();
        Assert.Contains("sıfırın altına", message);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/deals/{Guid.NewGuid()}/renewal-simulation")).StatusCode);
    }
}
