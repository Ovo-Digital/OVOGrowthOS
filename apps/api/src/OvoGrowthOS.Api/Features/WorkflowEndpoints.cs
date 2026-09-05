using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static class WorkflowEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };

    public static WebApplication MapWorkflowEndpoints(this WebApplication app)
    {
        MapBrands(app); MapEvaluations(app); MapRules(app); MapScenarios(app); MapDeals(app);
        MapPerformance(app); MapDashboard(app); MapSettingsAndAudit(app);
        return app;
    }

    private static void MapBrands(WebApplication app)
    {
        var group = app.MapGroup("/api/brands").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 20, string? search = null) =>
        {
            var query = db.Brands.AsNoTracking().Include(x => x.Economics).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search}%"));
            var size = Math.Clamp(pageSize, 1, 100); var total = await query.CountAsync();
            return Results.Ok(new { items = await query.OrderBy(x => x.Name).Skip((Math.Max(page, 1) - 1) * size).Take(size).ToListAsync(), total, page, pageSize = size });
        }).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.Brands.AsNoTracking().Include(x => x.Economics).Include(x => x.Evaluations).Include(x => x.Deals)
                .FirstOrDefaultAsync(x => x.Id == id) is { } brand ? Results.Ok(brand) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/", async (Brand brand, AppDbContext db, ClaimsPrincipal user) =>
        {
            brand.UpdatedAt = DateTimeOffset.UtcNow; db.Brands.Add(brand); Audit(db, user, "BrandCreated", "Brand", brand.Id, null, brand);
            await db.SaveChangesAsync(); return Results.Created($"/api/brands/{brand.Id}", brand);
        }).RequireAuthorization("OperationsWrite");
    }

    private static void MapEvaluations(WebApplication app)
    {
        var group = app.MapGroup("/api/evaluations").RequireAuthorization("EvaluationWrite");
        group.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Evaluations.AsNoTracking().Include(x => x.Brand)
            .OrderByDescending(x => x.UpdatedAt).ToListAsync())).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.Evaluations.AsNoTracking().Include(x => x.Brand)!.ThenInclude(x => x!.Economics).Include(x => x.Conditions)
                .Include(x => x.Scenarios).FirstOrDefaultAsync(x => x.Id == id) is { } value ? Results.Ok(value) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/", async (EvaluationDraftRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brand = await db.Brands.Include(x => x.Economics).SingleOrDefaultAsync(x => x.Id == request.BrandId);
            if (brand is null) return Results.NotFound();
            var evaluation = new BrandEvaluation { BrandId = request.BrandId, Brand = brand, CreatedBy = User(user), Status = EvaluationStatus.Draft };
            Apply(evaluation, request); db.Evaluations.Add(evaluation); Audit(db, user, "EvaluationCreated", "Evaluation", evaluation.Id, null, evaluation);
            await db.SaveChangesAsync(); return Results.Created($"/api/evaluations/{evaluation.Id}", evaluation);
        });
        group.MapPut("/{id:guid}", async (Guid id, EvaluationDraftRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var evaluation = await db.Evaluations.Include(x => x.Brand)!.ThenInclude(x => x!.Economics).FirstOrDefaultAsync(x => x.Id == id);
            if (evaluation is null) return Results.NotFound();
            if (evaluation.Status is EvaluationStatus.Analyzed or EvaluationStatus.Approved or EvaluationStatus.Rejected or EvaluationStatus.Archived) return Results.Conflict(new { error = "Analiz edilmiş değerlendirmeler değiştirilemez." });
            var old = JsonSerializer.Serialize(evaluation, Json); Apply(evaluation, request); Audit(db, user, "EvaluationSaved", "Evaluation", id, old, evaluation);
            await db.SaveChangesAsync(); return Results.Ok(evaluation);
        });
        group.MapPost("/{id:guid}/analyze", Analyze);
        group.MapPost("/{id:guid}/approve", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var value = await db.Evaluations.FindAsync(id); if (value is null) return Results.NotFound();
            if (value.Status != EvaluationStatus.Analyzed) return Results.Conflict(new { error = "Yalnızca analizi tamamlanan değerlendirmeler onaylanabilir." });
            value.Status = EvaluationStatus.Approved; value.UpdatedAt = DateTimeOffset.UtcNow; Audit(db, user, "EvaluationApproved", "Evaluation", id, null, value);
            await db.SaveChangesAsync(); return Results.Ok(value);
        }).RequireAuthorization("OperationsWrite");
    }

    private static async Task<IResult> Analyze(Guid id, AppDbContext db, ClaimsPrincipal user)
    {
        var evaluation = await db.Evaluations.Include(x => x.Brand)!.ThenInclude(x => x!.Economics).Include(x => x.Conditions).FirstOrDefaultAsync(x => x.Id == id);
        if (evaluation?.Brand?.Economics is null) return Results.NotFound();
        if (evaluation.Status is EvaluationStatus.Approved or EvaluationStatus.Rejected or EvaluationStatus.Archived) return Results.Conflict(new { error = "Geçmiş değerlendirmeler değiştirilemez." });
        var settings = await db.GeneralSettings.AsNoTracking().SingleAsync();
        var ruleSet = await db.RuleSets.AsNoTracking().Include(x => x.Rules).SingleAsync(x => x.Id == settings.DefaultRuleSetId && x.Status == RuleSetStatus.Published);
        var result = DealRecommendationEngine.Recommend(evaluation.Brand.Economics, evaluation, ruleSet, settings);
        evaluation.PartnershipScore = result.PartnershipScore; evaluation.DataConfidenceScore = result.DataConfidenceScore;
        evaluation.Decision = result.Decision; evaluation.RecommendedDealType = result.RecommendedDealType;
        evaluation.RecommendedContractMonths = result.RecommendedContractMonths; evaluation.RecommendedMinimumFee = result.RecommendedMinimumMonthlyFee;
        evaluation.RecommendedSetupInvestment = result.RecommendedSetupInvestment; evaluation.RecommendedAdSpend = result.RecommendedAdSpend;
        evaluation.RecommendedTargetMer = result.RecommendedTargetMer; evaluation.RecommendedTargetContributionMargin = result.RecommendedTargetBrandContributionMargin;
        evaluation.RuleSetId = ruleSet.Id; evaluation.RuleSetVersion = ruleSet.Version;
        evaluation.RuleSnapshotJson = JsonSerializer.Serialize(ruleSet, Json);
        evaluation.InputSnapshotJson = JsonSerializer.Serialize(new { evaluation.Brand.Economics, evaluation.ProductMarketFit, evaluation.GrowthPotential, evaluation.OperationalReadiness, evaluation.CreativeCapability, evaluation.FounderCooperation, evaluation.DataMaturity, evaluation.InternalMonthlyCost, evaluation.SetupInvestment }, Json);
        evaluation.CalculationSnapshotJson = JsonSerializer.Serialize(new { result.PartnershipScore, result.DataConfidenceScore, result.RecommendedTargetMer }, Json);
        evaluation.RecommendationSnapshotJson = JsonSerializer.Serialize(result, Json);
        evaluation.CurrentStep = 10;
        evaluation.Status = result.Decision == DecisionStatus.NeedMoreData ? EvaluationStatus.ReadyForAnalysis : EvaluationStatus.Analyzed;
        evaluation.CompletedAt = result.Decision == DecisionStatus.NeedMoreData ? null : DateTimeOffset.UtcNow; evaluation.UpdatedAt = DateTimeOffset.UtcNow;
        db.PartnershipConditions.RemoveRange(evaluation.Conditions);
        db.PartnershipConditions.AddRange(result.RequiredConditions.Select(x => new PartnershipCondition { EvaluationId = id, Code = x.Code, Title = x.Title, Description = x.Description, Required = x.Required }));
        Audit(db, user, "EvaluationAnalyzed", "Evaluation", id, null, result);
        await db.SaveChangesAsync(); return Results.Ok(result);
    }

    private static void Apply(BrandEvaluation e, EvaluationDraftRequest r)
    {
        e.CurrentStep = Math.Clamp(r.CurrentStep, 1, 10); e.Status = r.Status is EvaluationStatus.Draft ? (e.CurrentStep > 1 ? EvaluationStatus.InProgress : EvaluationStatus.Draft) : r.Status;
        e.ProductMarketFit = r.ProductMarketFit; e.GrowthPotential = r.GrowthPotential; e.OperationalReadiness = r.OperationalReadiness;
        e.CreativeCapability = r.CreativeCapability; e.FounderCooperation = r.FounderCooperation; e.DataMaturity = r.DataMaturity;
        e.InternalMonthlyCost = r.InternalMonthlyCost; e.SetupInvestment = r.SetupInvestment; e.UpdatedAt = DateTimeOffset.UtcNow;
        var x = e.Brand?.Economics ?? new BrandEconomics { BrandId = r.BrandId };
        x.AverageMonthlyRevenue = r.AverageMonthlyRevenue; x.RevenueConfidence = r.RevenueConfidence; x.GrossMarginRate = r.GrossMarginRate;
        x.GrossMarginConfidence = r.GrossMarginConfidence; x.CogsRate = r.CogsRate; x.CogsConfidence = r.CogsConfidence;
        x.AverageOrderValue = r.AverageOrderValue; x.AovConfidence = r.AovConfidence; x.ReturnRate = r.ReturnRate; x.ReturnRateConfidence = r.ReturnRateConfidence;
        x.CurrentAdSpend = r.CurrentAdSpend; x.AdSpendConfidence = r.AdSpendConfidence; x.CurrentCac = r.CurrentCac; x.CacConfidence = r.CacConfidence;
        x.AverageCustomerLtv = r.AverageCustomerLtv; x.LtvConfidence = r.LtvConfidence; x.StockCoverageDays = r.StockCoverageDays;
        x.StockCoverageConfidence = r.StockCoverageConfidence; x.MonthlyOrders = r.MonthlyOrders; x.MonthlySessions = r.MonthlySessions;
        x.NewCustomers = r.NewCustomers; x.ReturningCustomers = r.ReturningCustomers; x.VariableCostRate = r.VariableCostRate;
        if (e.Brand is not null) { e.Brand.Economics = x; e.Brand.UpdatedAt = DateTimeOffset.UtcNow; }
    }

    private static void MapRules(WebApplication app)
    {
        var group = app.MapGroup("/api/rulesets").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db) => Results.Ok(await db.RuleSets.AsNoTracking().Select(x => new { x.Id, x.Name, x.Version, x.Status, x.PublishedAt, ruleCount = x.Rules.Count }).OrderByDescending(x => x.Version).ToListAsync()));
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) => await db.RuleSets.AsNoTracking().Include(x => x.Rules).FirstOrDefaultAsync(x => x.Id == id) is { } x ? Results.Ok(x) : Results.NotFound());
        group.MapPost("/", async (RuleSetRequest request, AppDbContext db) => { var x = new RuleSet { Name = request.Name, Description = request.Description }; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/rulesets/{x.Id}", x); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/clone", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var source = await db.RuleSets.Include(x => x.Rules).SingleOrDefaultAsync(x => x.Id == id); if (source is null) return Results.NotFound(); var clone = RuleSetVersioning.Clone(source); db.Add(clone); Audit(db, user, "RuleSetCloned", "RuleSet", clone.Id, source, clone); await db.SaveChangesAsync(); return Results.Ok(clone); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/rules", async (Guid id, RuleRequest request, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(id); if (set is null) return Results.NotFound(); try { RuleSetVersioning.EnsureEditable(set); } catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); } var rule = ToRule(id, request); db.Add(rule); Audit(db, user, "RuleChanged", "RuleSet", id, null, rule); await db.SaveChangesAsync(); return Results.Ok(rule); }).RequireAuthorization("AdminOnly");
        group.MapPut("/{setId:guid}/rules/{ruleId:guid}", async (Guid setId, Guid ruleId, RuleRequest request, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(setId); var rule = await db.Rules.FindAsync(ruleId); if (set is null || rule is null || rule.RuleSetId != setId) return Results.NotFound(); try { RuleSetVersioning.EnsureEditable(set); } catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); } var old = JsonSerializer.Serialize(rule, Json); Copy(rule, request); Audit(db, user, "RuleChanged", "Rule", ruleId, old, rule); await db.SaveChangesAsync(); return Results.Ok(rule); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/publish", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(id); if (set is null) return Results.NotFound(); if (set.Status != RuleSetStatus.Draft) return Results.Conflict(new { error = "Yalnızca taslak kural setleri yayımlanabilir." }); foreach (var active in await db.RuleSets.Where(x => x.Status == RuleSetStatus.Published).ToListAsync()) { active.Status = RuleSetStatus.Archived; active.EffectiveTo = DateTimeOffset.UtcNow; } set.Status = RuleSetStatus.Published; set.PublishedAt = DateTimeOffset.UtcNow; var settings = await db.GeneralSettings.SingleAsync(); settings.DefaultRuleSetId = set.Id; Audit(db, user, "RuleSetPublished", "RuleSet", id, null, set); await db.SaveChangesAsync(); return Results.Ok(set); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/archive", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(id); if (set is null) return Results.NotFound(); var settings = await db.GeneralSettings.AsNoTracking().SingleAsync(); if (set.Status == RuleSetStatus.Published && settings.DefaultRuleSetId == id) return Results.Conflict(new { error = "Etkin kural setini arşivlemeden önce yerine kullanılacak kural setini yayımlayın." }); set.Status = RuleSetStatus.Archived; set.EffectiveTo = DateTimeOffset.UtcNow; Audit(db, user, "RuleSetArchived", "RuleSet", id, null, set); await db.SaveChangesAsync(); return Results.Ok(set); }).RequireAuthorization("AdminOnly");
    }

    private static Rule ToRule(Guid id, RuleRequest r) { var x = new Rule { RuleSetId = id, Name = r.Name }; Copy(x, r); return x; }
    private static void Copy(Rule x, RuleRequest r) { x.Name = r.Name; x.Description = r.Description; x.Category = r.Category; x.Field = r.Field; x.Operator = r.Operator; x.Value = r.Value; x.SecondaryValue = r.SecondaryValue; x.Severity = r.Severity; x.Weight = r.Weight; x.Enabled = r.Enabled; x.RecommendationEffect = r.RecommendationEffect; }

    private static void MapScenarios(WebApplication app)
    {
        var group = app.MapGroup("/api/evaluations/{evaluationId:guid}/scenarios").RequireAuthorization("EvaluationWrite");
        group.MapGet("/", async (Guid evaluationId, AppDbContext db) => Results.Ok(await db.Scenarios.AsNoTracking().Where(x => x.EvaluationId == evaluationId).OrderBy(x => x.Name).ToListAsync())).RequireAuthorization("ReadAccess");
        group.MapPost("/calculate", (Guid evaluationId, ScenarioRequest request) => Results.Ok(ScenarioCalculator.Calculate(ToScenario(evaluationId, request))));
        group.MapPost("/", async (Guid evaluationId, ScenarioRequest request, AppDbContext db) => { if (!await db.Evaluations.AnyAsync(x => x.Id == evaluationId)) return Results.NotFound(); var x = ToScenario(evaluationId, request); var result = ScenarioCalculator.Calculate(x); x.ResultJson = JsonSerializer.Serialize(result, Json); db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/evaluations/{evaluationId}/scenarios/{x.Id}", new { scenario = x, result }); });
        group.MapPost("/{id:guid}/duplicate", async (Guid evaluationId, Guid id, AppDbContext db) => { var source = await db.Scenarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.EvaluationId == evaluationId); if (source is null) return Results.NotFound(); source.Id = Guid.NewGuid(); source.Name += " Copy"; source.IsPreferred = false; source.CreatedAt = source.UpdatedAt = DateTimeOffset.UtcNow; db.Add(source); await db.SaveChangesAsync(); return Results.Ok(source); });
        group.MapPatch("/{id:guid}/rename", async (Guid evaluationId, Guid id, RenameRequest request, AppDbContext db) => { var value = await db.Scenarios.SingleOrDefaultAsync(x => x.Id == id && x.EvaluationId == evaluationId); if (value is null) return Results.NotFound(); if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string,string[]>{{"name",["Ad alanı zorunludur."]}}); value.Name = request.Name.Trim(); value.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Ok(value); });
        group.MapPut("/{id:guid}/preferred", async (Guid evaluationId, Guid id, AppDbContext db) => { var items = await db.Scenarios.Where(x => x.EvaluationId == evaluationId).ToListAsync(); var selected = items.SingleOrDefault(x => x.Id == id); if (selected is null) return Results.NotFound(); items.ForEach(x => x.IsPreferred = x.Id == id); await db.SaveChangesAsync(); return Results.Ok(selected); });
        group.MapDelete("/{id:guid}", async (Guid evaluationId, Guid id, AppDbContext db) => { var value = await db.Scenarios.SingleOrDefaultAsync(x => x.Id == id && x.EvaluationId == evaluationId); if (value is null) return Results.NotFound(); db.Remove(value); await db.SaveChangesAsync(); return Results.NoContent(); });
    }
    private static Scenario ToScenario(Guid id, ScenarioRequest r) => new() { EvaluationId = id, Name = r.Name, MonthlyRevenue = r.MonthlyRevenue,
        GrossMarginRate = r.GrossMarginRate, AdSpend = r.AdSpend, ReturnRate = r.ReturnRate, AverageOrderValue = r.AverageOrderValue,
        NewCustomers = r.NewCustomers, VariableCostRate = r.VariableCostRate, OvoInternalMonthlyCost = r.OvoInternalMonthlyCost,
        MinimumMonthlyFee = r.MinimumMonthlyFee, CommissionModel = r.CommissionModel, RevenueShareRate = r.RevenueShareRate,
        MonthlyRetainer = r.MonthlyRetainer, BaselineRevenue = r.BaselineRevenue, IncrementalRate = r.IncrementalRate,
        ProfitShareRate = r.ProfitShareRate, CommissionTiersJson = JsonSerializer.Serialize(r.CommissionTiers, Json),
        TargetBrandContributionMargin = r.TargetBrandContributionMargin, SetupInvestment = r.SetupInvestment, ContractMonths = r.ContractMonths };

    private static void MapDeals(WebApplication app)
    {
        var group = app.MapGroup("/api/deals").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Deals.AsNoTracking().Include(x => x.Brand).OrderByDescending(x => x.UpdatedAt).ToListAsync())).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) => await db.Deals.AsNoTracking().Include(x => x.Brand).Include(x => x.Conditions).FirstOrDefaultAsync(x => x.Id == id) is { } x ? Results.Ok(x) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/from-evaluation/{evaluationId:guid}", async (Guid evaluationId, DealRequest request, AppDbContext db, ClaimsPrincipal user) => { var e = await db.Evaluations.Include(x => x.Conditions).SingleOrDefaultAsync(x => x.Id == evaluationId); if (e is null) return Results.NotFound(); if (e.Status != EvaluationStatus.Approved) return Results.Conflict(new { error = "Önce değerlendirmeyi onaylayın." }); var d = ToDeal(e, request); db.Add(d); Audit(db, user, "DealCreated", "Deal", d.Id, null, d); await db.SaveChangesAsync(); return Results.Created($"/api/deals/{d.Id}", d); }).RequireAuthorization("OperationsWrite");
        group.MapPost("/compare", async (DealComparisonRequest request, AppDbContext db) => { var deals = await db.Deals.AsNoTracking().Where(x => request.DealIds.Contains(x.Id)).ToListAsync(); var settings = await db.GeneralSettings.AsNoTracking().SingleAsync(); return Results.Ok(DealComparisonEngine.Compare(deals, request.Basis, settings)); }).RequireAuthorization("ReadAccess");
        group.MapPost("/{id:guid}/accept", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await ChangeDealStatus(id, DealStatus.Accepted, db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/activate", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var deal = await db.Deals.Include(x => x.Brand).SingleOrDefaultAsync(x => x.Id == id); if (deal is null) return Results.NotFound(); if (deal.Status != DealStatus.Accepted) return Results.Conflict(new { error = "Yalnızca kabul edilmiş anlaşmalar etkinleştirilebilir." }); deal.Status = DealStatus.Active; deal.StartDate ??= DateOnly.FromDateTime(DateTime.UtcNow); deal.Brand!.Status = BrandStatus.Active; Audit(db, user, "DealActivated", "Deal", id, null, deal); await db.SaveChangesAsync(); return Results.Ok(deal); }).RequireAuthorization("OperationsWrite");
    }
    public sealed record DealComparisonRequest(List<Guid> DealIds, Scenario Basis);
    private static Deal ToDeal(BrandEvaluation e, DealRequest r) => new() { BrandId = e.BrandId, EvaluationId = e.Id, Name = r.Name,
        DealType = r.DealType, ContractMonths = r.ContractMonths, BaselineRevenue = r.BaselineRevenue,
        BaselinePeriodStart = r.BaselinePeriodStart, BaselinePeriodEnd = r.BaselinePeriodEnd, BaselineCalculationMethod = r.BaselineCalculationMethod,
        MonthlyRetainer = r.MonthlyRetainer, MinimumMonthlyFee = r.MinimumMonthlyFee, RevenueShareRate = r.RevenueShareRate,
        IncrementalRate = r.IncrementalRate, ProfitShareRate = r.ProfitShareRate, CommissionTiersJson = JsonSerializer.Serialize(r.CommissionTiers, Json),
        SetupInvestment = r.SetupInvestment, EstimatedMonthlyInternalCost = r.EstimatedMonthlyInternalCost,
        EvaluationSnapshotJson = e.RecommendationSnapshotJson, RuleSnapshotJson = e.RuleSnapshotJson,
        FinancialSnapshotJson = e.CalculationSnapshotJson, CommissionSnapshotJson = JsonSerializer.Serialize(r, Json),
        ConditionsSnapshotJson = JsonSerializer.Serialize(e.Conditions, Json), Conditions = e.Conditions.Select(x => new PartnershipCondition { Code = x.Code, Title = x.Title, Description = x.Description, Required = x.Required, Status = x.Status }).ToList() };
    private static async Task<IResult> ChangeDealStatus(Guid id, DealStatus status, AppDbContext db, ClaimsPrincipal user) { var deal = await db.Deals.FindAsync(id); if (deal is null) return Results.NotFound(); if(status==DealStatus.Accepted&&!await db.Evaluations.AnyAsync(x=>x.Id==deal.EvaluationId&&x.Status==EvaluationStatus.Approved))return Results.Conflict(new{error="Anlaşmayı kabul etmeden önce değerlendirmeyi onaylayın."}); if(status==DealStatus.Accepted&&deal.Status is not (DealStatus.Draft or DealStatus.InternalReview or DealStatus.Proposed or DealStatus.Negotiation))return Results.Conflict(new{error=$"{deal.Status} durumundaki bir anlaşma kabul edilemez."}); deal.Status = status; deal.UpdatedAt = DateTimeOffset.UtcNow; Audit(db, user, "DealChanged", "Deal", id, null, deal); await db.SaveChangesAsync(); return Results.Ok(deal); }

    private static void MapPerformance(WebApplication app)
    {
        var group = app.MapGroup("/api/performance").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db) => Results.Ok(await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Adjustments).OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync())).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) => await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal).Include(x => x.Adjustments).FirstOrDefaultAsync(x => x.Id == id) is { } x ? Results.Ok(x) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/calculate", async (PerformanceRequest request, AppDbContext db) => { var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.DealId && x.Status == DealStatus.Active); if (deal is null) return Results.Conflict(new { error = "Etkin bir anlaşma gereklidir." }); var p = ToPerformance(request); MonthlyPerformanceCalculator.Calculate(p, deal); return Results.Ok(p); }).RequireAuthorization("OperationsWrite");
        group.MapPost("/", async (PerformanceRequest request, AppDbContext db, ClaimsPrincipal user) => { var deal = await db.Deals.SingleOrDefaultAsync(x => x.Id == request.DealId && x.Status == DealStatus.Active); if (deal is null) return Results.Conflict(new { error = "Etkin bir anlaşma gereklidir." }); var p = ToPerformance(request); MonthlyPerformanceCalculator.Calculate(p, deal); db.Add(p); Audit(db, user, "MonthlyPerformanceCreated", "MonthlyPerformance", p.Id, null, p); await db.SaveChangesAsync(); return Results.Created($"/api/performance/{p.Id}", p); }).RequireAuthorization("OperationsWrite");
        group.MapPut("/{id:guid}", async (Guid id, PerformanceRequest request, AppDbContext db, ClaimsPrincipal user) => { var p = await db.MonthlyPerformances.Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound(); if (!MonthlyCloseWorkflow.CanEdit(p.Status)) return Results.Conflict(new { error = "Kilitlenmiş dönemler değiştirilemez." }); Copy(p, request); MonthlyPerformanceCalculator.Calculate(p, p.Deal!); Audit(db, user, "MonthlyPerformanceChanged", "MonthlyPerformance", id, null, p); await db.SaveChangesAsync(); return Results.Ok(p); }).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/submit", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await Transition(id, MonthlyPerformanceStatus.UnderReview, "MonthlyPerformanceSubmitted", db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/approve", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await Transition(id, MonthlyPerformanceStatus.Approved, "MonthlyPerformanceApproved", db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/lock", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await Transition(id, MonthlyPerformanceStatus.Locked, "MonthlyCloseLocked", db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/unlock", async (Guid id, TransitionRequest request, AppDbContext db, ClaimsPrincipal user) => { if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string,string[]>{{"reason",["Kilidi açma nedeni zorunludur."]}}); var p = await db.MonthlyPerformances.FindAsync(id); if (p is null) return Results.NotFound(); if (p.Status < MonthlyPerformanceStatus.Locked) return Results.Conflict(); var old = p.Status; p.Status = MonthlyPerformanceStatus.Approved; Audit(db, user, "MonthlyCloseUnlocked", "MonthlyPerformance", id, old, p.Status, request.Reason); await db.SaveChangesAsync(); return Results.Ok(p); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/adjustments", async (Guid id, AdjustmentRequest request, AppDbContext db, ClaimsPrincipal user) => { var p = await db.MonthlyPerformances.Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound(); if(string.IsNullOrWhiteSpace(request.Reason))return Results.ValidationProblem(new Dictionary<string,string[]>{{"reason",["Düzeltme nedeni zorunludur."]}}); var a = new CommissionAdjustment { MonthlyPerformanceId = id, Amount = request.Amount, Reason = request.Reason.Trim(), CreatedBy = User(user) }; p.Adjustments.Add(a); db.CommissionAdjustments.Add(a); MonthlyPerformanceCalculator.Calculate(p, p.Deal!); Audit(db, user, "CommissionAdjusted", "MonthlyPerformance", id, null, a, request.Reason); await db.SaveChangesAsync(); return Results.Ok(new { adjustment = a, p.OvoFee, p.CommissionBreakdownJson }); }).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/invoice", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await CommissionTransition(id, CommissionStatus.Invoiced, MonthlyPerformanceStatus.Invoiced, "CommissionInvoiced", db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/pay", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await CommissionTransition(id, CommissionStatus.Paid, MonthlyPerformanceStatus.Paid, "CommissionPaid", db, user)).RequireAuthorization("OperationsWrite");
        app.MapGet("/api/commissions", async (AppDbContext db) => Results.Ok(await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal).Include(x => x.Adjustments).OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).Select(x => new { x.Id, x.Year, x.Month, brand = x.Brand!.Name, x.CommissionableRevenue, dealType = x.Deal!.DealType, x.Deal.MonthlyRetainer, x.Deal.MinimumMonthlyFee, x.OvoFee, effectiveRate = x.CommissionableRevenue == 0 ? 0 : x.OvoFee / x.CommissionableRevenue, x.CommissionStatus }).ToListAsync())).RequireAuthorization("ReadAccess");
    }
    private static MonthlyPerformance ToPerformance(PerformanceRequest r) { var p = new MonthlyPerformance { BrandId = r.BrandId, DealId = r.DealId, Year = r.Year, Month = r.Month }; Copy(p, r); return p; }
    private static void Copy(MonthlyPerformance p, PerformanceRequest r) { p.GrossSales=r.GrossSales;p.Vat=r.Vat;p.Refunds=r.Refunds;p.Cancellations=r.Cancellations;p.Chargebacks=r.Chargebacks;p.CustomerPaidShipping=r.CustomerPaidShipping;p.GiftCardTopups=r.GiftCardTopups;p.Orders=r.Orders;p.Sessions=r.Sessions;p.NewCustomers=r.NewCustomers;p.ReturningCustomers=r.ReturningCustomers;p.Cogs=r.Cogs;p.PaymentFees=r.PaymentFees;p.FulfillmentCosts=r.FulfillmentCosts;p.ShippingSubsidy=r.ShippingSubsidy;p.OtherVariableCosts=r.OtherVariableCosts;p.MetaSpend=r.MetaSpend;p.GoogleSpend=r.GoogleSpend;p.TikTokSpend=r.TikTokSpend;p.InfluencerSpend=r.InfluencerSpend;p.OtherAdSpend=r.OtherAdSpend; }
    private static async Task<IResult> Transition(Guid id, MonthlyPerformanceStatus target, string action, AppDbContext db, ClaimsPrincipal user) { var p = await db.MonthlyPerformances.FindAsync(id); if (p is null) return Results.NotFound(); if (!MonthlyCloseWorkflow.CanTransition(p.Status, target)) return Results.Conflict(new { error = $"Cannot move from {p.Status} to {target}." }); var old=p.Status;p.Status=target;Audit(db,user,action,"MonthlyPerformance",id,old,target);await db.SaveChangesAsync();return Results.Ok(p); }
    private static async Task<IResult> CommissionTransition(Guid id, CommissionStatus commission, MonthlyPerformanceStatus period, string action, AppDbContext db, ClaimsPrincipal user) { var p=await db.MonthlyPerformances.FindAsync(id);if(p is null)return Results.NotFound();if(!MonthlyCloseWorkflow.CanTransition(p.Status,period))return Results.Conflict(new{error=$"Cannot move from {p.Status} to {period}."});p.Status=period;p.CommissionStatus=commission;Audit(db,user,action,"MonthlyPerformance",id,null,p);await db.SaveChangesAsync();return Results.Ok(p); }

    private static void MapDashboard(WebApplication app)
    {
        app.MapGet("/api/dashboard", async (AppDbContext db) =>
        {
            var latestPeriod = await db.MonthlyPerformances.AsNoTracking().OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).Select(x => new { x.Year, x.Month }).FirstOrDefaultAsync();
            var performance = latestPeriod is null ? [] : await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand)!.ThenInclude(x => x!.Economics).Include(x => x.Deal)!.ThenInclude(x => x!.Conditions).Where(x => x.Year == latestPeriod.Year && x.Month == latestPeriod.Month).ToListAsync();
            var activeBrands = await db.Brands.CountAsync(x => x.Status == BrandStatus.Active); var netRevenue=performance.Sum(x=>x.NetRevenue);var ovoRevenue=performance.Sum(x=>x.OvoFee);var ovoProfit=performance.Sum(x=>x.OvoGrossProfit);
            var outstanding=performance.Where(x=>x.CommissionStatus!=CommissionStatus.Paid).Sum(x=>x.OvoFee);var paid=performance.Where(x=>x.CommissionStatus==CommissionStatus.Paid).Sum(x=>x.OvoFee);
            var scores=await db.Evaluations.Where(x=>x.Status==EvaluationStatus.Approved||x.Status==EvaluationStatus.Analyzed).Select(x=>x.PartnershipScore).ToListAsync();
            var history=await db.MonthlyPerformances.AsNoTracking().ToListAsync();
            var trends=history.GroupBy(x=>new{x.Year,x.Month}).OrderBy(x=>x.Key.Year).ThenBy(x=>x.Key.Month).TakeLast(12)
                .Select(x=>new{x.Key.Year,x.Key.Month,netRevenue=x.Sum(y=>y.NetRevenue),ovoRevenue=x.Sum(y=>y.OvoFee),ovoGrossProfit=x.Sum(y=>y.OvoGrossProfit),ovoMargin=FinancialCalculator.Ratio(x.Sum(y=>y.OvoGrossProfit),x.Sum(y=>y.OvoFee)),mer=FinancialCalculator.Ratio(x.Sum(y=>y.NetRevenue),x.Sum(y=>y.TotalAdSpend))}).ToList();
            var dealModelDistribution=(await db.Deals.AsNoTracking().Where(x=>x.Status==DealStatus.Active).Select(x=>x.DealType).ToListAsync()).GroupBy(x=>x).Select(x=>new{dealType=x.Key,count=x.Count()}).ToList();
            var settings=await db.GeneralSettings.AsNoTracking().SingleAsync(); var largestFee=PortfolioRiskCalculator.LargestShare(performance.Select(x=>x.OvoFee));
            return Results.Ok(new { activeBrands, portfolioNetRevenue=netRevenue, ovoMonthlyRevenue=ovoRevenue, ovoGrossProfit=ovoProfit,
                ovoGrossMargin=FinancialCalculator.Ratio(ovoProfit,ovoRevenue),outstandingCommission=outstanding,paidCommission=paid,
                setupInvestmentOutstanding=await db.Deals.Where(x=>x.Status==DealStatus.Active).SumAsync(x=>x.SetupInvestment),
                averagePartnershipScore=scores.Count==0?0:scores.Average(),portfolioMer=FinancialCalculator.Ratio(netRevenue,performance.Sum(x=>x.TotalAdSpend)),
                largestClientRevenueShare=PortfolioRiskCalculator.LargestShare(performance.Select(x=>x.NetRevenue)),largestClientOvoFeeShare=largestFee,
                top3RevenueConcentration=PortfolioRiskCalculator.TopThreeShare(performance.Select(x=>x.NetRevenue)),top3OvoRevenueConcentration=PortfolioRiskCalculator.TopThreeShare(performance.Select(x=>x.OvoFee)),
                concentrationRisk=largestFee>settings.ConcentrationRiskThreshold?"High":"Normal", period=latestPeriod,trends,dealModelDistribution,
                brands=performance.Select(x=>new{x.BrandId,name=x.Brand!.Name,x.NetRevenue,x.OvoFee,x.Mer,contributionMargin=FinancialCalculator.Ratio(x.BrandContributionProfit,x.NetRevenue),health=BrandHealth(x,history)}) });
        }).RequireAuthorization("ReadAccess");
    }

    private static string BrandHealth(MonthlyPerformance current, IReadOnlyCollection<MonthlyPerformance> history)
    {
        var prior=history.Where(x=>x.BrandId==current.BrandId&&(x.Year<current.Year||x.Year==current.Year&&x.Month<current.Month)).OrderByDescending(x=>x.Year).ThenByDescending(x=>x.Month).FirstOrDefault();
        var sharpRevenueDrop=prior is not null&&prior.NetRevenue>0&&current.NetRevenue<prior.NetRevenue*.8m;
        var pendingCondition=current.Deal?.Conditions.Any(x=>x.Required&&x.Status==ConditionStatus.Pending)==true;
        var stock=current.Brand?.Economics?.StockCoverageDays??0;
        if(current.BrandContributionProfit<0||current.Mer<2||current.ReturnRate>.25m||stock is >0 and <30||sharpRevenueDrop)return "At Risk";
        if(current.Mer<3||FinancialCalculator.Ratio(current.BrandContributionProfit,current.NetRevenue)<.15m||current.ReturnRate>.15m||stock is >=30 and <60||pendingCondition)return "Watch";
        return "Healthy";
    }

    private static void MapSettingsAndAudit(WebApplication app)
    {
        app.MapGet("/api/settings", async (AppDbContext db) => Results.Ok(await db.GeneralSettings.AsNoTracking().SingleAsync())).RequireAuthorization("ReadAccess");
        app.MapPut("/api/settings", async (SettingsRequest r, AppDbContext db, ClaimsPrincipal user) => { var x=await db.GeneralSettings.SingleAsync();var old=JsonSerializer.Serialize(x,Json);x.DefaultCurrency=r.DefaultCurrency;x.DefaultVatRate=r.DefaultVatRate;x.DefaultContractMonths=r.DefaultContractMonths;x.DefaultSetupInvestment=r.DefaultSetupInvestment;x.TargetOvoGrossMargin=r.TargetOvoGrossMargin;x.TargetBrandContributionMargin=r.TargetBrandContributionMargin;x.MinimumFeeMultiplier=r.MinimumFeeMultiplier;x.ExistingRevenueThreshold=r.ExistingRevenueThreshold;x.ConcentrationRiskThreshold=r.ConcentrationRiskThreshold;x.DefaultRuleSetId=r.DefaultRuleSetId;x.UpdatedAt=DateTimeOffset.UtcNow;Audit(db,user,"SettingsChanged","Settings",x.Id,old,x);await db.SaveChangesAsync();return Results.Ok(x); }).RequireAuthorization("AdminOnly");
        app.MapGet("/api/audit", async (AppDbContext db,string? entityType,Guid? entityId) => {var q=db.AuditRecords.AsNoTracking().AsQueryable();if(entityType is not null)q=q.Where(x=>x.EntityType==entityType);if(entityId.HasValue)q=q.Where(x=>x.EntityId==entityId.ToString());return Results.Ok(await q.OrderByDescending(x=>x.CreatedAt).Take(200).ToListAsync());}).RequireAuthorization("ReadAccess");
    }

    private static string User(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private static void Audit(AppDbContext db, ClaimsPrincipal user, string action, string entity, Guid id, object? oldValue, object? newValue, string reason="") => db.AuditRecords.Add(new AuditRecord { UserId=User(user),Action=action,EntityType=entity,EntityId=id.ToString(),OldValueJson=oldValue is null?"":oldValue is string s?s:JsonSerializer.Serialize(oldValue,Json),NewValueJson=newValue is null?"":JsonSerializer.Serialize(newValue,Json),Reason=reason });
}
