namespace OvoGrowthOS.Domain;

public enum BrandStatus { Lead, Evaluation, Negotiation, Active, Paused, Rejected, Closed }
public enum BusinessModel { Dtc, Retail, Marketplace, Hybrid }
public enum CommercePlatform { Shopify, GrandNode, Custom, Other }
public enum DecisionStatus { Accept, ConditionalAccept, NeedMoreData, Reject }
public enum EvaluationStatus { Draft, InProgress, ReadyForAnalysis, Analyzed, Approved, Rejected, Archived }
public enum DealType { FlatRevenueShare, TieredRevenueShare, RetainerPlusRevenueShare, MinimumFeePlusRevenueShare, IncrementalRevenueShare, RetainerPlusIncrementalRevenueShare, ContributionProfitShare, FixedRetainer }
public enum DealStatus { Draft, InternalReview, Proposed, Negotiation, Accepted, Rejected, Active, Expired, Terminated }
public enum BaselineCalculationMethod { Manual, Trailing3MonthAverage, Trailing6MonthAverage, Trailing12MonthAverage }
public enum DataConfidence { Verified, ProvidedByBrand, Estimated, Unknown }
public enum RuleSetStatus { Draft, Published, Archived }
public enum RuleCategory { Financial, Marketing, Operations, Growth, Risk, Commission, Contract, Score, Recommendation }
public enum RuleField { GrossMargin, ReturnRate, StockCoverageDays, FounderCooperation, OperationalReadiness, ProductMarketFit, CurrentAdSpend, AverageMonthlyRevenue, PartnershipScore }
public enum RuleOperator { Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Between }
public enum RuleSeverity { Info, Warning, High, Critical }
public enum RecommendationEffect { None, PureRevenueShareNotAllowed, PreferRetainerLowShare, AllowTieredHybrid, AllowFourToSixPercent, AllowFiveToEightPercent, Reject }
public enum ConditionStatus { Pending, Satisfied, Waived }
public enum ScenarioRiskLevel { Low, Medium, High, Critical }
public enum MonthlyPerformanceStatus { Draft, UnderReview, Approved, Locked, Invoiced, Paid }
public enum CommissionStatus { Draft, Approved, Invoiced, Paid }

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required string Role { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public int TokenVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Brand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string LegalName { get; set; } = "";
    public string Website { get; set; } = "";
    public string Country { get; set; } = "TR";
    public string Currency { get; set; } = "TRY";
    public string Industry { get; set; } = "";
    public string SubIndustry { get; set; } = "";
    public BusinessModel BusinessModel { get; set; }
    public CommercePlatform Platform { get; set; }
    public BrandStatus Status { get; set; } = BrandStatus.Lead;
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public BrandEconomics? Economics { get; set; }
    public List<BrandEvaluation> Evaluations { get; set; } = [];
    public List<Deal> Deals { get; set; } = [];
    public List<MonthlyPerformance> MonthlyPerformances { get; set; } = [];
}

public sealed class BrandEconomics
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public decimal AverageMonthlyRevenue { get; set; }
    public DataConfidence RevenueConfidence { get; set; }
    public decimal GrossMarginRate { get; set; }
    public DataConfidence GrossMarginConfidence { get; set; }
    public decimal CogsRate { get; set; }
    public DataConfidence CogsConfidence { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DataConfidence AovConfidence { get; set; }
    public int MonthlyOrders { get; set; }
    public int MonthlySessions { get; set; }
    public decimal ReturnRate { get; set; }
    public DataConfidence ReturnRateConfidence { get; set; }
    public decimal CancellationRate { get; set; }
    public decimal ChargebackRate { get; set; }
    public decimal VariableCostRate { get; set; }
    public decimal CurrentAdSpend { get; set; }
    public DataConfidence AdSpendConfidence { get; set; }
    public decimal CurrentCac { get; set; }
    public DataConfidence CacConfidence { get; set; }
    public decimal AverageCustomerLtv { get; set; }
    public DataConfidence LtvConfidence { get; set; }
    public int NewCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public int StockCoverageDays { get; set; }
    public DataConfidence StockCoverageConfidence { get; set; }
}

public sealed class BrandEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft;
    public int CurrentStep { get; set; } = 1;
    public string CreatedBy { get; set; } = "";
    public int ProductMarketFit { get; set; }
    public int GrowthPotential { get; set; }
    public int OperationalReadiness { get; set; }
    public int CreativeCapability { get; set; }
    public int FounderCooperation { get; set; }
    public int DataMaturity { get; set; }
    public decimal InternalMonthlyCost { get; set; }
    public decimal SetupInvestment { get; set; }
    public decimal PartnershipScore { get; set; }
    public decimal DataConfidenceScore { get; set; }
    public DecisionStatus Decision { get; set; }
    public DealType RecommendedDealType { get; set; }
    public int RecommendedContractMonths { get; set; }
    public decimal RecommendedMinimumFee { get; set; }
    public decimal RecommendedSetupInvestment { get; set; }
    public decimal RecommendedAdSpend { get; set; }
    public decimal RecommendedTargetMer { get; set; }
    public decimal RecommendedTargetContributionMargin { get; set; }
    public Guid? RuleSetId { get; set; }
    public int RuleSetVersion { get; set; }
    public string RuleSnapshotJson { get; set; } = "{}";
    public string CalculationSnapshotJson { get; set; } = "{}";
    public string InputSnapshotJson { get; set; } = "{}";
    public string RecommendationSnapshotJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<PartnershipCondition> Conditions { get; set; } = [];
    public List<Scenario> Scenarios { get; set; } = [];
}

public sealed class RuleSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public int Version { get; set; } = 1;
    public RuleSetStatus Status { get; set; } = RuleSetStatus.Draft;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public List<Rule> Rules { get; set; } = [];
}

public sealed class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleSetId { get; set; }
    public RuleSet? RuleSet { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public RuleCategory Category { get; set; }
    public RuleField Field { get; set; }
    public RuleOperator Operator { get; set; }
    public decimal Value { get; set; }
    public decimal? SecondaryValue { get; set; }
    public RuleSeverity Severity { get; set; }
    public decimal Weight { get; set; }
    public bool Enabled { get; set; } = true;
    public RecommendationEffect RecommendationEffect { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PartnershipCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EvaluationId { get; set; }
    public Guid? DealId { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = "";
    public bool Required { get; set; } = true;
    public ConditionStatus Status { get; set; } = ConditionStatus.Pending;
    public string ResolutionReason { get; set; } = "";
    public string EvidenceUrl { get; set; } = "";
    public string ResolvedBy { get; set; } = "";
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class Scenario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EvaluationId { get; set; }
    public string Name { get; set; } = "Expected";
    public bool IsPreferred { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal GrossMarginRate { get; set; }
    public decimal AdSpend { get; set; }
    public decimal ReturnRate { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int NewCustomers { get; set; }
    public decimal VariableCostRate { get; set; }
    public decimal OvoInternalMonthlyCost { get; set; }
    public decimal MinimumMonthlyFee { get; set; }
    public DealType CommissionModel { get; set; }
    public decimal RevenueShareRate { get; set; }
    public decimal MonthlyRetainer { get; set; }
    public decimal BaselineRevenue { get; set; }
    public decimal IncrementalRate { get; set; }
    public decimal ProfitShareRate { get; set; }
    public string CommissionTiersJson { get; set; } = "[]";
    public decimal TargetBrandContributionMargin { get; set; }
    public decimal SetupInvestment { get; set; }
    public int ContractMonths { get; set; } = 24;
    public string ResultJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Deal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public Guid EvaluationId { get; set; }
    public required string Name { get; set; }
    public DealStatus Status { get; set; } = DealStatus.Draft;
    public DealType DealType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? RenewalOfDealId { get; set; }
    public string StatusReason { get; set; } = "";
    public int ContractMonths { get; set; } = 24;
    public decimal BaselineRevenue { get; set; }
    public DateOnly? BaselinePeriodStart { get; set; }
    public DateOnly? BaselinePeriodEnd { get; set; }
    public BaselineCalculationMethod BaselineCalculationMethod { get; set; }
    public decimal MonthlyRetainer { get; set; }
    public decimal MinimumMonthlyFee { get; set; }
    public decimal RevenueShareRate { get; set; }
    public decimal IncrementalRate { get; set; }
    public decimal ProfitShareRate { get; set; }
    public string CommissionTiersJson { get; set; } = "[]";
    public decimal SetupInvestment { get; set; }
    public decimal EstimatedMonthlyInternalCost { get; set; }
    public string Currency { get; set; } = "TRY";
    public string EvaluationSnapshotJson { get; set; } = "{}";
    public string RuleSnapshotJson { get; set; } = "{}";
    public string FinancialSnapshotJson { get; set; } = "{}";
    public string CommissionSnapshotJson { get; set; } = "{}";
    public string ConditionsSnapshotJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PartnershipCondition> Conditions { get; set; } = [];
}

public sealed class MonthlyPerformance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Vat { get; set; }
    public decimal Refunds { get; set; }
    public decimal Cancellations { get; set; }
    public decimal Chargebacks { get; set; }
    public decimal CustomerPaidShipping { get; set; }
    public decimal GiftCardTopups { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal CommissionableRevenue { get; set; }
    public int Orders { get; set; }
    public int Sessions { get; set; }
    public int NewCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public decimal Cogs { get; set; }
    public decimal PaymentFees { get; set; }
    public decimal FulfillmentCosts { get; set; }
    public decimal ShippingSubsidy { get; set; }
    public decimal OtherVariableCosts { get; set; }
    public decimal MetaSpend { get; set; }
    public decimal GoogleSpend { get; set; }
    public decimal TikTokSpend { get; set; }
    public decimal InfluencerSpend { get; set; }
    public decimal OtherAdSpend { get; set; }
    public decimal TotalAdSpend { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal ContributionBeforeMarketing { get; set; }
    public decimal ContributionBeforeOvo { get; set; }
    public decimal BrandContributionProfit { get; set; }
    public decimal Aov { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal Mer { get; set; }
    public decimal Cac { get; set; }
    public decimal ReturnRate { get; set; }
    public decimal OvoFee { get; set; }
    public decimal OvoInternalCost { get; set; }
    public decimal OvoGrossProfit { get; set; }
    public decimal OvoMargin { get; set; }
    public string CommissionBreakdownJson { get; set; } = "{}";
    public CommissionStatus CommissionStatus { get; set; } = CommissionStatus.Draft;
    public MonthlyPerformanceStatus Status { get; set; } = MonthlyPerformanceStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string PreparedBy { get; set; } = "";
    public DateTimeOffset? SubmittedAt { get; set; }
    public string ReviewedBy { get; set; } = "";
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public List<CommissionAdjustment> Adjustments { get; set; } = [];
    public CollectionAccount? Collection { get; set; }
}

public sealed class CommissionAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MonthlyPerformanceId { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GeneralSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DefaultCurrency { get; set; } = "TRY";
    public decimal DefaultVatRate { get; set; } = .20m;
    public int DefaultContractMonths { get; set; } = 24;
    public decimal DefaultSetupInvestment { get; set; } = 250_000m;
    public decimal TargetOvoGrossMargin { get; set; } = .55m;
    public decimal TargetBrandContributionMargin { get; set; } = .15m;
    public decimal MinimumFeeMultiplier { get; set; } = 1.8m;
    public decimal ExistingRevenueThreshold { get; set; } = 2_000_000m;
    public decimal ConcentrationRiskThreshold { get; set; } = .40m;
    public decimal MinimumPartnershipScore { get; set; } = 40m;
    public decimal ConditionalPartnershipScore { get; set; } = 55m;
    public decimal MinimumDataConfidenceScore { get; set; } = 50m;
    public decimal MinimumRecommendedAdSpend { get; set; } = 150_000m;
    public Guid? DefaultRuleSetId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DealTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DealType DealType { get; set; }
    public int ContractMonths { get; set; } = 24;
    public decimal MonthlyRetainer { get; set; }
    public decimal MinimumMonthlyFee { get; set; }
    public decimal RevenueShareRate { get; set; }
    public decimal IncrementalRate { get; set; }
    public decimal ProfitShareRate { get; set; }
    public string CommissionTiersJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public byte[] Content { get; set; } = [];
    public string Note { get; set; } = "";
    public string UploadedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string OldValueJson { get; set; } = "";
    public string NewValueJson { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record CommissionTier(decimal LowerBound, decimal? UpperBound, decimal Rate);
public sealed record FinancialInputs(decimal GrossSales, decimal Refunds, decimal Cancellations, decimal Chargebacks,
    decimal Cogs, decimal VariableCosts, decimal AdSpend, decimal OvoFee, int Orders = 0, int Sessions = 0, int NewCustomers = 0);
public sealed record FinancialResult(decimal NetRevenue, decimal GrossProfit, decimal ContributionBeforeMarketing,
    decimal ContributionBeforeOvo, decimal BrandContributionProfit, decimal BrandContributionMargin,
    decimal Mer, decimal Cac, decimal Aov, decimal ConversionRate);
public sealed record StructuredCondition(string Code, string Title, string Description, bool Required = true);
public sealed record RecommendationResult(DecisionStatus Decision, decimal PartnershipScore, decimal DataConfidenceScore,
    DealType RecommendedDealType, int RecommendedContractMonths, decimal RecommendedMinimumMonthlyFee,
    decimal RecommendedSetupInvestment, decimal RecommendedAdSpend, decimal RecommendedTargetMer,
    decimal RecommendedTargetBrandContributionMargin, string RecommendedCommissionStructure,
    IReadOnlyList<StructuredCondition> RequiredConditions, IReadOnlyList<string> PositiveSignals,
    IReadOnlyList<string> RiskSignals, IReadOnlyList<string> Reasons, IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> MissingInputs);
