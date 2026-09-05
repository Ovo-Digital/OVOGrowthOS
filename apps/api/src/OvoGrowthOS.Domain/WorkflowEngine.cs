using System.Text.Json;

namespace OvoGrowthOS.Domain;

public sealed record ScenarioCalculation(decimal CommissionableRevenue, decimal OvoFee, decimal EffectiveCommissionRate,
    decimal OvoGrossProfit, decimal OvoMargin, decimal BrandContributionProfit, decimal BrandContributionMargin,
    decimal Mer, decimal BreakEvenMer, decimal Cac, decimal PaybackMonths, decimal AllowableAdSpend,
    ScenarioRiskLevel RiskLevel, DecisionStatus ScenarioDecision);

public static class ScenarioCalculator
{
    public static ScenarioCalculation Calculate(Scenario s)
    {
        var commissionable = s.MonthlyRevenue * (1 - s.ReturnRate);
        var temporaryDeal = new Deal { BrandId = Guid.Empty, EvaluationId = s.EvaluationId, Name = s.Name,
            DealType = s.CommissionModel, MonthlyRetainer = s.MonthlyRetainer, MinimumMonthlyFee = s.MinimumMonthlyFee,
            RevenueShareRate = s.RevenueShareRate, BaselineRevenue = s.BaselineRevenue, IncrementalRate = s.IncrementalRate,
            ProfitShareRate = s.ProfitShareRate, CommissionTiersJson = s.CommissionTiersJson };
        var cogs = s.MonthlyRevenue * (1 - s.GrossMarginRate);
        var variable = s.MonthlyRevenue * s.VariableCostRate;
        var contributionBeforeOvo = s.MonthlyRevenue - cogs - variable - s.AdSpend;
        var commission = DealCommissionCalculator.Calculate(temporaryDeal, commissionable, contributionBeforeOvo);
        var finance = FinancialCalculator.Calculate(new(s.MonthlyRevenue, 0, 0, 0, cogs, variable, s.AdSpend, commission.FinalFee,
            s.AverageOrderValue == 0 ? 0 : (int)(s.MonthlyRevenue / s.AverageOrderValue), 0, s.NewCustomers));
        var breakEven = FinancialCalculator.BreakEvenMer(s.GrossMarginRate, s.VariableCostRate, commission.EffectiveRate, s.TargetBrandContributionMargin);
        var allowable = FinancialCalculator.AllowableAdSpend(s.MonthlyRevenue, s.GrossMarginRate, s.VariableCostRate, commission.EffectiveRate, s.TargetBrandContributionMargin);
        var ovoProfit = commission.FinalFee - s.OvoInternalMonthlyCost;
        var risk = finance.BrandContributionMargin < 0 || ovoProfit < 0 ? ScenarioRiskLevel.Critical
            : finance.BrandContributionMargin < s.TargetBrandContributionMargin ? ScenarioRiskLevel.High
            : FinancialCalculator.OvoMargin(commission.FinalFee, s.OvoInternalMonthlyCost) < .40m ? ScenarioRiskLevel.Medium : ScenarioRiskLevel.Low;
        var decision = risk == ScenarioRiskLevel.Critical ? DecisionStatus.Reject : risk >= ScenarioRiskLevel.High ? DecisionStatus.ConditionalAccept : DecisionStatus.Accept;
        return new(commissionable, commission.FinalFee, commission.EffectiveRate, ovoProfit,
            FinancialCalculator.OvoMargin(commission.FinalFee, s.OvoInternalMonthlyCost), finance.BrandContributionProfit,
            finance.BrandContributionMargin, finance.Mer, breakEven, finance.Cac,
            FinancialCalculator.PaybackMonths(s.SetupInvestment, ovoProfit), allowable, risk, decision);
    }
}

public sealed record DealComparisonResult(Guid DealId, string Name, decimal OvoFee, decimal EffectiveRate, decimal OvoGrossProfit,
    decimal OvoMargin, decimal BrandContributionProfit, decimal BrandContributionMargin, decimal SetupPayback,
    decimal BreakEvenMer, ScenarioRiskLevel Risk, decimal Score, IReadOnlyList<string> Reasons);

public static class DealComparisonEngine
{
    public static IReadOnlyList<DealComparisonResult> Compare(IEnumerable<Deal> deals, Scenario basis, GeneralSettings settings)
    {
        var results = new List<DealComparisonResult>();
        foreach (var deal in deals)
        {
            var scenario = Copy(basis, deal);
            var x = ScenarioCalculator.Calculate(scenario);
            var reasons = new List<string>();
            decimal score = 0;
            if (x.OvoGrossProfit > 0) { score += 25; reasons.Add("OVO hizmet maliyetini karşılıyor."); }
            if (x.BrandContributionMargin >= settings.TargetBrandContributionMargin) { score += 35; reasons.Add("Markaya hedeflenen katkı marjını bırakıyor."); }
            if (x.PaybackMonths > 0 && x.PaybackMonths <= 6) { score += 25; reasons.Add("Kurulum yatırımı altı ay içinde geri kazanılıyor."); }
            if (x.RiskLevel == ScenarioRiskLevel.Low) score += 15;
            results.Add(new(deal.Id, deal.Name, x.OvoFee, x.EffectiveCommissionRate, x.OvoGrossProfit, x.OvoMargin,
                x.BrandContributionProfit, x.BrandContributionMargin, x.PaybackMonths, x.BreakEvenMer, x.RiskLevel, score, reasons));
        }
        return results.OrderByDescending(x => x.Score).ToList();
    }
    private static Scenario Copy(Scenario b, Deal d) => new() { EvaluationId = b.EvaluationId, Name = d.Name,
        MonthlyRevenue = b.MonthlyRevenue, GrossMarginRate = b.GrossMarginRate, AdSpend = b.AdSpend, ReturnRate = b.ReturnRate,
        AverageOrderValue = b.AverageOrderValue, NewCustomers = b.NewCustomers, VariableCostRate = b.VariableCostRate,
        OvoInternalMonthlyCost = d.EstimatedMonthlyInternalCost, MinimumMonthlyFee = d.MinimumMonthlyFee,
        CommissionModel = d.DealType, RevenueShareRate = d.RevenueShareRate, MonthlyRetainer = d.MonthlyRetainer,
        BaselineRevenue = d.BaselineRevenue, IncrementalRate = d.IncrementalRate, ProfitShareRate = d.ProfitShareRate,
        CommissionTiersJson = d.CommissionTiersJson, TargetBrandContributionMargin = b.TargetBrandContributionMargin,
        SetupInvestment = d.SetupInvestment, ContractMonths = d.ContractMonths };
}

public static class MonthlyPerformanceCalculator
{
    public static CommissionResult Calculate(MonthlyPerformance p, Deal deal)
    {
        p.NetRevenue = p.GrossSales - p.Vat - p.Refunds - p.Cancellations - p.Chargebacks;
        p.CommissionableRevenue = p.GrossSales - p.Vat - p.Refunds - p.Cancellations - p.Chargebacks - p.CustomerPaidShipping - p.GiftCardTopups;
        p.TotalAdSpend = p.MetaSpend + p.GoogleSpend + p.TikTokSpend + p.InfluencerSpend + p.OtherAdSpend;
        var variable = p.PaymentFees + p.FulfillmentCosts + p.ShippingSubsidy + p.OtherVariableCosts;
        p.GrossProfit = p.NetRevenue - p.Cogs;
        p.ContributionBeforeMarketing = p.GrossProfit - variable;
        p.ContributionBeforeOvo = p.ContributionBeforeMarketing - p.TotalAdSpend;
        var adjustment = p.Adjustments.Sum(x => x.Amount);
        var commission = DealCommissionCalculator.Calculate(deal, p.CommissionableRevenue, p.ContributionBeforeOvo, adjustment);
        p.OvoFee = commission.FinalFee;
        p.BrandContributionProfit = p.ContributionBeforeOvo - p.OvoFee;
        p.Aov = FinancialCalculator.Ratio(p.NetRevenue, p.Orders);
        p.ConversionRate = FinancialCalculator.Ratio(p.Orders, p.Sessions);
        p.Mer = FinancialCalculator.Ratio(p.NetRevenue, p.TotalAdSpend);
        p.Cac = FinancialCalculator.Ratio(p.TotalAdSpend, p.NewCustomers);
        p.ReturnRate = FinancialCalculator.Ratio(p.Refunds, p.GrossSales);
        p.OvoInternalCost = deal.EstimatedMonthlyInternalCost;
        p.OvoGrossProfit = p.OvoFee - p.OvoInternalCost;
        p.OvoMargin = FinancialCalculator.OvoMargin(p.OvoFee, p.OvoInternalCost);
        p.CommissionBreakdownJson = JsonSerializer.Serialize(commission, DealCommissionCalculator.JsonOptions);
        p.UpdatedAt = DateTimeOffset.UtcNow;
        return commission;
    }
}

public static class MonthlyCloseWorkflow
{
    public static bool CanEdit(MonthlyPerformanceStatus status) => status < MonthlyPerformanceStatus.Locked;
    public static bool CanAdjust(MonthlyPerformanceStatus status) => status < MonthlyPerformanceStatus.Locked;
    public static bool CanUnlock(MonthlyPerformanceStatus status) => status == MonthlyPerformanceStatus.Locked;
    public static bool CanTransition(MonthlyPerformanceStatus from, MonthlyPerformanceStatus to) =>
        (from, to) is (MonthlyPerformanceStatus.Draft, MonthlyPerformanceStatus.UnderReview)
            or (MonthlyPerformanceStatus.UnderReview, MonthlyPerformanceStatus.Approved)
            or (MonthlyPerformanceStatus.Approved, MonthlyPerformanceStatus.Locked)
            or (MonthlyPerformanceStatus.Locked, MonthlyPerformanceStatus.Invoiced)
            or (MonthlyPerformanceStatus.Invoiced, MonthlyPerformanceStatus.Paid);
}

public static class PortfolioRiskCalculator
{
    public static decimal LargestShare(IEnumerable<decimal> values)
    {
        var list = values.Where(x => x > 0).ToList(); var total = list.Sum();
        return total == 0 ? 0 : decimal.Round(list.Max() / total, 4);
    }
    public static decimal TopThreeShare(IEnumerable<decimal> values)
    {
        var list = values.Where(x => x > 0).OrderByDescending(x => x).ToList(); var total = list.Sum();
        return total == 0 ? 0 : decimal.Round(list.Take(3).Sum() / total, 4);
    }
}

public static class RuleSetVersioning
{
    public static void EnsureEditable(RuleSet ruleSet)
    {
        if (ruleSet.Status != RuleSetStatus.Draft) throw new InvalidOperationException("Yayımlanmış veya arşivlenmiş kural setleri değiştirilemez.");
    }
    public static RuleSet Clone(RuleSet source) => new() { Name = source.Name, Description = source.Description,
        Version = source.Version + 1, Status = RuleSetStatus.Draft, Rules = source.Rules.Select(x => new Rule { Name = x.Name,
            Description = x.Description, Category = x.Category, Field = x.Field, Operator = x.Operator, Value = x.Value,
            SecondaryValue = x.SecondaryValue, Severity = x.Severity, Weight = x.Weight, Enabled = x.Enabled,
            RecommendationEffect = x.RecommendationEffect }).ToList() };
}
