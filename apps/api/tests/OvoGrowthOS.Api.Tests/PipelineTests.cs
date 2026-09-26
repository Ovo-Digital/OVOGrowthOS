using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class PipelineTests
{
    private static readonly Guid Admin = WorkflowApiFactory.AccountId("admin@ovo.test");
    private static HttpClient Client(WorkflowApiFactory f, string role = "Admin")
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowApiFactory.Token($"{role.ToLowerInvariant()}@ovo.test", role));
        return c;
    }
    private static FollowUpRequest Follow(LeadStage stage, int revision, LeadSource? source = null, string? sourceNote = null) =>
        new(Admin, stage, "", null, "Markayı arayın", revision, source, sourceNote);

    [Fact]
    public async Task Stage_history_starts_unmeasured_and_only_real_changes_measure_waiting_time()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); using var admin = Client(f);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", Follow(LeadStage.New, 0))).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.BrandStageHistories.SingleAsync(x => x.BrandId == brand);
            Assert.False(row.EntryKnown); Assert.Null(row.ExitedAt);
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", Follow(LeadStage.Contacted, 1))).StatusCode);

        var history = await admin.GetFromJsonAsync<JsonElement>($"/api/brands/{brand}/stage-history");
        var items = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var measured = items.Single(x => x.GetProperty("entryKnown").GetBoolean());
        var unmeasured = items.Single(x => !x.GetProperty("entryKnown").GetBoolean());
        Assert.Equal("Contacted", measured.GetProperty("stage").GetString());
        Assert.Equal(0, measured.GetProperty("days").GetInt32());
        Assert.Equal("New", unmeasured.GetProperty("stage").GetString());
        Assert.Equal(JsonValueKind.Null, unmeasured.GetProperty("days").ValueKind);
        Assert.Equal("admin@ovo.test", measured.GetProperty("enteredBy").GetString());

        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/pipeline/summary");
        var newStage = summary.GetProperty("stages").EnumerateArray().Single(x => x.GetProperty("stage").GetString() == "New");
        Assert.Equal(0, newStage.GetProperty("open").GetInt32());
        var contacted = summary.GetProperty("stages").EnumerateArray().Single(x => x.GetProperty("stage").GetString() == "Contacted");
        Assert.Equal(1, contacted.GetProperty("known").GetInt32());
        Assert.Equal(0, contacted.GetProperty("unknown").GetInt32());
    }

    [Fact]
    public async Task Stage_change_never_changes_commercial_state_or_creates_a_deal()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); using var admin = Client(f);
        (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", Follow(LeadStage.New, 0))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", Follow(LeadStage.ProposalFollowUp, 1))).EnsureSuccessStatusCode();

        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(BrandStatus.Lead, (await db.Brands.SingleAsync(x => x.Id == brand)).Status);
        Assert.Equal(0, await db.Deals.CountAsync());
    }

    [Fact]
    public async Task Summary_counts_a_brand_once_and_reports_unmeasured_leads_separately()
    {
        await using var f = new WorkflowApiFactory(); var measured = await f.SeedAsync(); using var admin = Client(f);
        (await admin.PutAsJsonAsync($"/api/brands/{measured}/follow-up", Follow(LeadStage.New, 0))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/brands/{measured}/follow-up", Follow(LeadStage.ProposalFollowUp, 1))).EnsureSuccessStatusCode();

        Guid convertedBrand, unmeasuredNoDeal;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            convertedBrand = db.Brands.Add(new Brand { Name = "Ölçüm başlangıcı öncesi anlaşma", Economics = new BrandEconomics() }).Entity.Id;
            unmeasuredNoDeal = db.Brands.Add(new Brand { Name = "Henüz ölçülmeyen marka", Economics = new BrandEconomics() }).Entity.Id;
            db.Deals.Add(new Deal { BrandId = measured, Name = "Ölçümlü anlaşma", Status = DealStatus.Proposed });
            db.Deals.Add(new Deal { BrandId = convertedBrand, Name = "Ölçüm öncesi anlaşma", Status = DealStatus.Proposed });
            await db.SaveChangesAsync();
        }

        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/pipeline/summary");
        Assert.Equal(3, summary.GetProperty("open").GetInt32());
        Assert.Equal(0, summary.GetProperty("lost").GetInt32());
        var conversion = summary.GetProperty("conversion");
        Assert.Equal(1, conversion.GetProperty("reached").GetInt32());
        Assert.Equal(1, conversion.GetProperty("converted").GetInt32());
        Assert.Equal(100.0m, conversion.GetProperty("rate").GetDecimal());
        Assert.Equal(1, conversion.GetProperty("beforeMeasurement").GetInt32());
        Assert.Equal(1, conversion.GetProperty("notMeasured").GetInt32());
        var newStage = summary.GetProperty("stages").EnumerateArray().Single(x => x.GetProperty("stage").GetString() == "New");
        Assert.Equal(2, newStage.GetProperty("open").GetInt32());
        Assert.Equal(2, newStage.GetProperty("unknown").GetInt32());
        Assert.Equal(JsonValueKind.Null, newStage.GetProperty("averageDays").ValueKind);
        var proposal = summary.GetProperty("stages").EnumerateArray().Single(x => x.GetProperty("stage").GetString() == "ProposalFollowUp");
        Assert.Equal(1, proposal.GetProperty("open").GetInt32());
        Assert.Equal(1, proposal.GetProperty("known").GetInt32());
        Assert.Contains(summary.GetProperty("notes").EnumerateArray(), x => x.GetString()!.Contains("satış garantisi"));
        Assert.DoesNotContain("hourlyCost", summary.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Loss_is_recorded_with_a_reason_and_refused_for_deals_duplicates_and_unauthorized_users()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync();
        using var admin = Client(f); using var analyst = Client(f, "Analyst");
        Guid withDeal;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            withDeal = db.Brands.Add(new Brand { Name = "Anlaşmalı kayıp denemesi", Economics = new BrandEconomics() }).Entity.Id;
            db.Deals.Add(new Deal { BrandId = withDeal, Name = "Var olan anlaşma", Status = DealStatus.Active });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsJsonAsync($"/api/brands/{brand}/pipeline/loss", new PipelineLossRequest("Bütçe yetmedi"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync("/api/pipeline/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/brands/{brand}/pipeline/loss", new PipelineLossRequest(""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/brands/{brand}/pipeline/loss", new PipelineLossRequest(new string('x', 1001)))).StatusCode);

        var recorded = await admin.PostAsJsonAsync($"/api/brands/{brand}/pipeline/loss", new PipelineLossRequest("Bütçe yetmedi"));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var body = await recorded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TeamWork.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd"), body.GetProperty("lostOn").GetString());
        Assert.Equal("Bütçe yetmedi", body.GetProperty("lostReason").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/brands/{brand}/pipeline/loss", new PipelineLossRequest("Tekrar deneme"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/brands/{withDeal}/pipeline/loss", new PipelineLossRequest("Bütçe yetmedi"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/brands/{withDeal}/pipeline/loss/cancel", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/brands/{brand}/pipeline/loss/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/brands/{brand}/pipeline/loss", new PipelineLossRequest("Yeniden değerlendirme"))).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var follow = await db.BrandFollowUps.SingleAsync(x => x.BrandId == brand);
            Assert.Equal("Yeniden değerlendirme", follow.LostReason);
            Assert.Equal("admin@ovo.test", follow.LostBy);
            Assert.True(await db.AuditRecords.AnyAsync(x => x.Action == "BrandPipelineLossRecorded"));
            Assert.True(await db.AuditRecords.AnyAsync(x => x.Action == "BrandPipelineLossCancelled"));
        }

        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/pipeline/summary");
        Assert.Equal(1, summary.GetProperty("open").GetInt32());
        Assert.Equal(1, summary.GetProperty("lost").GetInt32());
        Assert.Equal("Yeniden değerlendirme", summary.GetProperty("losses")[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Source_channel_is_kept_across_saves_without_touching_commercial_state()
    {
        await using var f = new WorkflowApiFactory(); var brand = await f.SeedAsync(); using var admin = Client(f);
        var withSource = Follow(LeadStage.Contacted, 0, LeadSource.Referral, "Öneren müşteri");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", withSource)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", Follow(LeadStage.ProposalFollowUp, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/brands/{brand}/follow-up", withSource with { Revision = 2, Stage = LeadStage.ProposalFollowUp, SourceChannel = (LeadSource?)99 })).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var follow = await db.BrandFollowUps.SingleAsync(x => x.BrandId == brand);
            Assert.Equal(LeadSource.Referral, follow.SourceChannel);
            Assert.Equal("Öneren müşteri", follow.SourceNote);
            Assert.Null(follow.LostOn);
            Assert.Equal(BrandStatus.Lead, (await db.Brands.SingleAsync(x => x.Id == brand)).Status);
        }

        var leads = await admin.GetFromJsonAsync<JsonElement>("/api/lead-follow-ups");
        var lead = leads.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetString() == brand.ToString());
        Assert.True(lead.GetProperty("stageEntryKnown").GetBoolean());
        Assert.Equal(JsonValueKind.String, lead.GetProperty("stageEnteredAt").ValueKind);

        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/pipeline/summary");
        var sources = summary.GetProperty("sources").EnumerateArray().ToList();
        Assert.Equal(1, sources.Single(x => x.GetProperty("channel").GetString() == "Referral").GetProperty("count").GetInt32());
        Assert.Equal("Referans / tavsiye", sources.Single(x => x.GetProperty("channel").GetString() == "Referral").GetProperty("label").GetString());
    }
}
