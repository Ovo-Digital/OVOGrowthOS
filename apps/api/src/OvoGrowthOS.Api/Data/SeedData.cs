using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Data;

public static class SeedData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    public static async Task InitializeAsync(AppDbContext db, IConfiguration configuration)
    {
        await db.Database.MigrateAsync();
        if (!await db.UserAccounts.AnyAsync())
        {
            var email = configuration["DefaultAdmin:Email"] ?? "admin@ovodigital.com";
            var hash = configuration["DefaultAdmin:PasswordHash"];
            if (!string.IsNullOrWhiteSpace(hash)) db.UserAccounts.Add(new UserAccount { Email = email.ToLowerInvariant(), Name = "OVO Admin", Role = "Admin", PasswordHash = hash });
        }
        var ruleSet = await SeedRulesAsync(db);
        var settings = await db.GeneralSettings.SingleOrDefaultAsync();
        if (settings is null)
        {
            settings = new GeneralSettings { DefaultRuleSetId = ruleSet.Id };
            db.GeneralSettings.Add(settings);
        }
        if (!await db.DealTemplates.AnyAsync())
        {
            var tiers=JsonSerializer.Serialize(new[]{new CommissionTier(0,500_000,.08m),new CommissionTier(500_000,1_500_000,.06m),new CommissionTier(1_500_000,3_000_000,.045m),new CommissionTier(3_000_000,null,.035m)},Json);
            db.DealTemplates.AddRange(
                new DealTemplate{Name="Seçenek A · Asgari ücret + kademeli pay",Description="Asgari aylık güvence ve ciro büyüdükçe azalan kademeli pay.",DisplayOrder=1,DealType=DealType.MinimumFeePlusRevenueShare,MinimumMonthlyFee=45_000,RevenueShareRate=.05m,CommissionTiersJson=tiers},
                new DealTemplate{Name="Seçenek B · Aylık ücret + sabit pay",Description="Aylık sabit ücret ve düşük oranlı gelir payı.",DisplayOrder=2,DealType=DealType.RetainerPlusRevenueShare,MonthlyRetainer=30_000,RevenueShareRate=.04m},
                new DealTemplate{Name="Seçenek C · Büyüme farkı",Description="Aylık sabit ücret ve baz cironun üzerindeki büyümeden pay.",DisplayOrder=3,DealType=DealType.RetainerPlusIncrementalRevenueShare,MonthlyRetainer=50_000,IncrementalRate=.10m});
        }
        await SeedPortfolioAsync(db, ruleSet, settings);
        await db.SaveChangesAsync();
    }

    private static async Task<RuleSet> SeedRulesAsync(AppDbContext db)
    {
        var existing = await db.RuleSets.Include(x => x.Rules).FirstOrDefaultAsync(x => x.Name == "OVO Varsayılan Karar Kuralları" && x.Version == 1);
        if (existing is not null) return existing;
        var rules = new RuleSet { Name = "OVO Varsayılan Karar Kuralları", Description = "İş ortaklığı kararlarında kullanılan temel kurallar.",
            Status = RuleSetStatus.Published, PublishedAt = DateTimeOffset.UtcNow };
        rules.Rules.AddRange(
            R("Brüt kâr marjı %25'in altında", RuleField.GrossMargin, RuleOperator.LessThan, .25m, RuleSeverity.Critical, RecommendationEffect.PureRevenueShareNotAllowed),
            R("Brüt kâr marjı %30'un altında", RuleField.GrossMargin, RuleOperator.LessThan, .30m, RuleSeverity.High, RecommendationEffect.PreferRetainerLowShare),
            R("Brüt kâr marjı %30-%40 arasında", RuleField.GrossMargin, RuleOperator.Between, .30m, RuleSeverity.Info, RecommendationEffect.PreferRetainerLowShare, .40m),
            R("Brüt kâr marjı %40-%50 arasında", RuleField.GrossMargin, RuleOperator.Between, .40m, RuleSeverity.Info, RecommendationEffect.AllowTieredHybrid, .50m),
            R("Brüt kâr marjı %50-%60 arasında", RuleField.GrossMargin, RuleOperator.Between, .50m, RuleSeverity.Info, RecommendationEffect.AllowFourToSixPercent, .60m),
            R("Brüt kâr marjı en az %60", RuleField.GrossMargin, RuleOperator.GreaterThanOrEqual, .60m, RuleSeverity.Info, RecommendationEffect.AllowFiveToEightPercent),
            R("İade oranı %25'in üzerinde", RuleField.ReturnRate, RuleOperator.GreaterThan, .25m, RuleSeverity.High),
            R("Stok yeterliliği 30 günün altında", RuleField.StockCoverageDays, RuleOperator.LessThan, 30, RuleSeverity.High),
            R("Stok yeterliliği 30-60 gün arasında", RuleField.StockCoverageDays, RuleOperator.Between, 30, RuleSeverity.Warning, RecommendationEffect.None, 60),
            R("Kurucu iş birliği zayıf", RuleField.FounderCooperation, RuleOperator.LessThanOrEqual, 2, RuleSeverity.High),
            R("Operasyonel hazırlık zayıf", RuleField.OperationalReadiness, RuleOperator.LessThanOrEqual, 2, RuleSeverity.High),
            R("Ürün-pazar uyumu zayıf", RuleField.ProductMarketFit, RuleOperator.LessThanOrEqual, 2, RuleSeverity.Warning));
        db.RuleSets.Add(rules);
        return rules;
    }

    private static Rule R(string name, RuleField field, RuleOperator op, decimal value, RuleSeverity severity,
        RecommendationEffect effect = RecommendationEffect.None, decimal? secondary = null) => new() { Name = name,
        Description = name, Category = field is RuleField.GrossMargin or RuleField.ReturnRate ? RuleCategory.Financial : RuleCategory.Risk,
        Field = field, Operator = op, Value = value, SecondaryValue = secondary, Severity = severity, RecommendationEffect = effect };

    private static async Task SeedPortfolioAsync(AppDbContext db, RuleSet ruleSet, GeneralSettings settings)
    {
        var existing = await db.Brands.Select(x => x.Name).ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
        var brands = new[]
        {
            Create("Luna Jewelry", "Premium Takı", 1_080_000, .65m, 205_000, 1_690, .05m, 92, BrandStatus.Active, EvaluationStatus.Approved, 5, 5, 4, 4, 5, 4, ruleSet, settings),
            Create("Mode Atelier", "Kadın Giyim", 5_240_000, .55m, 865_000, 2_190, .14m, 68, BrandStatus.Evaluation, EvaluationStatus.Analyzed, 4, 4, 4, 5, 4, 4, ruleSet, settings),
            Create("Volt Electronics", "Tüketici Elektroniği", 2_180_000, .18m, 95_000, 4_850, .08m, 24, BrandStatus.Rejected, EvaluationStatus.Rejected, 2, 2, 2, 2, 2, 2, ruleSet, settings),
            Create("Arven Home", "Ev ve Yaşam", 3_460_000, .48m, 612_000, 2_480, .09m, 84, BrandStatus.Active, EvaluationStatus.Approved, 4, 5, 4, 4, 5, 4, ruleSet, settings),
            Create("Minoa Skin", "Kozmetik ve Cilt Bakımı", 2_240_000, .72m, 438_000, 990, .035m, 118, BrandStatus.Active, EvaluationStatus.Approved, 5, 5, 5, 5, 4, 4, ruleSet, settings),
            Create("Pera Activewear", "Spor Giyim", 4_780_000, .51m, 1_045_000, 1_760, .19m, 47, BrandStatus.Negotiation, EvaluationStatus.Approved, 4, 5, 3, 5, 4, 4, ruleSet, settings),
            Create("Rooted Nutrition", "Takviye Edici Gıda", 1_620_000, .61m, 285_000, 740, .065m, 73, BrandStatus.Evaluation, EvaluationStatus.Analyzed, 4, 4, 3, 3, 4, 2, ruleSet, settings, DataConfidence.Estimated),
            Create("Lodos Coffee", "Kahve ve İçecek", 1_340_000, .58m, 192_000, 525, .012m, 66, BrandStatus.Active, EvaluationStatus.Approved, 4, 4, 5, 4, 5, 3, ruleSet, settings),
            Create("Minik Bulut Kids", "Bebek ve Çocuk", 890_000, .24m, 0, 1_180, .28m, 21, BrandStatus.Rejected, EvaluationStatus.Rejected, 1, 1, 1, 2, 1, 1, ruleSet, settings),
            Create("Vela Pet", "Evcil Hayvan Ürünleri", 1_780_000, .62m, 318_000, 815, .045m, 76, BrandStatus.Paused, EvaluationStatus.Approved, 5, 4, 4, 4, 4, 4, ruleSet, settings)
        };

        foreach (var brand in brands.Where(x => !existing.Contains(x.Name)))
        {
            var evaluation = brand.Evaluations.Single();
            if (evaluation.Status is EvaluationStatus.Analyzed or EvaluationStatus.Approved or EvaluationStatus.Rejected)
                AddScenarios(evaluation, brand.Economics!, settings);
            if (brand.Status is BrandStatus.Active or BrandStatus.Negotiation or BrandStatus.Paused)
                AddCommercialHistory(brand, evaluation);
            db.Brands.Add(brand);
        }
    }

    private static Brand Create(string name, string industry, decimal revenue, decimal margin, decimal spend, decimal aov,
        decimal returns, int stockDays, BrandStatus brandStatus, EvaluationStatus evaluationStatus, int pmf, int growth,
        int operations, int creative, int founder, int data, RuleSet ruleSet, GeneralSettings settings,
        DataConfidence confidence = DataConfidence.Verified)
    {
        var monthlyOrders = Math.Max(1, (int)(revenue / aov));
        var newCustomers = Math.Max(1, (int)(monthlyOrders * .62m));
        var brand = new Brand { Name = name, LegalName = $"{name} A.Ş.", Industry = industry, Status = brandStatus,
            Country = "TR", Currency = "TRY", Website = $"https://{Slug(name)}.example", ContactName = "Örnek Marka Yetkilisi",
            ContactEmail = $"ekip@{Slug(name)}.example", BusinessModel = BusinessModel.Dtc, Platform = CommercePlatform.Shopify };
        brand.Economics = new BrandEconomics { BrandId = brand.Id, AverageMonthlyRevenue = revenue, RevenueConfidence = confidence,
            GrossMarginRate = margin, GrossMarginConfidence = confidence, CogsRate = 1 - margin, CogsConfidence = confidence,
            CurrentAdSpend = spend, AdSpendConfidence = confidence, AverageOrderValue = aov, AovConfidence = confidence,
            ReturnRate = returns, ReturnRateConfidence = confidence, StockCoverageDays = stockDays, StockCoverageConfidence = confidence,
            MonthlyOrders = monthlyOrders, MonthlySessions = monthlyOrders * 72, VariableCostRate = industry.Contains("Giyim") ? .105m : .075m,
            NewCustomers = newCustomers, ReturningCustomers = monthlyOrders - newCustomers,
            CurrentCac = newCustomers == 0 ? 0 : spend / newCustomers, CacConfidence = confidence,
            AverageCustomerLtv = aov * (industry.Contains("Kahve") || industry.Contains("Kozmetik") ? 3.4m : 2.1m), LtvConfidence = confidence };
        var evaluation = new BrandEvaluation { BrandId = brand.Id, CurrentStep = evaluationStatus is EvaluationStatus.Draft ? 2 : 10,
            Status = evaluationStatus, CreatedBy = "örnek-veri", ProductMarketFit = pmf, GrowthPotential = growth,
            OperationalReadiness = operations, CreativeCapability = creative, FounderCooperation = founder, DataMaturity = data,
            InternalMonthlyCost = revenue >= 3_000_000 ? 58_000 : revenue >= 1_500_000 ? 42_000 : 30_000,
            SetupInvestment = revenue >= 3_000_000 ? 320_000 : 220_000, RuleSetId = ruleSet.Id, RuleSetVersion = ruleSet.Version,
            CompletedAt = evaluationStatus is EvaluationStatus.Draft or EvaluationStatus.InProgress ? null : DateTimeOffset.UtcNow.AddDays(-18) };
        if (evaluationStatus is not (EvaluationStatus.Draft or EvaluationStatus.InProgress))
        {
            var recommendation = DealRecommendationEngine.Recommend(brand.Economics, evaluation, ruleSet, settings);
            evaluation.PartnershipScore = recommendation.PartnershipScore; evaluation.DataConfidenceScore = recommendation.DataConfidenceScore;
            evaluation.Decision = recommendation.Decision; evaluation.RecommendedDealType = recommendation.RecommendedDealType;
            evaluation.RecommendedContractMonths = recommendation.RecommendedContractMonths;
            evaluation.RecommendedMinimumFee = recommendation.RecommendedMinimumMonthlyFee;
            evaluation.RecommendedSetupInvestment = recommendation.RecommendedSetupInvestment;
            evaluation.RecommendedAdSpend = recommendation.RecommendedAdSpend;
            evaluation.RecommendedTargetMer = recommendation.RecommendedTargetMer;
            evaluation.RecommendedTargetContributionMargin = recommendation.RecommendedTargetBrandContributionMargin;
            evaluation.RuleSnapshotJson = JsonSerializer.Serialize(ruleSet, Json);
            evaluation.InputSnapshotJson = JsonSerializer.Serialize(new { brand.Economics, evaluation.ProductMarketFit, evaluation.GrowthPotential, evaluation.OperationalReadiness }, Json);
            evaluation.RecommendationSnapshotJson = JsonSerializer.Serialize(recommendation, Json);
            evaluation.Conditions.AddRange(recommendation.RequiredConditions.Select(x => new PartnershipCondition { EvaluationId = evaluation.Id, Code = x.Code, Title = x.Title, Description = x.Description, Required = x.Required }));
        }
        brand.Evaluations.Add(evaluation);
        return brand;
    }

    private static void AddScenarios(BrandEvaluation evaluation, BrandEconomics e, GeneralSettings settings)
    {
        var variants = new[] { ("Temkinli", .82m, 1.08m, false), ("Beklenen", 1m, 1m, true), ("Büyüme", 1.28m, 1.18m, false) };
        foreach (var (name, revenueFactor, spendFactor, preferred) in variants)
        {
            var scenario = new Scenario { EvaluationId = evaluation.Id, Name = name, IsPreferred = preferred,
                MonthlyRevenue = decimal.Round(e.AverageMonthlyRevenue * revenueFactor, 2), GrossMarginRate = e.GrossMarginRate,
                AdSpend = decimal.Round(e.CurrentAdSpend * spendFactor, 2), ReturnRate = e.ReturnRate, AverageOrderValue = e.AverageOrderValue,
                NewCustomers = Math.Max(1, (int)(e.NewCustomers * revenueFactor)), VariableCostRate = e.VariableCostRate,
                OvoInternalMonthlyCost = evaluation.InternalMonthlyCost, MinimumMonthlyFee = evaluation.RecommendedMinimumFee,
                CommissionModel = evaluation.RecommendedDealType, RevenueShareRate = .045m, MonthlyRetainer = evaluation.RecommendedMinimumFee,
                BaselineRevenue = e.AverageMonthlyRevenue, IncrementalRate = .10m, ProfitShareRate = .18m,
                TargetBrandContributionMargin = settings.TargetBrandContributionMargin, SetupInvestment = evaluation.SetupInvestment,
                ContractMonths = evaluation.RecommendedContractMonths == 0 ? settings.DefaultContractMonths : evaluation.RecommendedContractMonths };
            scenario.ResultJson = JsonSerializer.Serialize(ScenarioCalculator.Calculate(scenario), Json);
            evaluation.Scenarios.Add(scenario);
        }
    }

    private static void AddCommercialHistory(Brand brand, BrandEvaluation evaluation)
    {
        var active = brand.Status == BrandStatus.Active; var paused = brand.Status == BrandStatus.Paused;
        var tiers = JsonSerializer.Serialize(new[] { new CommissionTier(0, 1_000_000, .06m), new CommissionTier(1_000_000, 3_000_000, .045m), new CommissionTier(3_000_000, null, .035m) }, Json);
        var deal = new Deal { BrandId = brand.Id, EvaluationId = evaluation.Id, Name = $"{brand.Name} Büyüme İş Ortaklığı",
            Status = active ? DealStatus.Active : paused ? DealStatus.Expired : DealStatus.Proposed,
            DealType = evaluation.RecommendedDealType, ContractMonths = evaluation.RecommendedContractMonths == 0 ? 24 : evaluation.RecommendedContractMonths,
            StartDate = active || paused ? new DateOnly(2026, 1, 1) : null, EndDate = paused ? new DateOnly(2026, 8, 31) : active ? new DateOnly(2027, 12, 31) : null,
            BaselineRevenue = brand.Economics!.AverageMonthlyRevenue, MonthlyRetainer = evaluation.RecommendedDealType is DealType.RetainerPlusRevenueShare or DealType.RetainerPlusIncrementalRevenueShare or DealType.FixedRetainer ? evaluation.RecommendedMinimumFee : 0,
            MinimumMonthlyFee = evaluation.RecommendedDealType == DealType.MinimumFeePlusRevenueShare ? evaluation.RecommendedMinimumFee : 0,
            RevenueShareRate = .045m, IncrementalRate = .10m, ProfitShareRate = .18m, CommissionTiersJson = tiers,
            SetupInvestment = evaluation.SetupInvestment, EstimatedMonthlyInternalCost = evaluation.InternalMonthlyCost,
            EvaluationSnapshotJson = evaluation.RecommendationSnapshotJson, RuleSnapshotJson = evaluation.RuleSnapshotJson,
            StatusReason = paused ? "Sezon sonu stok planlaması nedeniyle yeni dönem bekleniyor." : "" };
        brand.Deals.Add(deal);
        if (!active) return;
        var e = brand.Economics; var gross = decimal.Round(e.AverageMonthlyRevenue * 1.20m, 2);
        var performance = new MonthlyPerformance { BrandId = brand.Id, DealId = deal.Id, Year = 2026, Month = 8,
            GrossSales = gross, Vat = decimal.Round(gross / 6m, 2), Refunds = decimal.Round(gross * e.ReturnRate, 2),
            Cancellations = decimal.Round(gross * .012m, 2), Chargebacks = decimal.Round(gross * .0015m, 2),
            Orders = e.MonthlyOrders, Sessions = e.MonthlySessions, NewCustomers = e.NewCustomers, ReturningCustomers = e.ReturningCustomers,
            Cogs = decimal.Round(e.AverageMonthlyRevenue * e.CogsRate, 2), PaymentFees = decimal.Round(e.AverageMonthlyRevenue * .021m, 2),
            FulfillmentCosts = decimal.Round(e.MonthlyOrders * 38m, 2), ShippingSubsidy = decimal.Round(e.MonthlyOrders * 21m, 2),
            OtherVariableCosts = decimal.Round(e.AverageMonthlyRevenue * .012m, 2), MetaSpend = decimal.Round(e.CurrentAdSpend * .62m, 2),
            GoogleSpend = decimal.Round(e.CurrentAdSpend * .25m, 2), TikTokSpend = decimal.Round(e.CurrentAdSpend * .08m, 2),
            InfluencerSpend = decimal.Round(e.CurrentAdSpend * .05m, 2), Status = brand.Name switch { "Minoa Skin" => MonthlyPerformanceStatus.Paid, "Arven Home" => MonthlyPerformanceStatus.Locked, "Lodos Coffee" => MonthlyPerformanceStatus.UnderReview, _ => MonthlyPerformanceStatus.Approved },
            CommissionStatus = brand.Name == "Minoa Skin" ? CommissionStatus.Paid : CommissionStatus.Approved,
            PreparedBy = "partner@ovodigital.com", SubmittedAt = DateTimeOffset.UtcNow.AddDays(-8),
            ReviewedBy = brand.Name == "Lodos Coffee" ? "" : "admin@ovodigital.com", ApprovedAt = brand.Name == "Lodos Coffee" ? null : DateTimeOffset.UtcNow.AddDays(-6),
            LockedAt = brand.Name is "Minoa Skin" or "Arven Home" ? DateTimeOffset.UtcNow.AddDays(-5) : null };
        MonthlyPerformanceCalculator.Calculate(performance, deal); brand.MonthlyPerformances.Add(performance);
    }

    private static string Slug(string value) => value.ToLowerInvariant().Replace(" ", "-").Replace("ı", "i");
}
