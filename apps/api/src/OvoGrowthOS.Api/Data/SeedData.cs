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
        if (!await db.Brands.AnyAsync()) SeedPortfolio(db, ruleSet, settings);
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

    private static void SeedPortfolio(AppDbContext db, RuleSet ruleSet, GeneralSettings settings)
    {
        var luna = Create("Luna Jewelry", "Premium Takı", 1_000_000, .65m, 200_000, 1_600, .05m, 90, BrandStatus.Active, 5, 5, 4, ruleSet, settings);
        var mode = Create("Mode Atelier", "Moda", 5_000_000, .55m, 800_000, 2_200, .14m, 70, BrandStatus.Evaluation, 4, 4, 4, ruleSet, settings);
        var volt = Create("Volt Electronics", "Elektronik", 2_000_000, .18m, 100_000, 4_800, .08m, 25, BrandStatus.Negotiation, 3, 2, 3, ruleSet, settings);
        db.Brands.AddRange(luna, mode, volt);

        var evaluation = luna.Evaluations[0];
        var deal = new Deal { BrandId = luna.Id, EvaluationId = evaluation.Id, Name = "Luna Büyüme İş Ortaklığı",
            Status = DealStatus.Active, DealType = DealType.MinimumFeePlusRevenueShare, MinimumMonthlyFee = 45_000,
            RevenueShareRate = .05m, SetupInvestment = 250_000, EstimatedMonthlyInternalCost = 25_000,
            EvaluationSnapshotJson = evaluation.RecommendationSnapshotJson, RuleSnapshotJson = evaluation.RuleSnapshotJson };
        luna.Deals.Add(deal);
        var performance = new MonthlyPerformance { BrandId = luna.Id, DealId = deal.Id, Year = 2026, Month = 8,
            GrossSales = 1_080_000, Vat = 180_000, Refunds = 45_000, Orders = 640, Sessions = 45_000,
            NewCustomers = 390, ReturningCustomers = 250, Cogs = 300_000, PaymentFees = 22_000,
            FulfillmentCosts = 35_000, ShippingSubsidy = 15_000, MetaSpend = 130_000, GoogleSpend = 70_000,
            Status = MonthlyPerformanceStatus.Paid, CommissionStatus = CommissionStatus.Paid };
        MonthlyPerformanceCalculator.Calculate(performance, deal);
        luna.MonthlyPerformances.Add(performance);
    }

    private static Brand Create(string name, string industry, decimal revenue, decimal margin, decimal spend, decimal aov,
        decimal returns, int stockDays, BrandStatus status, int pmf, int growth, int operations, RuleSet ruleSet, GeneralSettings settings)
    {
        var brand = new Brand { Name = name, LegalName = $"{name} A.Ş.", Industry = industry, Status = status,
            BusinessModel = BusinessModel.Dtc, Platform = CommercePlatform.Shopify };
        brand.Economics = new BrandEconomics { BrandId = brand.Id, AverageMonthlyRevenue = revenue, RevenueConfidence = DataConfidence.Verified,
            GrossMarginRate = margin, GrossMarginConfidence = DataConfidence.Verified, CogsRate = 1 - margin, CogsConfidence = DataConfidence.Verified,
            CurrentAdSpend = spend, AdSpendConfidence = DataConfidence.Verified, AverageOrderValue = aov, AovConfidence = DataConfidence.Verified,
            ReturnRate = returns, ReturnRateConfidence = DataConfidence.Verified, StockCoverageDays = stockDays,
            StockCoverageConfidence = DataConfidence.ProvidedByBrand, MonthlyOrders = (int)(revenue / aov), MonthlySessions = 50_000,
            VariableCostRate = .08m, NewCustomers = 350, ReturningCustomers = 180, CurrentCac = spend / 350,
            CacConfidence = DataConfidence.Estimated, AverageCustomerLtv = aov * 2, LtvConfidence = DataConfidence.Estimated };
        var evaluation = new BrandEvaluation { BrandId = brand.Id, CurrentStep = 10, Status = EvaluationStatus.Analyzed, CreatedBy = "seed",
            ProductMarketFit = pmf, GrowthPotential = growth, OperationalReadiness = operations, CreativeCapability = 4,
            FounderCooperation = 4, DataMaturity = 3, InternalMonthlyCost = 25_000, SetupInvestment = 250_000,
            RuleSetId = ruleSet.Id, RuleSetVersion = ruleSet.Version, CompletedAt = DateTimeOffset.UtcNow };
        var recommendation = DealRecommendationEngine.Recommend(brand.Economics, evaluation, ruleSet, settings);
        evaluation.PartnershipScore = recommendation.PartnershipScore; evaluation.DataConfidenceScore = recommendation.DataConfidenceScore;
        evaluation.Decision = recommendation.Decision; evaluation.RecommendedDealType = recommendation.RecommendedDealType;
        evaluation.RecommendedMinimumFee = recommendation.RecommendedMinimumMonthlyFee;
        evaluation.RuleSnapshotJson = JsonSerializer.Serialize(ruleSet, Json);
        evaluation.InputSnapshotJson = JsonSerializer.Serialize(new { brand.Economics, evaluation.ProductMarketFit, evaluation.GrowthPotential, evaluation.OperationalReadiness }, Json);
        evaluation.RecommendationSnapshotJson = JsonSerializer.Serialize(recommendation, Json);
        brand.Evaluations.Add(evaluation);
        return brand;
    }
}
