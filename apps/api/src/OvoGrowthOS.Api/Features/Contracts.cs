using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record EvaluationDraftRequest(Guid BrandId, int CurrentStep, EvaluationStatus Status,
    decimal AverageMonthlyRevenue, DataConfidence RevenueConfidence, decimal GrossMarginRate, DataConfidence GrossMarginConfidence,
    decimal CogsRate, DataConfidence CogsConfidence, decimal AverageOrderValue, DataConfidence AovConfidence,
    decimal ReturnRate, DataConfidence ReturnRateConfidence, decimal CurrentAdSpend, DataConfidence AdSpendConfidence,
    decimal CurrentCac, DataConfidence CacConfidence, decimal AverageCustomerLtv, DataConfidence LtvConfidence,
    int StockCoverageDays, DataConfidence StockCoverageConfidence, int MonthlyOrders, int MonthlySessions,
    int NewCustomers, int ReturningCustomers, decimal VariableCostRate, int ProductMarketFit, int GrowthPotential,
    int OperationalReadiness, int CreativeCapability, int FounderCooperation, int DataMaturity,
    decimal InternalMonthlyCost, decimal SetupInvestment);

public sealed record RuleSetRequest(string Name, string Description);
public sealed record RuleRequest(string Name, string Description, RuleCategory Category, RuleField Field,
    RuleOperator Operator, decimal Value, decimal? SecondaryValue, RuleSeverity Severity, decimal Weight,
    bool Enabled, RecommendationEffect RecommendationEffect);
public sealed record ScenarioRequest(string Name, decimal MonthlyRevenue, decimal GrossMarginRate, decimal AdSpend,
    decimal ReturnRate, decimal AverageOrderValue, int NewCustomers, decimal VariableCostRate,
    decimal OvoInternalMonthlyCost, decimal MinimumMonthlyFee, DealType CommissionModel, decimal RevenueShareRate,
    decimal MonthlyRetainer, decimal BaselineRevenue, decimal IncrementalRate, decimal ProfitShareRate,
    List<CommissionTier> CommissionTiers, decimal TargetBrandContributionMargin, decimal SetupInvestment, int ContractMonths);
public sealed record DealRequest(string Name, DealType DealType, int ContractMonths, decimal BaselineRevenue,
    DateOnly? BaselinePeriodStart, DateOnly? BaselinePeriodEnd, BaselineCalculationMethod BaselineCalculationMethod,
    decimal MonthlyRetainer, decimal MinimumMonthlyFee, decimal RevenueShareRate, decimal IncrementalRate,
    decimal ProfitShareRate, List<CommissionTier> CommissionTiers, decimal SetupInvestment, decimal EstimatedMonthlyInternalCost);
public sealed record PerformanceRequest(Guid BrandId, Guid DealId, int Year, int Month, decimal GrossSales, decimal Vat,
    decimal Refunds, decimal Cancellations, decimal Chargebacks, decimal CustomerPaidShipping, decimal GiftCardTopups,
    int Orders, int Sessions, int NewCustomers, int ReturningCustomers, decimal Cogs, decimal PaymentFees,
    decimal FulfillmentCosts, decimal ShippingSubsidy, decimal OtherVariableCosts, decimal MetaSpend,
    decimal GoogleSpend, decimal TikTokSpend, decimal InfluencerSpend, decimal OtherAdSpend);
public sealed record TransitionRequest(string? Reason = null);
public sealed record RenameRequest(string Name);
public sealed record AdjustmentRequest(decimal Amount, string Reason);
public sealed record SettingsRequest(string DefaultCurrency, decimal DefaultVatRate, int DefaultContractMonths,
    decimal DefaultSetupInvestment, decimal TargetOvoGrossMargin, decimal TargetBrandContributionMargin,
    decimal MinimumFeeMultiplier, decimal ExistingRevenueThreshold, decimal ConcentrationRiskThreshold, Guid? DefaultRuleSetId);
