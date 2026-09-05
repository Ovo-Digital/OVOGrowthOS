using System.Text.Json;

namespace OvoGrowthOS.Domain;

public static class CommissionCalculator
{
    public static decimal Flat(decimal revenue, decimal rate) => Money(revenue * rate);
    public static decimal Tiered(decimal revenue, IEnumerable<CommissionTier> tiers) => TieredBreakdown(revenue, tiers).Sum(x => x.Fee);
    public static IReadOnlyList<CommissionTierResult> TieredBreakdown(decimal revenue, IEnumerable<CommissionTier> tiers)
    {
        if (revenue <= 0) return [];
        var results = new List<CommissionTierResult>();
        foreach (var tier in tiers.OrderBy(x => x.LowerBound))
        {
            if (revenue <= tier.LowerBound) break;
            var amount = Math.Min(revenue, tier.UpperBound ?? revenue) - tier.LowerBound;
            if (amount > 0) results.Add(new(tier.LowerBound, tier.UpperBound, tier.Rate, amount, Money(amount * tier.Rate)));
        }
        return results;
    }
    public static decimal MinimumFee(decimal calculatedFee, decimal minimumFee) => Math.Max(calculatedFee, minimumFee);
    public static decimal RetainerPlusShare(decimal revenue, decimal retainer, decimal rate) => Money(retainer + revenue * rate);
    public static decimal Incremental(decimal revenue, decimal baseline, decimal retainer, decimal rate) => Money(retainer + Math.Max(revenue - baseline, 0) * rate);
    public static decimal ContributionProfitShare(decimal contributionProfit, decimal retainer, decimal rate) => Money(retainer + Math.Max(contributionProfit, 0) * rate);
    public static decimal EffectiveRate(decimal fee, decimal revenue) => Ratio(fee, revenue);
    private static decimal Money(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    private static decimal Ratio(decimal numerator, decimal denominator) => denominator == 0 ? 0 : decimal.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);
}

public sealed record CommissionTierResult(decimal LowerBound, decimal? UpperBound, decimal Rate, decimal RevenueAmount, decimal Fee);
public sealed record CommissionResult(decimal BaseRetainer, decimal CalculatedShare, decimal MinimumFee, decimal Adjustments,
    decimal FinalFee, decimal EffectiveRate, IReadOnlyList<CommissionTierResult> Tiers);

public static class DealCommissionCalculator
{
    public static CommissionResult Calculate(Deal deal, decimal commissionableRevenue, decimal contributionBeforeOvo, decimal adjustments = 0)
    {
        var tiers = DeserializeTiers(deal.CommissionTiersJson);
        decimal share = deal.DealType switch
        {
            DealType.FlatRevenueShare => CommissionCalculator.Flat(commissionableRevenue, deal.RevenueShareRate),
            DealType.TieredRevenueShare => CommissionCalculator.Tiered(commissionableRevenue, tiers),
            DealType.RetainerPlusRevenueShare => CommissionCalculator.Flat(commissionableRevenue, deal.RevenueShareRate),
            DealType.MinimumFeePlusRevenueShare => tiers.Count > 0 ? CommissionCalculator.Tiered(commissionableRevenue, tiers) : CommissionCalculator.Flat(commissionableRevenue, deal.RevenueShareRate),
            DealType.IncrementalRevenueShare or DealType.RetainerPlusIncrementalRevenueShare => CommissionCalculator.Flat(Math.Max(commissionableRevenue - deal.BaselineRevenue, 0), deal.IncrementalRate),
            DealType.ContributionProfitShare => CommissionCalculator.Flat(Math.Max(contributionBeforeOvo, 0), deal.ProfitShareRate),
            _ => 0
        };
        var baseRetainer = deal.DealType is DealType.RetainerPlusRevenueShare or DealType.RetainerPlusIncrementalRevenueShare or DealType.ContributionProfitShare or DealType.FixedRetainer ? deal.MonthlyRetainer : 0;
        var calculated = baseRetainer + share;
        var final = deal.DealType == DealType.MinimumFeePlusRevenueShare ? Math.Max(calculated, deal.MinimumMonthlyFee) : calculated;
        final += adjustments;
        return new(baseRetainer, share, deal.MinimumMonthlyFee, adjustments, final,
            CommissionCalculator.EffectiveRate(final, commissionableRevenue), CommissionCalculator.TieredBreakdown(commissionableRevenue, tiers));
    }
    public static IReadOnlyList<CommissionTier> DeserializeTiers(string json) => JsonSerializer.Deserialize<List<CommissionTier>>(json, JsonOptions) ?? [];
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class FinancialCalculator
{
    public static FinancialResult Calculate(FinancialInputs x)
    {
        var net = x.GrossSales - x.Refunds - x.Cancellations - x.Chargebacks;
        var grossProfit = net - x.Cogs;
        var beforeMarketing = grossProfit - x.VariableCosts;
        var beforeOvo = beforeMarketing - x.AdSpend;
        var brandProfit = beforeOvo - x.OvoFee;
        return new(net, grossProfit, beforeMarketing, beforeOvo, brandProfit, Ratio(brandProfit, net),
            Ratio(net, x.AdSpend), Ratio(x.AdSpend, x.NewCustomers), Ratio(net, x.Orders), Ratio(x.Orders, x.Sessions));
    }
    public static decimal BreakEvenMer(decimal grossMarginRate, decimal variableCostRate, decimal ovoEffectiveRate, decimal targetContributionMargin)
    {
        var allowable = grossMarginRate - variableCostRate - ovoEffectiveRate - targetContributionMargin;
        return allowable <= 0 ? 0 : decimal.Round(1 / allowable, 4);
    }
    public static decimal AllowableAdSpend(decimal netRevenue, decimal grossMarginRate, decimal variableCostRate, decimal ovoEffectiveRate, decimal targetContributionMargin) =>
        Math.Max(0, netRevenue * (grossMarginRate - variableCostRate - ovoEffectiveRate - targetContributionMargin));
    public static decimal OvoMargin(decimal fee, decimal internalCost) => Ratio(fee - internalCost, fee);
    public static decimal PaybackMonths(decimal setupInvestment, decimal monthlyOvoProfit) => monthlyOvoProfit <= 0 ? 0 : Ratio(setupInvestment, monthlyOvoProfit);
    public static decimal Ratio(decimal numerator, decimal denominator) => denominator == 0 ? 0 : decimal.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);
}

public static class PartnershipScoreCalculator
{
    public static decimal Calculate(decimal grossMarginRate, int growth, int operations, int productMarketFit, int creative, int founder, int data)
    {
        var unitEconomics = Math.Clamp(grossMarginRate / .60m, 0, 1) * 30;
        decimal Scale(int value, int weight) => Math.Clamp(value, 0, 5) / 5m * weight;
        return decimal.Round(unitEconomics + Scale(growth, 15) + Scale(operations, 15) + Scale(productMarketFit, 15) + Scale(creative, 10) + Scale(founder, 10) + Scale(data, 5), 1);
    }
}

public static class DataConfidenceCalculator
{
    public static decimal Calculate(params DataConfidence[] values) => values.Length == 0 ? 0 : decimal.Round(values.Average(Score), 1);
    public static decimal Score(DataConfidence confidence) => confidence switch { DataConfidence.Verified => 100, DataConfidence.ProvidedByBrand => 75, DataConfidence.Estimated => 40, _ => 0 };
    public static decimal Calculate(BrandEconomics e) => Calculate(e.RevenueConfidence, e.GrossMarginConfidence, e.CogsConfidence,
        e.AovConfidence, e.ReturnRateConfidence, e.AdSpendConfidence, e.CacConfidence, e.LtvConfidence, e.StockCoverageConfidence);
}

public sealed record RuleContext(decimal GrossMargin, decimal ReturnRate, decimal StockCoverageDays, decimal FounderCooperation,
    decimal OperationalReadiness, decimal ProductMarketFit, decimal CurrentAdSpend, decimal AverageMonthlyRevenue, decimal PartnershipScore);
public sealed record MatchedRule(Guid RuleId, string Name, RuleSeverity Severity, decimal Weight, RecommendationEffect Effect, string Description);

public static class RuleEvaluator
{
    public static IReadOnlyList<MatchedRule> Evaluate(IEnumerable<Rule> rules, RuleContext context)
    {
        var matches = rules.Where(x => x.Enabled && Match(Value(x.Field, context), x.Operator, x.Value, x.SecondaryValue))
            .Select(x => new MatchedRule(x.Id, x.Name, x.Severity, x.Weight, x.RecommendationEffect, x.Description)).ToList();
        if (context.GrossMargin < .30m && context.CurrentAdSpend == 0 && context.ProductMarketFit <= 2)
            matches.Add(new(Guid.Empty, "Düşük marj, reklam harcaması yok ve ürün-pazar uyumu zayıf", RuleSeverity.Critical, decimal.MaxValue, RecommendationEffect.Reject, "Birden fazla kritik riski birlikte değerlendiren ret kuralı."));
        return matches.OrderByDescending(x => x.Severity).ThenByDescending(x => x.Weight).ThenBy(x => x.RuleId).ToList();
    }
    private static decimal Value(RuleField field, RuleContext x) => field switch
    {
        RuleField.GrossMargin => x.GrossMargin, RuleField.ReturnRate => x.ReturnRate,
        RuleField.StockCoverageDays => x.StockCoverageDays, RuleField.FounderCooperation => x.FounderCooperation,
        RuleField.OperationalReadiness => x.OperationalReadiness, RuleField.ProductMarketFit => x.ProductMarketFit,
        RuleField.CurrentAdSpend => x.CurrentAdSpend, RuleField.AverageMonthlyRevenue => x.AverageMonthlyRevenue, _ => x.PartnershipScore
    };
    private static bool Match(decimal actual, RuleOperator op, decimal value, decimal? secondary) => op switch
    {
        RuleOperator.Equal => actual == value, RuleOperator.NotEqual => actual != value,
        RuleOperator.GreaterThan => actual > value, RuleOperator.GreaterThanOrEqual => actual >= value,
        RuleOperator.LessThan => actual < value, RuleOperator.LessThanOrEqual => actual <= value,
        RuleOperator.Between => secondary.HasValue && actual >= value && actual < secondary.Value, _ => false
    };
}

public static class DealRecommendationEngine
{
    public static RecommendationResult Recommend(BrandEconomics e, BrandEvaluation evaluation, RuleSet ruleSet, GeneralSettings settings)
    {
        var missing = Missing(e);
        var confidence = DataConfidenceCalculator.Calculate(e);
        if (missing.Count > 0)
            return new(DecisionStatus.NeedMoreData, 0, confidence, DealType.FixedRetainer, settings.DefaultContractMonths, 0,
                evaluation.SetupInvestment, 0, 0, settings.TargetBrandContributionMargin, "Kritik bilgiler bekleniyor", [], [], [],
                ["Karar için gerekli finansal bilgiler eksik."], [], missing);

        var score = PartnershipScoreCalculator.Calculate(e.GrossMarginRate, evaluation.GrowthPotential, evaluation.OperationalReadiness,
            evaluation.ProductMarketFit, evaluation.CreativeCapability, evaluation.FounderCooperation, evaluation.DataMaturity);
        var matches = RuleEvaluator.Evaluate(ruleSet.Rules, new(e.GrossMarginRate, e.ReturnRate, e.StockCoverageDays,
            evaluation.FounderCooperation, evaluation.OperationalReadiness, evaluation.ProductMarketFit, e.CurrentAdSpend, e.AverageMonthlyRevenue, score));
        var risks = matches.Where(x => x.Severity >= RuleSeverity.Warning).Select(x => x.Name).ToList();
        var positives = new List<string>();
        if (e.GrossMarginRate >= .50m) positives.Add("Sağlıklı brüt kâr marjı performansa dayalı fiyatlamayı destekliyor.");
        if (evaluation.ProductMarketFit >= 4) positives.Add("Ürün-pazar uyumu güçlü.");
        if (e.StockCoverageDays >= 60) positives.Add("Stok yeterliliği büyümeyi destekliyor.");
        var reject = matches.Any(x => x.Effect == RecommendationEffect.Reject) || score < settings.MinimumPartnershipScore;
        var decision = reject ? DecisionStatus.Reject : confidence < settings.MinimumDataConfidenceScore ? DecisionStatus.NeedMoreData : score < settings.ConditionalPartnershipScore || matches.Any(x => x.Severity >= RuleSeverity.High) ? DecisionStatus.ConditionalAccept : DecisionStatus.Accept;
        var effect = matches.FirstOrDefault(x => x.Effect != RecommendationEffect.None)?.Effect ?? RecommendationEffect.None;
        var deal = effect switch
        {
            RecommendationEffect.PureRevenueShareNotAllowed or RecommendationEffect.PreferRetainerLowShare => DealType.RetainerPlusRevenueShare,
            RecommendationEffect.AllowTieredHybrid => DealType.MinimumFeePlusRevenueShare,
            RecommendationEffect.AllowFourToSixPercent or RecommendationEffect.AllowFiveToEightPercent => DealType.TieredRevenueShare,
            _ when e.AverageMonthlyRevenue >= settings.ExistingRevenueThreshold => DealType.RetainerPlusIncrementalRevenueShare,
            _ => DealType.MinimumFeePlusRevenueShare
        };
        var minimumFee = decimal.Round(evaluation.InternalMonthlyCost * settings.MinimumFeeMultiplier, 2);
        var effectiveRate = e.AverageMonthlyRevenue == 0 ? 0 : minimumFee / e.AverageMonthlyRevenue;
        var targetMer = FinancialCalculator.BreakEvenMer(e.GrossMarginRate, e.VariableCostRate, effectiveRate, settings.TargetBrandContributionMargin);
        var conditions = Conditions(e, evaluation);
        return new(decision, score, confidence, deal, settings.DefaultContractMonths, minimumFee,
            evaluation.SetupInvestment == 0 ? settings.DefaultSetupInvestment : evaluation.SetupInvestment,
            Math.Max(e.CurrentAdSpend, settings.MinimumRecommendedAdSpend), targetMer, settings.TargetBrandContributionMargin,
            Describe(deal), conditions, positives, risks, matches.Select(x => x.Description).Where(x => x.Length > 0).ToList(),
            [$"Asgari ücret çarpanı: {settings.MinimumFeeMultiplier}", $"Kural seti: {ruleSet.Name} v{ruleSet.Version}"], missing);
    }
    private static List<string> Missing(BrandEconomics e)
    {
        var result = new List<string>();
        if (e.AverageMonthlyRevenue <= 0 || e.RevenueConfidence == DataConfidence.Unknown) result.Add("Aylık ciro");
        if (e.GrossMarginRate <= 0 || e.GrossMarginConfidence == DataConfidence.Unknown) result.Add("Brüt kâr marjı");
        if (e.CogsRate <= 0 || e.CogsConfidence == DataConfidence.Unknown) result.Add("Ürün maliyeti oranı");
        if (e.AverageOrderValue <= 0 || e.AovConfidence == DataConfidence.Unknown) result.Add("Ortalama sepet tutarı");
        if (e.CurrentAdSpend < 0 || e.AdSpendConfidence == DataConfidence.Unknown) result.Add("Reklam harcaması");
        if (e.ReturnRate < 0 || e.ReturnRateConfidence == DataConfidence.Unknown) result.Add("İade oranı");
        if (e.StockCoverageDays <= 0 || e.StockCoverageConfidence == DataConfidence.Unknown) result.Add("Stok yeterliliği");
        return result;
    }
    private static IReadOnlyList<StructuredCondition> Conditions(BrandEconomics e, BrandEvaluation v)
    {
        var result = new List<StructuredCondition> {
            new("MIN_MEDIA_BUDGET", "Asgari aylık medya bütçesi", "Kararlaştırılan aylık medya bütçesi korunmalı."),
            new("ANALYTICS_ACCESS", "Analiz araçlarına erişim", "OVO'ya analiz araçları için gerekli erişimler verilmeli."),
            new("COMMERCE_ADMIN", "E-ticaret yönetim erişimi", "Shopify, GrandNode veya kullanılan e-ticaret yönetim paneline erişim verilmeli."),
            new("META_ACCESS", "Meta Business erişimi", "Kararlaştırılan Meta Business ve reklam hesabı erişimleri verilmeli."),
            new("GOOGLE_ADS_ACCESS", "Google Ads erişimi", "Kararlaştırılan Google Ads erişimleri verilmeli."),
            new("MERCHANT_CENTER_ACCESS", "Merchant Center erişimi", "Kararlaştırılan Merchant Center erişimleri verilmeli."),
            new("MONTHLY_REPORTING", "Aylık finansal raporlama", "Kontrol edilmiş aylık finansal bilgiler sağlanmalı.") };
        if (e.StockCoverageDays < 60) result.Add(new("STOCK_60", "En az 60 günlük stok", "En az 60 günlük kullanılabilir stok korunmalı."));
        if (e.GrossMarginConfidence != DataConfidence.Verified) result.Add(new("MARGIN_VERIFY", "Marj doğrulaması", "Ürün bazındaki brüt kâr marjı doğrulanmalı."));
        if (v.CreativeCapability < 4) result.Add(new("WEEKLY_CREATIVE", "Haftalık içerik üretimi", "Onaylanan reklam içerikleri her hafta teslim edilmeli."));
        return result;
    }
    private static string Describe(DealType type) => type switch
    {
        DealType.TieredRevenueShare => "Ciro arttıkça değişen kademeli gelir payı",
        DealType.RetainerPlusIncrementalRevenueShare => "Aylık sabit ücret ve mevcut cironun üzerindeki büyümeden pay",
        DealType.RetainerPlusRevenueShare => "Aylık sabit ücret ve düşük oranlı gelir payı",
        _ => "Asgari aylık ücret ve gelir payı"
    };
}
