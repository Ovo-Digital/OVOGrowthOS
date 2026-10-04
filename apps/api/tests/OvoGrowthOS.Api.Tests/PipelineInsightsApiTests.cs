using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PipelineInsightsApiTests
{
    private static async Task<HttpClient> Client(WorkflowApiFactory f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = WorkflowApiFactory.TestPassword });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return c;
    }

    private static async Task Db(WorkflowApiFactory f, Func<AppDbContext, Task> action)
    {
        await using var scope = f.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<Guid> SeedBrand(WorkflowApiFactory f, string name, BrandStatus status = BrandStatus.Lead)
    {
        var brandId = Guid.NewGuid();
        await Db(f, async db =>
        {
            db.Brands.Add(new Brand
            {
                Id = brandId, Name = name, Status = status, Industry = "Spor", Website = "örnek.com",
                ContactName = "Ayşe Yılmaz", ContactEmail = "ayse@ornek.com"
            });
            await db.SaveChangesAsync();
        });
        return brandId;
    }

    private static async Task<Guid> SeedDealBrand(WorkflowApiFactory f, string name)
    {
        var brandId = Guid.NewGuid(); var dealId = Guid.NewGuid(); var evaluationId = Guid.NewGuid();
        await Db(f, async db =>
        {
            var brand = new Brand { Id = brandId, Name = name, Status = BrandStatus.Lead };
            var evaluation = new BrandEvaluation { Id = evaluationId, BrandId = brandId, Brand = brand, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test" };
            var deal = new Deal { Id = dealId, BrandId = brandId, Brand = brand, EvaluationId = evaluationId, Name = "Anlaşma", Status = DealStatus.Active, StartDate = new DateOnly(2026, 1, 1) };
            db.AddRange(brand, evaluation, deal);
            await db.SaveChangesAsync();
        });
        return brandId;
    }

    private static Task Stage(WorkflowApiFactory f, Guid brandId, LeadStage stage, int enteredDaysAgo, int? exitedDaysAgo = null) =>
        Db(f, async db =>
        {
            db.BrandStageHistories.Add(new BrandStageHistory
            {
                BrandId = brandId, Stage = stage, EntryKnown = true,
                EnteredAt = DateTimeOffset.UtcNow.AddDays(-enteredDaysAgo),
                ExitedAt = exitedDaysAgo is null ? null : DateTimeOffset.UtcNow.AddDays(-exitedDaysAgo.Value)
            });
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Loss_analysis_groups_losses_builds_funnel_and_validates_window()
    {
        await using var f = new WorkflowApiFactory();
        var lostBrand = await SeedBrand(f, "Kaybedilen Aday");
        var converted = await SeedDealBrand(f, "Dönüşen Aday");
        await Db(f, async db =>
        {
            db.BrandFollowUps.Add(new BrandFollowUp
            {
                BrandId = lostBrand, Stage = LeadStage.Contacted, SourceChannel = LeadSource.Referral,
                LostOn = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1).AddDays(4),
                LostReason = "Fiyat aralığı tutmadı"
            });
            await db.SaveChangesAsync();
        });
        await Stage(f, lostBrand, LeadStage.New, 40, 30);
        await Stage(f, lostBrand, LeadStage.Contacted, 30);
        await Stage(f, converted, LeadStage.New, 20, 15);
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync("/api/pipeline/loss-analysis?months=6"));
        var root = doc.RootElement;
        Assert.Equal(6, root.GetProperty("windowMonths").GetInt32());
        Assert.Equal(1, root.GetProperty("totalLosses").GetInt32());
        var stage = root.GetProperty("byStage").EnumerateArray().Single();
        Assert.Equal("İlk görüşme yapıldı", stage.GetProperty("label").GetString());
        Assert.Equal(100m, stage.GetProperty("share").GetDecimal());
        Assert.Contains(root.GetProperty("byMonth").EnumerateArray(), x => x.GetProperty("count").GetInt32() == 1);
        var funnel = root.GetProperty("funnel").EnumerateArray().ToList();
        var newStage = funnel.Single(x => x.GetProperty("stage").GetString() == "New");
        Assert.Equal(2, newStage.GetProperty("entered").GetInt32());
        Assert.Equal(1, newStage.GetProperty("converted").GetInt32());
        Assert.Equal(1, newStage.GetProperty("lost").GetInt32());
        Assert.Equal(50m, newStage.GetProperty("conversionRate").GetDecimal());
        Assert.Equal(3, root.GetProperty("notes").GetArrayLength());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/pipeline/loss-analysis?months=5")).StatusCode);
        using var anon = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/pipeline/loss-analysis")).StatusCode);
    }

    [Fact]
    public async Task Meeting_brief_composes_existing_records_without_changing_them()
    {
        await using var f = new WorkflowApiFactory();
        var brandId = await SeedBrand(f, "Brief Markası");
        await Db(f, async db =>
        {
            db.BrandFollowUps.Add(new BrandFollowUp
            {
                BrandId = brandId, Stage = LeadStage.MeetingPlanned, SourceChannel = LeadSource.Event,
                NextStep = "Demo görüşmesi planla", NextContactOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2)
            });
            db.BrandContactNotes.Add(new BrandContactNote
            {
                BrandId = brandId, ContactOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
                Text = "İlk görüşme yapıldı; bütçe sorulacak.", CreatedBy = "admin@ovo.test"
            });
            var evaluation = new BrandEvaluation { BrandId = brandId, Status = EvaluationStatus.Approved, Decision = DecisionStatus.Accept, CreatedBy = "test", PartnershipScore = 72 };
            db.Evaluations.Add(evaluation);
            var briefDeal = new Deal { BrandId = brandId, EvaluationId = evaluation.Id, Name = "Brief Anlaşması", Status = DealStatus.Active, StartDate = new DateOnly(2026, 1, 1) };
            db.Deals.Add(briefDeal);
            db.WorkTasks.Add(new WorkTask
            {
                BrandId = brandId, AssigneeId = WorkflowApiFactory.AccountId("admin@ovo.test"),
                Title = "Bütçe bilgisini iste", Description = "Test", DueOn = DateOnly.FromDateTime(DateTime.UtcNow),
                Kind = WorkKind.General, CreatedBy = "admin@ovo.test"
            });
            db.MonthlyPerformances.Add(new MonthlyPerformance
            {
                BrandId = brandId, DealId = briefDeal.Id, Year = 2026, Month = 9,
                Status = MonthlyPerformanceStatus.Locked,
                NetRevenue = 120000m, TotalAdSpend = 40000m, Mer = 3m
            });
            await db.SaveChangesAsync();
        });
        await Stage(f, brandId, LeadStage.New, 12, 8);
        await Stage(f, brandId, LeadStage.MeetingPlanned, 3);
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync($"/api/brands/{brandId}/meeting-brief"));
        var root = doc.RootElement;
        Assert.Equal("Brief Markası", root.GetProperty("brand").GetProperty("name").GetString());
        Assert.Equal("Spor", root.GetProperty("brand").GetProperty("industry").GetString());
        var contact = root.GetProperty("contact");
        Assert.Equal("MeetingPlanned", contact.GetProperty("stage").GetString());
        Assert.Equal("Event", contact.GetProperty("sourceChannel").GetString());
        Assert.True(contact.GetProperty("stageEntryKnown").GetBoolean());
        Assert.InRange(contact.GetProperty("stageDays").GetInt32(), 2, 3);
        Assert.Equal("Demo görüşmesi planla", contact.GetProperty("nextStep").GetString());
        Assert.Single(root.GetProperty("notes").EnumerateArray());
        Assert.Equal(72m, root.GetProperty("evaluation").GetProperty("partnershipScore").GetDecimal());
        Assert.Single(root.GetProperty("tasks").EnumerateArray());
        Assert.Equal(3m, root.GetProperty("latestPeriod").GetProperty("mer").GetDecimal());
        Assert.Contains("salt okunur", root.GetProperty("note").GetString());

        async Task<int> AuditCountAsync()
        {
            await using var scope = f.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditRecords.CountAsync();
        }

        var auditBefore = await AuditCountAsync();
        var second = await admin.GetStringAsync($"/api/brands/{brandId}/meeting-brief");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/brands/{Guid.NewGuid()}/meeting-brief")).StatusCode);
        Assert.Equal(auditBefore, await AuditCountAsync());
        Assert.Equal((await admin.GetStringAsync($"/api/brands/{brandId}/meeting-brief")).Length, second.Length);
    }

    [Fact]
    public async Task Pre_screening_lists_attention_first_and_skips_lost_and_active_brands()
    {
        await using var f = new WorkflowApiFactory();
        var incomplete = await SeedBrand(f, "Eksik Aday");
        var complete = await SeedBrand(f, "Hazır Aday");
        var lost = await SeedBrand(f, "Kayıp Aday");
        await SeedBrand(f, "Etkin Marka", BrandStatus.Active);
        await Db(f, async db =>
        {
            db.BrandFollowUps.AddRange(
                new BrandFollowUp { BrandId = complete, Stage = LeadStage.Contacted, SourceChannel = LeadSource.Referral, NextStep = "Numune gönder", OwnerId = WorkflowApiFactory.AccountId("admin@ovo.test"), LostOn = null },
                new BrandFollowUp { BrandId = lost, Stage = LeadStage.New, LostOn = TeamWork.Today(DateTimeOffset.UtcNow).AddDays(-3), LostReason = "İlgi yok" });
            db.BrandContactNotes.Add(new BrandContactNote
            {
                BrandId = complete, ContactOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2),
                Text = "Görüşme yapıldı.", CreatedBy = "admin@ovo.test"
            });
            await db.SaveChangesAsync();
        });
        await Stage(f, complete, LeadStage.Contacted, 3);
        using var admin = await Client(f);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync("/api/leads/pre-screening"));
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("total").GetInt32());
        Assert.Equal(1, root.GetProperty("ready").GetInt32());
        Assert.Equal(1, root.GetProperty("attention").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("attention", items[0].GetProperty("state").GetString());
        Assert.Equal("Eksik Aday", items[0].GetProperty("brandName").GetString());
        Assert.Contains(items[0].GetProperty("issues").EnumerateArray(), x => x.GetProperty("code").GetString() == "source");
        Assert.Contains(items[0].GetProperty("issues").EnumerateArray(), x => x.GetProperty("code").GetString() == "owner");
        Assert.Equal("ready", items[1].GetProperty("state").GetString());
        Assert.Empty(items[1].GetProperty("issues").EnumerateArray());
        Assert.Equal(3, root.GetProperty("notes").GetArrayLength());
    }
}
