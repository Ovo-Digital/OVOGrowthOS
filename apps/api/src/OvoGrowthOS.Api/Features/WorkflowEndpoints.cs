using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };

    public static WebApplication MapWorkflowEndpoints(this WebApplication app)
    {
        MapBrands(app); MapEvaluations(app); MapRules(app); MapScenarios(app); MapDeals(app); MapConditions(app); MapUsers(app);
        MapDealTemplates(app); MapDocuments(app); MapPerformance(app); MapDashboard(app); MapSearchAndTasks(app); MapSettingsAndAudit(app); MapTeamWork(app); MapCollections(app); MapOperatingCosts(app); MapBrandReports(app); MapPerformanceImports(app);
        MapCustomerPortal(app);
        return app;
    }

    private static void MapUsers(WebApplication app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization("AdminOnly");
        group.MapGet("/", async (AppDbContext db) => Results.Ok(await db.UserAccounts.AsNoTracking().Where(x => x.Role != "BrandClient").OrderBy(x => x.Name).Select(x => new { x.Id, x.Email, x.Name, x.Role, x.IsActive, x.CreatedAt }).ToListAsync()));
        group.MapPost("/", async (UserAccountRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10) return Results.ValidationProblem(new Dictionary<string,string[]> { ["password"] = ["Şifre en az 10 karakter olmalıdır."] });
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.UserAccounts.AnyAsync(x => x.Email == email)) return Results.Conflict(new { error = "Bu e-posta ile kayıtlı bir kullanıcı zaten var." });
            var account = new UserAccount { Email = request.Email.Trim().ToLowerInvariant(), Name = request.Name.Trim(), Role = request.Role, PasswordHash = JwtTokenService.HashPassword(request.Password), IsActive = request.IsActive };
            db.Add(account); Audit(db, user, "UserCreated", "UserAccount", account.Id, null, new { account.Email, account.Name, account.Role, account.IsActive }); await db.SaveChangesAsync();
            return Results.Created($"/api/users/{account.Id}", new { account.Id, account.Email, account.Name, account.Role, account.IsActive });
        }).AddEndpointFilter<ValidationFilter<UserAccountRequest>>();
        group.MapPut("/{id:guid}", async (Guid id, UserAccountRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            // Serialize account changes across API instances so two managers cannot remove the last admin concurrently.
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            if (transaction is not null) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"UserAccounts\" IN SHARE ROW EXCLUSIVE MODE");
            var actorId = Guid.Parse(user.FindFirstValue("uid")!);
            var actorVersion = int.Parse(user.FindFirstValue("session_version")!, System.Globalization.CultureInfo.InvariantCulture);
            if (!await db.UserAccounts.AnyAsync(x => x.Id == actorId && x.IsActive && x.Role == "Admin" && x.TokenVersion == actorVersion))
                return Results.Unauthorized();
            var account = await db.UserAccounts.FindAsync(id); if (account is null) return Results.NotFound();
            if (account.Role == "BrandClient") return Results.Conflict(new { error = "Müşteri hesaplarını müşteri portalı yönetiminden düzenleyin." });
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.UserAccounts.AnyAsync(x => x.Email == email && x.Id != id)) return Results.Conflict(new { error = "Bu e-posta ile kayıtlı bir kullanıcı zaten var." });
            if (account.IsActive && account.Role == "Admin" && (!request.IsActive || request.Role != "Admin") &&
                !await db.UserAccounts.AnyAsync(x => x.Id != id && x.IsActive && x.Role == "Admin"))
                return Results.Conflict(new { error = "Son etkin yöneticinin hesabı kapatılamaz veya rolü değiştirilemez. Önce başka bir yönetici oluşturun." });
            var old = JsonSerializer.Serialize(new { account.Email, account.Name, account.Role, account.IsActive }, Json);
            account.Email = email; account.Name = request.Name.Trim(); account.Role = request.Role; account.IsActive = request.IsActive;
            if (!string.IsNullOrEmpty(request.Password)) account.PasswordHash = JwtTokenService.HashPassword(request.Password);
            account.TokenVersion++; account.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, user, "UserChanged", "UserAccount", id, old, new { account.Email, account.Name, account.Role, account.IsActive });
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return Results.Ok(new { account.Id, account.Email, account.Name, account.Role, account.IsActive });
        }).AddEndpointFilter<ValidationFilter<UserAccountRequest>>();
    }

    private static void MapBrands(WebApplication app)
    {
        var group = app.MapGroup("/api/brands").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 20, string? search = null, BrandStatus? status = null, string sort = "name") =>
        {
            var query = db.Brands.AsNoTracking().Include(x => x.Economics).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search}%"));
            if (status.HasValue) query = query.Where(x => x.Status == status);
            var size = Math.Clamp(pageSize, 1, 100); var total = await query.CountAsync();
            var ordered=sort switch{"recent"=>query.OrderByDescending(x=>x.UpdatedAt),"oldest"=>query.OrderBy(x=>x.UpdatedAt),"nameDesc"=>query.OrderByDescending(x=>x.Name),_=>query.OrderBy(x=>x.Name)};
            return Results.Ok(new { items = await ordered.Skip((Math.Max(page, 1) - 1) * size).Take(size).ToListAsync(), total, page, pageSize = size });
        }).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.Brands.AsNoTracking().Include(x => x.Economics).Include(x => x.Evaluations).Include(x => x.Deals)
                .FirstOrDefaultAsync(x => x.Id == id) is { } brand ? Results.Ok(brand) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/", async (Brand brand, AppDbContext db, ClaimsPrincipal user) =>
        {
            brand.UpdatedAt = DateTimeOffset.UtcNow; db.Brands.Add(brand); Audit(db, user, "BrandCreated", "Brand", brand.Id, null, brand);
            await db.SaveChangesAsync(); return Results.Created($"/api/brands/{brand.Id}", brand);
        }).AddEndpointFilter<ValidationFilter<Brand>>().RequireAuthorization("OperationsWrite");
        group.MapPut("/{id:guid}", async (Guid id, BrandUpdateRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brand = await db.Brands.FindAsync(id); if (brand is null) return Results.NotFound();
            var old = JsonSerializer.Serialize(brand, Json);
            brand.Name=request.Name.Trim();brand.LegalName=request.LegalName.Trim();brand.Website=request.Website.Trim();brand.Country=request.Country.ToUpperInvariant();brand.Currency=request.Currency.ToUpperInvariant();
            brand.Industry=request.Industry.Trim();brand.SubIndustry=request.SubIndustry.Trim();brand.BusinessModel=request.BusinessModel;brand.Platform=request.Platform;brand.Status=request.Status;
            brand.ContactName=request.ContactName.Trim();brand.ContactEmail=request.ContactEmail.Trim();brand.ContactPhone=request.ContactPhone.Trim();brand.UpdatedAt=DateTimeOffset.UtcNow;
            Audit(db,user,"BrandChanged","Brand",id,old,brand);await db.SaveChangesAsync();return Results.Ok(brand);
        }).AddEndpointFilter<ValidationFilter<BrandUpdateRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/archive", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brand=await db.Brands.FindAsync(id);if(brand is null)return Results.NotFound();if(await db.Deals.AnyAsync(x=>x.BrandId==id&&x.Status==DealStatus.Active))return Results.Conflict(new{error="Etkin anlaşması bulunan marka arşivlenemez."});
            var old=brand.Status;brand.Status=BrandStatus.Closed;brand.UpdatedAt=DateTimeOffset.UtcNow;Audit(db,user,"BrandArchived","Brand",id,old,brand.Status);await db.SaveChangesAsync();return Results.Ok(brand);
        }).RequireAuthorization("OperationsWrite");
    }

    private static void MapEvaluations(WebApplication app)
    {
        var group = app.MapGroup("/api/evaluations").RequireAuthorization("EvaluationWrite");
        group.MapGet("/", async (AppDbContext db,int page=1,int pageSize=20,string? search=null,EvaluationStatus? status=null,string sort="recent") => {var q=db.Evaluations.AsNoTracking().Include(x=>x.Brand).AsQueryable();if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>EF.Functions.ILike(x.Brand!.Name,$"%{search}%"));if(status.HasValue)q=q.Where(x=>x.Status==status);var ordered=sort switch{"oldest"=>q.OrderBy(x=>x.UpdatedAt),"name"=>q.OrderBy(x=>x.Brand!.Name),"nameDesc"=>q.OrderByDescending(x=>x.Brand!.Name),_=>q.OrderByDescending(x=>x.UpdatedAt)};return Results.Ok(await Page(ordered,page,pageSize));}).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.Evaluations.AsNoTracking().Include(x => x.Brand)!.ThenInclude(x => x!.Economics).Include(x => x.Conditions)
                .Include(x => x.Scenarios).FirstOrDefaultAsync(x => x.Id == id) is { } value ? Results.Ok(value) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/", async (EvaluationDraftRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brand = await db.Brands.Include(x => x.Economics).SingleOrDefaultAsync(x => x.Id == request.BrandId);
            if (brand is null) return Results.NotFound();
            if (await db.Evaluations.AnyAsync(x => x.BrandId == request.BrandId &&
                (x.Status == EvaluationStatus.Draft || x.Status == EvaluationStatus.InProgress || x.Status == EvaluationStatus.ReadyForAnalysis)))
                return Results.Conflict(new { error = "Bu marka için devam eden bir değerlendirme var. Önce mevcut değerlendirmeyi tamamlayın." });
            var evaluation = new BrandEvaluation { BrandId = request.BrandId, Brand = brand, CreatedBy = User(user), Status = EvaluationStatus.Draft };
            Apply(evaluation, request); db.Evaluations.Add(evaluation); Audit(db, user, "EvaluationCreated", "Evaluation", evaluation.Id, null, evaluation);
            await db.SaveChangesAsync(); return Results.Created($"/api/evaluations/{evaluation.Id}", evaluation);
        }).AddEndpointFilter<ValidationFilter<EvaluationDraftRequest>>();
        group.MapPut("/{id:guid}", async (Guid id, EvaluationDraftRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var evaluation = await db.Evaluations.Include(x => x.Brand)!.ThenInclude(x => x!.Economics).FirstOrDefaultAsync(x => x.Id == id);
            if (evaluation is null) return Results.NotFound();
            if (evaluation.BrandId != request.BrandId) return Results.Conflict(new { error = "Değerlendirmenin markası sonradan değiştirilemez." });
            if (evaluation.Status is EvaluationStatus.Analyzed or EvaluationStatus.Approved or EvaluationStatus.Rejected or EvaluationStatus.Archived) return Results.Conflict(new { error = "Analiz edilmiş değerlendirmeler değiştirilemez." });
            var old = JsonSerializer.Serialize(evaluation, Json); Apply(evaluation, request); Audit(db, user, "EvaluationSaved", "Evaluation", id, old, evaluation);
            await db.SaveChangesAsync(); return Results.Ok(evaluation);
        }).AddEndpointFilter<ValidationFilter<EvaluationDraftRequest>>();
        group.MapPost("/{id:guid}/analyze", Analyze);
        group.MapPost("/{id:guid}/approve", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var value = await db.Evaluations.FindAsync(id); if (value is null) return Results.NotFound();
            if (value.Status != EvaluationStatus.Analyzed || value.Decision is not (DecisionStatus.Accept or DecisionStatus.ConditionalAccept)) return Results.Conflict(new { error = "Yalnızca kabul veya koşullu kabul kararı verilen değerlendirmeler onaylanabilir." });
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
        evaluation.Status = result.Decision switch { DecisionStatus.NeedMoreData => EvaluationStatus.ReadyForAnalysis, DecisionStatus.Reject => EvaluationStatus.Rejected, _ => EvaluationStatus.Analyzed };
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
        group.MapPost("/", async (RuleSetRequest request, AppDbContext db) => { var x = new RuleSet { Name = request.Name.Trim(), Description = request.Description }; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/rulesets/{x.Id}", x); }).AddEndpointFilter<ValidationFilter<RuleSetRequest>>().RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/clone", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var source = await db.RuleSets.Include(x => x.Rules).SingleOrDefaultAsync(x => x.Id == id); if (source is null) return Results.NotFound(); var clone = RuleSetVersioning.Clone(source); db.Add(clone); Audit(db, user, "RuleSetCloned", "RuleSet", clone.Id, source, clone); await db.SaveChangesAsync(); return Results.Ok(clone); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/rules", async (Guid id, RuleRequest request, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(id); if (set is null) return Results.NotFound(); try { RuleSetVersioning.EnsureEditable(set); } catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); } var rule = ToRule(id, request); db.Add(rule); Audit(db, user, "RuleChanged", "RuleSet", id, null, rule); await db.SaveChangesAsync(); return Results.Ok(rule); }).AddEndpointFilter<ValidationFilter<RuleRequest>>().RequireAuthorization("AdminOnly");
        group.MapPut("/{setId:guid}/rules/{ruleId:guid}", async (Guid setId, Guid ruleId, RuleRequest request, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(setId); var rule = await db.Rules.FindAsync(ruleId); if (set is null || rule is null || rule.RuleSetId != setId) return Results.NotFound(); try { RuleSetVersioning.EnsureEditable(set); } catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); } var old = JsonSerializer.Serialize(rule, Json); Copy(rule, request); Audit(db, user, "RuleChanged", "Rule", ruleId, old, rule); await db.SaveChangesAsync(); return Results.Ok(rule); }).AddEndpointFilter<ValidationFilter<RuleRequest>>().RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/publish", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(id); if (set is null) return Results.NotFound(); if (set.Status != RuleSetStatus.Draft) return Results.Conflict(new { error = "Yalnızca taslak kural setleri yayımlanabilir." }); foreach (var active in await db.RuleSets.Where(x => x.Status == RuleSetStatus.Published).ToListAsync()) { active.Status = RuleSetStatus.Archived; active.EffectiveTo = DateTimeOffset.UtcNow; } set.Status = RuleSetStatus.Published; set.PublishedAt = DateTimeOffset.UtcNow; var settings = await db.GeneralSettings.SingleAsync(); settings.DefaultRuleSetId = set.Id; Audit(db, user, "RuleSetPublished", "RuleSet", id, null, set); await db.SaveChangesAsync(); return Results.Ok(set); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/archive", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var set = await db.RuleSets.FindAsync(id); if (set is null) return Results.NotFound(); var settings = await db.GeneralSettings.AsNoTracking().SingleAsync(); if (set.Status == RuleSetStatus.Published && settings.DefaultRuleSetId == id) return Results.Conflict(new { error = "Etkin kural setini arşivlemeden önce yerine kullanılacak kural setini yayımlayın." }); set.Status = RuleSetStatus.Archived; set.EffectiveTo = DateTimeOffset.UtcNow; Audit(db, user, "RuleSetArchived", "RuleSet", id, null, set); await db.SaveChangesAsync(); return Results.Ok(set); }).RequireAuthorization("AdminOnly");
    }

    private static Rule ToRule(Guid id, RuleRequest r) { var x = new Rule { RuleSetId = id, Name = r.Name }; Copy(x, r); return x; }
    private static void Copy(Rule x, RuleRequest r) { x.Name = r.Name; x.Description = r.Description; x.Category = r.Category; x.Field = r.Field; x.Operator = r.Operator; x.Value = r.Value; x.SecondaryValue = r.SecondaryValue; x.Severity = r.Severity; x.Weight = r.Weight; x.Enabled = r.Enabled; x.RecommendationEffect = r.RecommendationEffect; }

    private static void MapScenarios(WebApplication app)
    {
        var group = app.MapGroup("/api/evaluations/{evaluationId:guid}/scenarios").RequireAuthorization("EvaluationWrite");
        group.MapGet("/", async (Guid evaluationId, AppDbContext db) => Results.Ok(await db.Scenarios.AsNoTracking().Where(x => x.EvaluationId == evaluationId).OrderBy(x => x.Name).ToListAsync())).RequireAuthorization("ReadAccess");
        group.MapPost("/calculate", (Guid evaluationId, ScenarioRequest request) => Results.Ok(ScenarioCalculator.Calculate(ToScenario(evaluationId, request)))).AddEndpointFilter<ValidationFilter<ScenarioRequest>>();
        group.MapPost("/", async (Guid evaluationId, ScenarioRequest request, AppDbContext db) => { if (!await db.Evaluations.AnyAsync(x => x.Id == evaluationId && x.Status != EvaluationStatus.Archived)) return Results.NotFound(); var x = ToScenario(evaluationId, request); var result = ScenarioCalculator.Calculate(x); x.ResultJson = JsonSerializer.Serialize(result, Json); db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/evaluations/{evaluationId}/scenarios/{x.Id}", new { scenario = x, result }); }).AddEndpointFilter<ValidationFilter<ScenarioRequest>>();
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
        group.MapGet("/", async (HttpRequest request,AppDbContext db,int page=1,int pageSize=20,string? search=null,DealStatus? status=null,string sort="recent") => {var q=db.Deals.AsNoTracking().Include(x=>x.Brand).AsQueryable();if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>EF.Functions.ILike(x.Name,$"%{search}%")||EF.Functions.ILike(x.Brand!.Name,$"%{search}%"));if(status.HasValue)q=q.Where(x=>x.Status==status);var ordered=sort switch{"oldest"=>q.OrderBy(x=>x.UpdatedAt),"name"=>q.OrderBy(x=>x.Name),"nameDesc"=>q.OrderByDescending(x=>x.Name),_=>q.OrderByDescending(x=>x.UpdatedAt)};if(!request.Query.ContainsKey("page"))return Results.Ok(await ordered.Take(100).ToListAsync());return Results.Ok(await Page(ordered,page,pageSize));}).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) => await db.Deals.AsNoTracking().Include(x => x.Brand).Include(x => x.Conditions).FirstOrDefaultAsync(x => x.Id == id) is { } x ? Results.Ok(x) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/from-evaluation/{evaluationId:guid}", async (Guid evaluationId, DealRequest request, AppDbContext db, ClaimsPrincipal user) => { var e = await db.Evaluations.Include(x => x.Conditions).SingleOrDefaultAsync(x => x.Id == evaluationId); if (e is null) return Results.NotFound(); if (e.Status != EvaluationStatus.Approved) return Results.Conflict(new { error = "Önce değerlendirmeyi onaylayın." }); var d = ToDeal(e, request); db.Add(d); Audit(db, user, "DealCreated", "Deal", d.Id, null, d); await db.SaveChangesAsync(); return Results.Created($"/api/deals/{d.Id}", d); }).AddEndpointFilter<ValidationFilter<DealRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/compare", async (DealComparisonRequest request, AppDbContext db) => { var ids=request.DealIds.Distinct().ToList();if(ids.Count<2)return Results.ValidationProblem(new Dictionary<string,string[]>{{"dealIds",["Karşılaştırma için en az iki farklı anlaşma seçin."]}});var deals = await db.Deals.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();if(deals.Count!=ids.Count)return Results.NotFound(new{error="Seçilen anlaşmalardan biri bulunamadı."});if(deals.Select(x=>x.BrandId).Distinct().Count()!=1||deals.Select(x=>x.EvaluationId).Distinct().Count()!=1||deals[0].EvaluationId!=request.Basis.EvaluationId)return Results.Conflict(new{error="Yalnızca aynı marka ve değerlendirmeye ait anlaşmalar karşılaştırılabilir."});var settings = await db.GeneralSettings.AsNoTracking().SingleAsync(); return Results.Ok(DealComparisonEngine.Compare(deals, request.Basis, settings)); }).RequireAuthorization("ReadAccess");
        group.MapPost("/{id:guid}/accept", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await ChangeDealStatus(id, DealStatus.Accepted, db, user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/activate", async (Guid id, AppDbContext db, ClaimsPrincipal user) => { var deal = await db.Deals.Include(x => x.Brand).SingleOrDefaultAsync(x => x.Id == id); if (deal is null) return Results.NotFound(); if (deal.Status != DealStatus.Accepted) return Results.Conflict(new { error = "Yalnızca kabul edilmiş anlaşmalar etkinleştirilebilir." }); if(await db.Deals.AnyAsync(x=>x.BrandId==deal.BrandId&&x.Status==DealStatus.Active&&x.Id!=id))return Results.Conflict(new{error="Bu markanın zaten etkin bir anlaşması var. Önce mevcut anlaşmayı sonlandırın."}); deal.Status = DealStatus.Active; deal.StartDate ??= DateOnly.FromDateTime(DateTime.UtcNow);deal.EndDate??=deal.StartDate.Value.AddMonths(deal.ContractMonths); deal.Brand!.Status = BrandStatus.Active; Audit(db, user, "DealActivated", "Deal", id, null, deal); await db.SaveChangesAsync(); return Results.Ok(deal); }).RequireAuthorization("OperationsWrite");
        group.MapPut("/{id:guid}", async (Guid id,DealRequest request,AppDbContext db,ClaimsPrincipal user)=>{var deal=await db.Deals.SingleOrDefaultAsync(x=>x.Id==id);if(deal is null)return Results.NotFound();if(deal.Status is not(DealStatus.Draft or DealStatus.InternalReview or DealStatus.Proposed or DealStatus.Negotiation))return Results.Conflict(new{error="Kabul edilmiş veya kapanmış anlaşmanın ticari koşulları değiştirilemez."});var old=JsonSerializer.Serialize(deal,Json);CopyDeal(deal,request);deal.UpdatedAt=DateTimeOffset.UtcNow;Audit(db,user,"DealChanged","Deal",id,old,deal);await db.SaveChangesAsync();return Results.Ok(deal);}).AddEndpointFilter<ValidationFilter<DealRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/review",async(Guid id,AppDbContext db,ClaimsPrincipal user)=>await MoveDeal(id,[DealStatus.Draft],DealStatus.InternalReview,"DealSubmittedForReview",db,user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/propose",async(Guid id,AppDbContext db,ClaimsPrincipal user)=>await MoveDeal(id,[DealStatus.InternalReview],DealStatus.Proposed,"DealProposed",db,user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/negotiate",async(Guid id,AppDbContext db,ClaimsPrincipal user)=>await MoveDeal(id,[DealStatus.Proposed],DealStatus.Negotiation,"DealNegotiationStarted",db,user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/terminate",async(Guid id,DealLifecycleRequest request,AppDbContext db,ClaimsPrincipal user)=>await CloseDeal(id,DealStatus.Terminated,request,db,user)).AddEndpointFilter<ValidationFilter<DealLifecycleRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/expire",async(Guid id,DealLifecycleRequest request,AppDbContext db,ClaimsPrincipal user)=>await CloseDeal(id,DealStatus.Expired,request,db,user)).AddEndpointFilter<ValidationFilter<DealLifecycleRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/renew",async(Guid id,DealLifecycleRequest request,AppDbContext db,ClaimsPrincipal user)=>{var source=await db.Deals.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id);if(source is null)return Results.NotFound();if(source.Status is not(DealStatus.Active or DealStatus.Expired))return Results.Conflict(new{error="Yalnızca etkin veya süresi dolmuş anlaşmalar yenilenebilir."});var renewal=CloneDeal(source);renewal.Name=$"{source.Name} · Yenileme";renewal.RenewalOfDealId=source.Id;renewal.StatusReason=request.Reason.Trim();db.Add(renewal);Audit(db,user,"DealRenewalCreated","Deal",renewal.Id,source,renewal,request.Reason);await db.SaveChangesAsync();return Results.Created($"/api/deals/{renewal.Id}",renewal);}).AddEndpointFilter<ValidationFilter<DealLifecycleRequest>>().RequireAuthorization("OperationsWrite");
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
    private static void CopyDeal(Deal d,DealRequest r){d.Name=r.Name.Trim();d.DealType=r.DealType;d.ContractMonths=r.ContractMonths;d.BaselineRevenue=r.BaselineRevenue;d.BaselinePeriodStart=r.BaselinePeriodStart;d.BaselinePeriodEnd=r.BaselinePeriodEnd;d.BaselineCalculationMethod=r.BaselineCalculationMethod;d.MonthlyRetainer=r.MonthlyRetainer;d.MinimumMonthlyFee=r.MinimumMonthlyFee;d.RevenueShareRate=r.RevenueShareRate;d.IncrementalRate=r.IncrementalRate;d.ProfitShareRate=r.ProfitShareRate;d.CommissionTiersJson=JsonSerializer.Serialize(r.CommissionTiers,Json);d.SetupInvestment=r.SetupInvestment;d.EstimatedMonthlyInternalCost=r.EstimatedMonthlyInternalCost;d.CommissionSnapshotJson=JsonSerializer.Serialize(r,Json);}
    private static Deal CloneDeal(Deal s)=>new(){BrandId=s.BrandId,EvaluationId=s.EvaluationId,Name=s.Name,DealType=s.DealType,ContractMonths=s.ContractMonths,BaselineRevenue=s.BaselineRevenue,BaselinePeriodStart=s.BaselinePeriodStart,BaselinePeriodEnd=s.BaselinePeriodEnd,BaselineCalculationMethod=s.BaselineCalculationMethod,MonthlyRetainer=s.MonthlyRetainer,MinimumMonthlyFee=s.MinimumMonthlyFee,RevenueShareRate=s.RevenueShareRate,IncrementalRate=s.IncrementalRate,ProfitShareRate=s.ProfitShareRate,CommissionTiersJson=s.CommissionTiersJson,SetupInvestment=s.SetupInvestment,EstimatedMonthlyInternalCost=s.EstimatedMonthlyInternalCost,Currency=s.Currency,EvaluationSnapshotJson=s.EvaluationSnapshotJson,RuleSnapshotJson=s.RuleSnapshotJson,FinancialSnapshotJson=s.FinancialSnapshotJson,CommissionSnapshotJson=s.CommissionSnapshotJson,ConditionsSnapshotJson=s.ConditionsSnapshotJson};
    private static async Task<IResult> MoveDeal(Guid id,DealStatus[] from,DealStatus target,string action,AppDbContext db,ClaimsPrincipal user){var d=await db.Deals.FindAsync(id);if(d is null)return Results.NotFound();if(!from.Contains(d.Status))return Results.Conflict(new{error=$"Anlaşma {d.Status} durumundan {target} durumuna geçirilemez."});var old=d.Status;d.Status=target;d.UpdatedAt=DateTimeOffset.UtcNow;Audit(db,user,action,"Deal",id,old,target);await db.SaveChangesAsync();return Results.Ok(d);}
    private static async Task<IResult> CloseDeal(Guid id,DealStatus target,DealLifecycleRequest request,AppDbContext db,ClaimsPrincipal user){var d=await db.Deals.Include(x=>x.Brand).SingleOrDefaultAsync(x=>x.Id==id);if(d is null)return Results.NotFound();if(d.Status!=DealStatus.Active)return Results.Conflict(new{error="Yalnızca etkin anlaşmalar kapatılabilir."});var old=d.Status;d.Status=target;d.EndDate=request.EffectiveDate??DateOnly.FromDateTime(DateTime.UtcNow);d.StatusReason=request.Reason.Trim();d.UpdatedAt=DateTimeOffset.UtcNow;if(d.Brand is not null&&!await db.Deals.AnyAsync(x=>x.BrandId==d.BrandId&&x.Id!=id&&x.Status==DealStatus.Active))d.Brand.Status=BrandStatus.Closed;Audit(db,user,target==DealStatus.Terminated?"DealTerminated":"DealExpired","Deal",id,old,d,request.Reason);await db.SaveChangesAsync();return Results.Ok(d);}
    private static async Task<IResult> ChangeDealStatus(Guid id, DealStatus status, AppDbContext db, ClaimsPrincipal user) { var deal = await db.Deals.FindAsync(id); if (deal is null) return Results.NotFound(); if(status==DealStatus.Accepted&&!await db.Evaluations.AnyAsync(x=>x.Id==deal.EvaluationId&&x.Status==EvaluationStatus.Approved))return Results.Conflict(new{error="Anlaşmayı kabul etmeden önce değerlendirmeyi onaylayın."}); if(status==DealStatus.Accepted&&deal.Status is not (DealStatus.Draft or DealStatus.InternalReview or DealStatus.Proposed or DealStatus.Negotiation))return Results.Conflict(new{error=$"{deal.Status} durumundaki bir anlaşma kabul edilemez."}); deal.Status = status; deal.UpdatedAt = DateTimeOffset.UtcNow; Audit(db, user, "DealChanged", "Deal", id, null, deal); await db.SaveChangesAsync(); return Results.Ok(deal); }

    private static void MapConditions(WebApplication app)
    {
        app.MapPut("/api/conditions/{id:guid}",async(Guid id,ConditionUpdateRequest request,AppDbContext db,ClaimsPrincipal user)=>{var condition=await db.PartnershipConditions.FindAsync(id);if(condition is null)return Results.NotFound();var old=JsonSerializer.Serialize(condition,Json);condition.Status=request.Status;condition.ResolutionReason=request.Reason.Trim();condition.EvidenceUrl=request.EvidenceUrl.Trim();condition.ResolvedBy=User(user);condition.ResolvedAt=DateTimeOffset.UtcNow;Audit(db,user,request.Status==ConditionStatus.Waived?"ConditionWaived":"ConditionSatisfied","Condition",id,old,condition,request.Reason);await db.SaveChangesAsync();return Results.Ok(condition);}).AddEndpointFilter<ValidationFilter<ConditionUpdateRequest>>().RequireAuthorization("OperationsWrite");
    }

    private static void MapDealTemplates(WebApplication app)
    {
        var group=app.MapGroup("/api/deal-templates").RequireAuthorization("ReadAccess");
        group.MapGet("/",async(AppDbContext db)=>Results.Ok(await db.DealTemplates.AsNoTracking().OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Name).ToListAsync()));
        group.MapPost("/",async(DealTemplateRequest request,AppDbContext db,ClaimsPrincipal user)=>{var value=ToTemplate(request);db.Add(value);Audit(db,user,"DealTemplateCreated","DealTemplate",value.Id,null,value);await db.SaveChangesAsync();return Results.Created($"/api/deal-templates/{value.Id}",value);}).AddEndpointFilter<ValidationFilter<DealTemplateRequest>>().RequireAuthorization("AdminOnly");
        group.MapPut("/{id:guid}",async(Guid id,DealTemplateRequest request,AppDbContext db,ClaimsPrincipal user)=>{var value=await db.DealTemplates.FindAsync(id);if(value is null)return Results.NotFound();var old=JsonSerializer.Serialize(value,Json);CopyTemplate(value,request);Audit(db,user,"DealTemplateChanged","DealTemplate",id,old,value);await db.SaveChangesAsync();return Results.Ok(value);}).AddEndpointFilter<ValidationFilter<DealTemplateRequest>>().RequireAuthorization("AdminOnly");
        app.MapPost("/api/deals/from-evaluation/{evaluationId:guid}/templates",async(Guid evaluationId,AppDbContext db,ClaimsPrincipal user)=>{var evaluation=await db.Evaluations.Include(x=>x.Brand)!.ThenInclude(x=>x!.Economics).Include(x=>x.Conditions).SingleOrDefaultAsync(x=>x.Id==evaluationId);if(evaluation is null)return Results.NotFound();if(evaluation.Status!=EvaluationStatus.Approved)return Results.Conflict(new{error="Önce değerlendirmeyi onaylayın."});if(await db.Deals.AnyAsync(x=>x.EvaluationId==evaluationId))return Results.Conflict(new{error="Bu değerlendirme için anlaşma seçenekleri zaten oluşturulmuş."});var templates=await db.DealTemplates.AsNoTracking().Where(x=>x.Enabled).OrderBy(x=>x.DisplayOrder).ToListAsync();if(templates.Count==0)return Results.Conflict(new{error="Etkin anlaşma şablonu bulunamadı."});var deals=templates.Select(t=>ToDeal(evaluation,new DealRequest(t.Name,t.DealType,t.ContractMonths,evaluation.Brand!.Economics!.AverageMonthlyRevenue,null,null,BaselineCalculationMethod.Manual,t.MonthlyRetainer,t.MinimumMonthlyFee,t.RevenueShareRate,t.IncrementalRate,t.ProfitShareRate,JsonSerializer.Deserialize<List<CommissionTier>>(t.CommissionTiersJson,Json)??[],evaluation.SetupInvestment,evaluation.InternalMonthlyCost))).ToList();db.AddRange(deals);foreach(var d in deals)Audit(db,user,"DealCreatedFromTemplate","Deal",d.Id,null,d);await db.SaveChangesAsync();return Results.Created($"/api/evaluations/{evaluationId}/deals",deals);}).RequireAuthorization("OperationsWrite");
    }
    private static DealTemplate ToTemplate(DealTemplateRequest r){var x=new DealTemplate{Name=r.Name};CopyTemplate(x,r);return x;}
    private static void CopyTemplate(DealTemplate x,DealTemplateRequest r){x.Name=r.Name.Trim();x.Description=r.Description.Trim();x.Enabled=r.Enabled;x.DisplayOrder=r.DisplayOrder;x.DealType=r.DealType;x.ContractMonths=r.ContractMonths;x.MonthlyRetainer=r.MonthlyRetainer;x.MinimumMonthlyFee=r.MinimumMonthlyFee;x.RevenueShareRate=r.RevenueShareRate;x.IncrementalRate=r.IncrementalRate;x.ProfitShareRate=r.ProfitShareRate;x.CommissionTiersJson=JsonSerializer.Serialize(r.CommissionTiers,Json);x.UpdatedAt=DateTimeOffset.UtcNow;}

    private static void MapDocuments(WebApplication app)
    {
        app.MapGet("/api/documents/{entityType}/{entityId}",async(string entityType,string entityId,AppDbContext db)=>Results.Ok(await db.DocumentAttachments.AsNoTracking().Where(x=>x.EntityType==entityType&&x.EntityId==entityId).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.EntityType,x.EntityId,x.FileName,x.ContentType,size=x.Content.Length,x.Note,x.UploadedBy,x.CreatedAt}).ToListAsync())).RequireAuthorization("ReadAccess");
        app.MapGet("/api/documents/{id:guid}/download",async(Guid id,AppDbContext db)=>await db.DocumentAttachments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id) is{} x?Results.File(x.Content,x.ContentType,x.FileName):Results.NotFound()).RequireAuthorization("ReadAccess");
        app.MapPost("/api/documents/{entityType}/{entityId}",async(string entityType,string entityId,HttpRequest request,AppDbContext db,ClaimsPrincipal user)=>{if(!await EntityExists(entityType,entityId,db))return Results.NotFound(new{error="Belgenin bağlanacağı kayıt bulunamadı."});if(!request.HasFormContentType)return Results.BadRequest(new{error="Bir dosya seçin."});var form=await request.ReadFormAsync();var file=form.Files.FirstOrDefault();if(file is null||file.Length==0)return Results.BadRequest(new{error="Bir dosya seçin."});if(file.Length>10*1024*1024)return Results.BadRequest(new{error="Dosya boyutu 10 MB sınırını aşamaz."});var allowed=new[]{"application/pdf","image/png","image/jpeg","text/csv","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"};if(!allowed.Contains(file.ContentType))return Results.BadRequest(new{error="PDF, PNG, JPG, CSV veya Excel dosyası yükleyin."});await using var stream=new MemoryStream();await file.CopyToAsync(stream);var value=new DocumentAttachment{EntityType=entityType,EntityId=entityId,FileName=Path.GetFileName(file.FileName),ContentType=file.ContentType,Content=stream.ToArray(),Note=form["note"].ToString().Trim(),UploadedBy=User(user)};db.Add(value);Audit(db,user,"DocumentUploaded","Document",value.Id,null,new{value.EntityType,value.EntityId,value.FileName,value.Note});await db.SaveChangesAsync();return Results.Created($"/api/documents/{value.Id}",new{value.Id,value.FileName,value.Note,value.CreatedAt});}).DisableAntiforgery().RequireAuthorization("OperationsWrite");
    }
    private static async Task<bool> EntityExists(string type,string id,AppDbContext db)=>Guid.TryParse(id,out var value)&&type switch{"Brand"=>await db.Brands.AnyAsync(x=>x.Id==value),"Evaluation"=>await db.Evaluations.AnyAsync(x=>x.Id==value),"Deal"=>await db.Deals.AnyAsync(x=>x.Id==value),"MonthlyPerformance"=>await db.MonthlyPerformances.AnyAsync(x=>x.Id==value),_=>false};

    private static void MapPerformance(WebApplication app)
    {
        var group = app.MapGroup("/api/performance").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (AppDbContext db,int page=1,int pageSize=20,string? search=null,MonthlyPerformanceStatus? status=null,int? year=null,int? month=null,string sort="recent") => {var q=db.MonthlyPerformances.AsNoTracking().Include(x=>x.Brand).Include(x=>x.Deal).Include(x=>x.Adjustments).AsQueryable();if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>EF.Functions.ILike(x.Brand!.Name,$"%{search}%"));if(status.HasValue)q=q.Where(x=>x.Status==status);if(year.HasValue)q=q.Where(x=>x.Year==year);if(month.HasValue)q=q.Where(x=>x.Month==month);var ordered=sort switch{"oldest"=>q.OrderBy(x=>x.Year).ThenBy(x=>x.Month),"name"=>q.OrderBy(x=>x.Brand!.Name),"nameDesc"=>q.OrderByDescending(x=>x.Brand!.Name),_=>q.OrderByDescending(x=>x.Year).ThenByDescending(x=>x.Month)};return Results.Ok(await Page(ordered,page,pageSize));}).RequireAuthorization("ReadAccess");
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) => await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal).Include(x => x.Adjustments).FirstOrDefaultAsync(x => x.Id == id) is { } x ? Results.Ok(x) : Results.NotFound()).RequireAuthorization("ReadAccess");
        group.MapPost("/calculate", async (PerformanceRequest request, AppDbContext db) => { var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.DealId && x.Status == DealStatus.Active); if (deal is null) return Results.Conflict(new { error = "Etkin bir anlaşma gereklidir." }); if(deal.BrandId!=request.BrandId)return Results.Conflict(new{error="Seçilen anlaşma bu markaya ait değildir."}); var p = ToPerformance(request); MonthlyPerformanceCalculator.Calculate(p, deal); return Results.Ok(p); }).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/", async (PerformanceRequest request, AppDbContext db, ClaimsPrincipal user) => { var deal = await db.Deals.SingleOrDefaultAsync(x => x.Id == request.DealId && x.Status == DealStatus.Active); if (deal is null) return Results.Conflict(new { error = "Etkin bir anlaşma gereklidir." }); if(deal.BrandId!=request.BrandId)return Results.Conflict(new{error="Seçilen anlaşma bu markaya ait değildir."});if(await db.MonthlyPerformances.AnyAsync(x=>x.BrandId==request.BrandId&&x.Year==request.Year&&x.Month==request.Month))return Results.Conflict(new{error="Bu marka ve dönem için daha önce kayıt oluşturulmuş."}); var p = ToPerformance(request); MonthlyPerformanceCalculator.Calculate(p, deal); db.Add(p); Audit(db, user, "MonthlyPerformanceCreated", "MonthlyPerformance", p.Id, null, p); await db.SaveChangesAsync(); return Results.Created($"/api/performance/{p.Id}", p); }).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPut("/{id:guid}", async (Guid id, PerformanceRequest request, AppDbContext db, ClaimsPrincipal user) => { var p = await db.MonthlyPerformances.Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound(); if (!MonthlyCloseWorkflow.CanEdit(p.Status)) return Results.Conflict(new { error = "Kilitlenmiş dönemler değiştirilemez." });if(p.BrandId!=request.BrandId||p.DealId!=request.DealId)return Results.Conflict(new{error="Dönemin markası veya anlaşması sonradan değiştirilemez."}); Copy(p, request); MonthlyPerformanceCalculator.Calculate(p, p.Deal!); Audit(db, user, "MonthlyPerformanceChanged", "MonthlyPerformance", id, null, p); await db.SaveChangesAsync(); return Results.Ok(p); }).AddEndpointFilter<ValidationFilter<PerformanceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/submit", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await SubmitPerformance(id,db,user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/approve", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await ApprovePerformance(id,db,user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/lock", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await LockPerformance(id,db,user)).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/unlock", async (Guid id, TransitionRequest request, AppDbContext db, ClaimsPrincipal user) => { if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string,string[]>{{"reason",["Kilidi açma nedeni zorunludur."]}}); var p = await db.MonthlyPerformances.FindAsync(id); if (p is null) return Results.NotFound(); if (!MonthlyCloseWorkflow.CanUnlock(p.Status)) return Results.Conflict(new{error="Yalnızca henüz faturalanmamış kilitli dönemlerin kilidi açılabilir."}); var old = p.Status; p.Status = MonthlyPerformanceStatus.Approved; Audit(db, user, "MonthlyCloseUnlocked", "MonthlyPerformance", id, old, p.Status, request.Reason); await db.SaveChangesAsync(); return Results.Ok(p); }).RequireAuthorization("AdminOnly");
        group.MapPost("/{id:guid}/adjustments", async (Guid id, AdjustmentRequest request, AppDbContext db, ClaimsPrincipal user) => { var p = await db.MonthlyPerformances.Include(x => x.Deal).Include(x => x.Adjustments).SingleOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound();if(!MonthlyCloseWorkflow.CanAdjust(p.Status))return Results.Conflict(new{error="Kilitlenmiş, faturalanmış veya ödenmiş dönemlere düzeltme eklenemez. Faturalanmamış kilitli dönem için önce yönetici kilidi açmalıdır."}); var a = new CommissionAdjustment { MonthlyPerformanceId = id, Amount = request.Amount, Reason = request.Reason.Trim(), CreatedBy = User(user) }; p.Adjustments.Add(a); db.CommissionAdjustments.Add(a); MonthlyPerformanceCalculator.Calculate(p, p.Deal!); Audit(db, user, "CommissionAdjusted", "MonthlyPerformance", id, null, a, request.Reason); await db.SaveChangesAsync(); return Results.Ok(new { adjustment = a, p.OvoFee, p.CommissionBreakdownJson }); }).AddEndpointFilter<ValidationFilter<AdjustmentRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/invoice", () => Results.Conflict(new { error = "Hakediş dökümündeki fatura ve tahsilat formunu kullanın." })).RequireAuthorization("OperationsWrite");
        group.MapPost("/{id:guid}/pay", () => Results.Conflict(new { error = "Hakediş dökümünden ödeme tutarı, tarihi ve referansı ile tahsilat kaydedin." })).RequireAuthorization("OperationsWrite");
        app.MapGet("/api/commissions", ListCommissions).RequireAuthorization("ReadAccess");
    }
    private static MonthlyPerformance ToPerformance(PerformanceRequest r) { var p = new MonthlyPerformance { BrandId = r.BrandId, DealId = r.DealId, Year = r.Year, Month = r.Month }; Copy(p, r); return p; }
    private static void Copy(MonthlyPerformance p, PerformanceRequest r) { p.GrossSales=r.GrossSales;p.Vat=r.Vat;p.Refunds=r.Refunds;p.Cancellations=r.Cancellations;p.Chargebacks=r.Chargebacks;p.CustomerPaidShipping=r.CustomerPaidShipping;p.GiftCardTopups=r.GiftCardTopups;p.Orders=r.Orders;p.Sessions=r.Sessions;p.NewCustomers=r.NewCustomers;p.ReturningCustomers=r.ReturningCustomers;p.Cogs=r.Cogs;p.PaymentFees=r.PaymentFees;p.FulfillmentCosts=r.FulfillmentCosts;p.ShippingSubsidy=r.ShippingSubsidy;p.OtherVariableCosts=r.OtherVariableCosts;p.MetaSpend=r.MetaSpend;p.GoogleSpend=r.GoogleSpend;p.TikTokSpend=r.TikTokSpend;p.InfluencerSpend=r.InfluencerSpend;p.OtherAdSpend=r.OtherAdSpend; }
    private static async Task<IResult> SubmitPerformance(Guid id,AppDbContext db,ClaimsPrincipal user){var p=await db.MonthlyPerformances.FindAsync(id);if(p is null)return Results.NotFound();if(!MonthlyCloseWorkflow.CanTransition(p.Status,MonthlyPerformanceStatus.UnderReview))return Results.Conflict(new{error="Yalnızca taslak dönem kontrole gönderilebilir."});p.Status=MonthlyPerformanceStatus.UnderReview;p.PreparedBy=User(user);p.SubmittedAt=DateTimeOffset.UtcNow;Audit(db,user,"MonthlyPerformanceSubmitted","MonthlyPerformance",id,MonthlyPerformanceStatus.Draft,p.Status);await db.SaveChangesAsync();return Results.Ok(p);}
    private static async Task<IResult> ApprovePerformance(Guid id,AppDbContext db,ClaimsPrincipal user){var p=await db.MonthlyPerformances.FindAsync(id);if(p is null)return Results.NotFound();if(!MonthlyCloseWorkflow.CanTransition(p.Status,MonthlyPerformanceStatus.Approved))return Results.Conflict(new{error="Yalnızca kontrol bekleyen dönem onaylanabilir."});var reviewer=User(user);if(string.Equals(p.PreparedBy,reviewer,StringComparison.OrdinalIgnoreCase))return Results.Conflict(new{error="Aylık sonucu hazırlayan kişi aynı kaydı onaylayamaz. Başka bir yetkili onaylamalıdır."});p.Status=MonthlyPerformanceStatus.Approved;p.ReviewedBy=reviewer;p.ApprovedAt=DateTimeOffset.UtcNow;Audit(db,user,"MonthlyPerformanceApproved","MonthlyPerformance",id,MonthlyPerformanceStatus.UnderReview,p.Status);await db.SaveChangesAsync();return Results.Ok(p);}
    private static async Task<IResult> LockPerformance(Guid id,AppDbContext db,ClaimsPrincipal user){var p=await db.MonthlyPerformances.FindAsync(id);if(p is null)return Results.NotFound();if(!MonthlyCloseWorkflow.CanTransition(p.Status,MonthlyPerformanceStatus.Locked))return Results.Conflict(new{error="Yalnızca onaylanmış dönem kilitlenebilir."});p.Status=MonthlyPerformanceStatus.Locked;p.LockedAt=DateTimeOffset.UtcNow;Audit(db,user,"MonthlyCloseLocked","MonthlyPerformance",id,MonthlyPerformanceStatus.Approved,p.Status);await db.SaveChangesAsync();return Results.Ok(p);}

    private static void MapDashboard(WebApplication app)
    {
        app.MapGet("/api/dashboard", PortfolioReport).RequireAuthorization("ReadAccess");
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

    private static void MapSearchAndTasks(WebApplication app)
    {
        app.MapGet("/api/search", async (string q, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Results.Ok(Array.Empty<object>());
            var term=$"%{q.Trim()}%";
            var brands=await db.Brands.AsNoTracking().Where(x=>EF.Functions.ILike(x.Name,term)).OrderBy(x=>x.Name).Take(5).Select(x=>new{type="Marka",title=x.Name,detail=x.Industry,href=$"/brands/{x.Id}"}).ToListAsync();
            var evaluations=await db.Evaluations.AsNoTracking().Where(x=>EF.Functions.ILike(x.Brand!.Name,term)).OrderByDescending(x=>x.UpdatedAt).Take(5).Select(x=>new{type="Değerlendirme",title=x.Brand!.Name,detail=x.Status.ToString(),href=$"/evaluations/{x.Id}"}).ToListAsync();
            var deals=await db.Deals.AsNoTracking().Where(x=>EF.Functions.ILike(x.Name,term)||EF.Functions.ILike(x.Brand!.Name,term)).OrderByDescending(x=>x.UpdatedAt).Take(5).Select(x=>new{type="Anlaşma",title=x.Name,detail=x.Brand!.Name,href=$"/deals?open={x.Id}"}).ToListAsync();
            return Results.Ok(brands.Cast<object>().Concat(evaluations).Concat(deals));
        }).RequireAuthorization("ReadAccess");

        app.MapGet("/api/tasks", TeamReminders).RequireAuthorization("ReadAccess");
    }

    private static void MapSettingsAndAudit(WebApplication app)
    {
        app.MapGet("/api/settings", async (AppDbContext db) => Results.Ok(await db.GeneralSettings.AsNoTracking().SingleAsync())).RequireAuthorization("ReadAccess");
        app.MapPut("/api/settings", async (SettingsRequest r, AppDbContext db, ClaimsPrincipal user) => {if(r.DefaultRuleSetId.HasValue&&!await db.RuleSets.AnyAsync(y=>y.Id==r.DefaultRuleSetId&&y.Status==RuleSetStatus.Published))return Results.ValidationProblem(new Dictionary<string,string[]>{{"defaultRuleSetId",["Varsayılan kural seti yayımlanmış olmalıdır."]}});var x=await db.GeneralSettings.SingleAsync();var old=JsonSerializer.Serialize(x,Json);x.DefaultCurrency=r.DefaultCurrency.ToUpperInvariant();x.DefaultVatRate=r.DefaultVatRate;x.DefaultContractMonths=r.DefaultContractMonths;x.DefaultSetupInvestment=r.DefaultSetupInvestment;x.TargetOvoGrossMargin=r.TargetOvoGrossMargin;x.TargetBrandContributionMargin=r.TargetBrandContributionMargin;x.MinimumFeeMultiplier=r.MinimumFeeMultiplier;x.ExistingRevenueThreshold=r.ExistingRevenueThreshold;x.ConcentrationRiskThreshold=r.ConcentrationRiskThreshold;x.MinimumPartnershipScore=r.MinimumPartnershipScore;x.ConditionalPartnershipScore=r.ConditionalPartnershipScore;x.MinimumDataConfidenceScore=r.MinimumDataConfidenceScore;x.MinimumRecommendedAdSpend=r.MinimumRecommendedAdSpend;x.DefaultRuleSetId=r.DefaultRuleSetId;x.UpdatedAt=DateTimeOffset.UtcNow;Audit(db,user,"SettingsChanged","Settings",x.Id,old,x);await db.SaveChangesAsync();return Results.Ok(x); }).AddEndpointFilter<ValidationFilter<SettingsRequest>>().RequireAuthorization("AdminOnly");
        app.MapGet("/api/audit", async (AppDbContext db,int page=1,int pageSize=20,string? entityType=null,Guid? entityId=null,string? search=null,string sort="recent") => {var q=db.AuditRecords.AsNoTracking().AsQueryable();if(entityType is not null)q=q.Where(x=>x.EntityType==entityType);if(entityId.HasValue)q=q.Where(x=>x.EntityId==entityId.ToString());if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>EF.Functions.ILike(x.UserId,$"%{search}%")||EF.Functions.ILike(x.Action,$"%{search}%"));var ordered=sort switch{"oldest"=>q.OrderBy(x=>x.CreatedAt),"name"=>q.OrderBy(x=>x.UserId),"nameDesc"=>q.OrderByDescending(x=>x.UserId),_=>q.OrderByDescending(x=>x.CreatedAt)};return Results.Ok(await Page(ordered,page,pageSize));}).RequireAuthorization("ReadAccess");
    }

    private static async Task<object> Page<T>(IQueryable<T> query,int page,int pageSize)
    {
        var current=Math.Max(1,page);var size=Math.Clamp(pageSize,1,100);var total=await query.CountAsync();
        return new{items=await query.Skip((current-1)*size).Take(size).ToListAsync(),total,page=current,pageSize=size};
    }

    private static string User(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private static void Audit(AppDbContext db, ClaimsPrincipal user, string action, string entity, Guid id, object? oldValue, object? newValue, string reason="") => db.AuditRecords.Add(new AuditRecord { UserId=User(user),Action=action,EntityType=entity,EntityId=id.ToString(),OldValueJson=oldValue is null?"":oldValue is string s?s:JsonSerializer.Serialize(oldValue,Json),NewValueJson=newValue is null?"":JsonSerializer.Serialize(newValue,Json),Reason=reason });
}
