using FluentValidation;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Validation;

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null) return await next(context);
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return result.IsValid
            ? await next(context)
            : Results.ValidationProblem(result.ToDictionary());
    }
}

internal static class ValidationRules
{
    public static void Rate<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, decimal>> expression, string label) =>
        validator.RuleFor(expression).InclusiveBetween(0, 1).WithMessage($"{label} 0 ile 1 arasında olmalıdır.");

    public static void NonNegative<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, decimal>> expression, string label) =>
        validator.RuleFor(expression).GreaterThanOrEqualTo(0).WithMessage($"{label} negatif olamaz.");
}

public sealed class EvaluationDraftRequestValidator : AbstractValidator<EvaluationDraftRequest>
{
    public EvaluationDraftRequestValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("Marka seçimi zorunludur.");
        RuleFor(x => x.CurrentStep).InclusiveBetween(1, 10).WithMessage("Değerlendirme adımı 1 ile 10 arasında olmalıdır.");
        RuleFor(x => x.Status).Must(x => x is EvaluationStatus.Draft or EvaluationStatus.InProgress or EvaluationStatus.ReadyForAnalysis)
            .WithMessage("Değerlendirme durumu bu işlemle doğrudan tamamlanmış duruma getirilemez.");
        this.NonNegative(x => x.AverageMonthlyRevenue, "Aylık ciro");
        this.Rate(x => x.GrossMarginRate, "Brüt kâr marjı");
        this.Rate(x => x.CogsRate, "Ürün maliyeti oranı");
        this.NonNegative(x => x.AverageOrderValue, "Ortalama sepet tutarı");
        this.Rate(x => x.ReturnRate, "İade oranı");
        this.NonNegative(x => x.CurrentAdSpend, "Reklam harcaması");
        this.NonNegative(x => x.CurrentCac, "Müşteri edinme maliyeti");
        this.NonNegative(x => x.AverageCustomerLtv, "Müşteri yaşam boyu değeri");
        this.Rate(x => x.VariableCostRate, "Değişken maliyet oranı");
        RuleFor(x => x.StockCoverageDays).GreaterThanOrEqualTo(0).WithMessage("Stok günü negatif olamaz.");
        RuleFor(x => x.MonthlyOrders).GreaterThanOrEqualTo(0).WithMessage("Sipariş sayısı negatif olamaz.");
        RuleFor(x => x.MonthlySessions).GreaterThanOrEqualTo(0).WithMessage("Oturum sayısı negatif olamaz.");
        RuleFor(x => x.NewCustomers).GreaterThanOrEqualTo(0).WithMessage("Yeni müşteri sayısı negatif olamaz.");
        RuleFor(x => x.ReturningCustomers).GreaterThanOrEqualTo(0).WithMessage("Tekrar gelen müşteri sayısı negatif olamaz.");
        foreach (var score in new[] { nameof(EvaluationDraftRequest.ProductMarketFit), nameof(EvaluationDraftRequest.GrowthPotential), nameof(EvaluationDraftRequest.OperationalReadiness), nameof(EvaluationDraftRequest.CreativeCapability), nameof(EvaluationDraftRequest.FounderCooperation), nameof(EvaluationDraftRequest.DataMaturity) })
            RuleFor(x => Score(x, score)).InclusiveBetween(0, 5).WithMessage("Değerlendirme puanları 0 ile 5 arasında olmalıdır.");
        this.NonNegative(x => x.InternalMonthlyCost, "Aylık iç maliyet");
        this.NonNegative(x => x.SetupInvestment, "Kurulum yatırımı");
    }

    private static int Score(EvaluationDraftRequest x, string name) => name switch
    {
        nameof(EvaluationDraftRequest.ProductMarketFit) => x.ProductMarketFit,
        nameof(EvaluationDraftRequest.GrowthPotential) => x.GrowthPotential,
        nameof(EvaluationDraftRequest.OperationalReadiness) => x.OperationalReadiness,
        nameof(EvaluationDraftRequest.CreativeCapability) => x.CreativeCapability,
        nameof(EvaluationDraftRequest.FounderCooperation) => x.FounderCooperation,
        _ => x.DataMaturity
    };
}

public sealed class ScenarioRequestValidator : AbstractValidator<ScenarioRequest>
{
    public ScenarioRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Senaryo adı zorunludur ve 160 karakteri geçemez.");
        this.NonNegative(x => x.MonthlyRevenue, "Aylık ciro"); this.Rate(x => x.GrossMarginRate, "Brüt kâr marjı");
        this.NonNegative(x => x.AdSpend, "Reklam harcaması"); this.Rate(x => x.ReturnRate, "İade oranı");
        this.NonNegative(x => x.AverageOrderValue, "Ortalama sepet tutarı"); this.Rate(x => x.VariableCostRate, "Değişken maliyet oranı");
        this.NonNegative(x => x.OvoInternalMonthlyCost, "OVO aylık iç maliyeti"); this.NonNegative(x => x.MinimumMonthlyFee, "Asgari aylık ücret");
        this.Rate(x => x.RevenueShareRate, "Gelir payı oranı"); this.NonNegative(x => x.MonthlyRetainer, "Aylık sabit ücret");
        this.NonNegative(x => x.BaselineRevenue, "Baz ciro"); this.Rate(x => x.IncrementalRate, "Büyüme payı oranı"); this.Rate(x => x.ProfitShareRate, "Kâr payı oranı");
        this.Rate(x => x.TargetBrandContributionMargin, "Hedef marka katkı marjı"); this.NonNegative(x => x.SetupInvestment, "Kurulum yatırımı");
        RuleFor(x => x.NewCustomers).GreaterThanOrEqualTo(0).WithMessage("Yeni müşteri sayısı negatif olamaz.");
        RuleFor(x => x.ContractMonths).InclusiveBetween(1, 120).WithMessage("Sözleşme süresi 1 ile 120 ay arasında olmalıdır.");
        RuleForEach(x => x.CommissionTiers).SetValidator(new CommissionTierValidator());
    }
}

public sealed class DealRequestValidator : AbstractValidator<DealRequest>
{
    public DealRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Anlaşma adı zorunludur ve 160 karakteri geçemez.");
        RuleFor(x => x.ContractMonths).InclusiveBetween(1, 120).WithMessage("Sözleşme süresi 1 ile 120 ay arasında olmalıdır.");
        this.NonNegative(x => x.BaselineRevenue, "Baz ciro"); this.NonNegative(x => x.MonthlyRetainer, "Aylık sabit ücret");
        this.NonNegative(x => x.MinimumMonthlyFee, "Asgari aylık ücret"); this.Rate(x => x.RevenueShareRate, "Gelir payı oranı");
        this.Rate(x => x.IncrementalRate, "Büyüme payı oranı"); this.Rate(x => x.ProfitShareRate, "Kâr payı oranı");
        this.NonNegative(x => x.SetupInvestment, "Kurulum yatırımı"); this.NonNegative(x => x.EstimatedMonthlyInternalCost, "Tahmini aylık iç maliyet");
        RuleFor(x => x).Must(x => !x.BaselinePeriodStart.HasValue || !x.BaselinePeriodEnd.HasValue || x.BaselinePeriodStart <= x.BaselinePeriodEnd)
            .WithMessage("Baz dönem başlangıcı bitiş tarihinden sonra olamaz.");
        RuleForEach(x => x.CommissionTiers).SetValidator(new CommissionTierValidator());
    }
}

public sealed class CommissionTierValidator : AbstractValidator<CommissionTier>
{
    public CommissionTierValidator()
    {
        RuleFor(x => x.LowerBound).GreaterThanOrEqualTo(0).WithMessage("Kademe alt sınırı negatif olamaz.");
        RuleFor(x => x.Rate).InclusiveBetween(0, 1).WithMessage("Kademe oranı 0 ile 1 arasında olmalıdır.");
        RuleFor(x => x).Must(x => !x.UpperBound.HasValue || x.UpperBound > x.LowerBound).WithMessage("Kademe üst sınırı alt sınırdan büyük olmalıdır.");
    }
}

public sealed class PerformanceRequestValidator : AbstractValidator<PerformanceRequest>
{
    public PerformanceRequestValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("Marka seçimi zorunludur.");
        RuleFor(x => x.DealId).NotEmpty().WithMessage("Anlaşma seçimi zorunludur.");
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100).WithMessage("Yıl 2020 ile 2100 arasında olmalıdır.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("Ay 1 ile 12 arasında olmalıdır.");
        foreach (var field in DecimalFields()) RuleFor(x => Value(x, field)).GreaterThanOrEqualTo(0).WithMessage("Finansal tutarlar negatif olamaz.");
        RuleFor(x => x.Orders).GreaterThanOrEqualTo(0).WithMessage("Sipariş sayısı negatif olamaz.");
        RuleFor(x => x.Sessions).GreaterThanOrEqualTo(0).WithMessage("Oturum sayısı negatif olamaz.");
        RuleFor(x => x.NewCustomers).GreaterThanOrEqualTo(0).WithMessage("Yeni müşteri sayısı negatif olamaz.");
        RuleFor(x => x.ReturningCustomers).GreaterThanOrEqualTo(0).WithMessage("Tekrar gelen müşteri sayısı negatif olamaz.");
        RuleFor(x => x).Must(x => x.NewCustomers + x.ReturningCustomers <= x.Orders).WithMessage("Yeni ve tekrar gelen müşteri toplamı sipariş sayısını geçemez.");
        RuleFor(x => x).Must(x => x.Vat + x.Refunds + x.Cancellations + x.Chargebacks + x.CustomerPaidShipping + x.GiftCardTopups <= x.GrossSales)
            .WithMessage("Cirodan düşülen tutarların toplamı brüt satışı geçemez.");
    }

    private static string[] DecimalFields() => [nameof(PerformanceRequest.GrossSales), nameof(PerformanceRequest.Vat), nameof(PerformanceRequest.Refunds), nameof(PerformanceRequest.Cancellations), nameof(PerformanceRequest.Chargebacks), nameof(PerformanceRequest.CustomerPaidShipping), nameof(PerformanceRequest.GiftCardTopups), nameof(PerformanceRequest.Cogs), nameof(PerformanceRequest.PaymentFees), nameof(PerformanceRequest.FulfillmentCosts), nameof(PerformanceRequest.ShippingSubsidy), nameof(PerformanceRequest.OtherVariableCosts), nameof(PerformanceRequest.MetaSpend), nameof(PerformanceRequest.GoogleSpend), nameof(PerformanceRequest.TikTokSpend), nameof(PerformanceRequest.InfluencerSpend), nameof(PerformanceRequest.OtherAdSpend)];
    private static decimal Value(PerformanceRequest x, string name) => name switch
    {
        nameof(PerformanceRequest.GrossSales) => x.GrossSales, nameof(PerformanceRequest.Vat) => x.Vat, nameof(PerformanceRequest.Refunds) => x.Refunds,
        nameof(PerformanceRequest.Cancellations) => x.Cancellations, nameof(PerformanceRequest.Chargebacks) => x.Chargebacks,
        nameof(PerformanceRequest.CustomerPaidShipping) => x.CustomerPaidShipping, nameof(PerformanceRequest.GiftCardTopups) => x.GiftCardTopups,
        nameof(PerformanceRequest.Cogs) => x.Cogs, nameof(PerformanceRequest.PaymentFees) => x.PaymentFees, nameof(PerformanceRequest.FulfillmentCosts) => x.FulfillmentCosts,
        nameof(PerformanceRequest.ShippingSubsidy) => x.ShippingSubsidy, nameof(PerformanceRequest.OtherVariableCosts) => x.OtherVariableCosts,
        nameof(PerformanceRequest.MetaSpend) => x.MetaSpend, nameof(PerformanceRequest.GoogleSpend) => x.GoogleSpend, nameof(PerformanceRequest.TikTokSpend) => x.TikTokSpend,
        nameof(PerformanceRequest.InfluencerSpend) => x.InfluencerSpend, _ => x.OtherAdSpend
    };
}

public sealed class SettingsRequestValidator : AbstractValidator<SettingsRequest>
{
    public SettingsRequestValidator()
    {
        RuleFor(x => x.DefaultCurrency).NotEmpty().Length(3).WithMessage("Para birimi üç harfli ISO kodu olmalıdır.");
        this.Rate(x => x.DefaultVatRate, "KDV oranı"); this.Rate(x => x.TargetOvoGrossMargin, "OVO hedef brüt kâr marjı");
        this.Rate(x => x.TargetBrandContributionMargin, "Marka hedef katkı marjı"); this.Rate(x => x.ConcentrationRiskThreshold, "Yoğunlaşma riski eşiği");
        RuleFor(x => x.DefaultContractMonths).InclusiveBetween(1, 120).WithMessage("Varsayılan sözleşme süresi 1 ile 120 ay arasında olmalıdır.");
        this.NonNegative(x => x.DefaultSetupInvestment, "Varsayılan kurulum yatırımı");
        RuleFor(x => x.MinimumFeeMultiplier).GreaterThan(0).WithMessage("Asgari ücret çarpanı sıfırdan büyük olmalıdır.");
        this.NonNegative(x => x.ExistingRevenueThreshold, "Mevcut ciro eşiği"); this.NonNegative(x => x.MinimumRecommendedAdSpend, "Asgari reklam bütçesi");
        RuleFor(x => x.MinimumPartnershipScore).InclusiveBetween(0, 100).WithMessage("Asgari ortaklık puanı 0 ile 100 arasında olmalıdır.");
        RuleFor(x => x.ConditionalPartnershipScore).InclusiveBetween(0, 100).GreaterThanOrEqualTo(x => x.MinimumPartnershipScore).WithMessage("Koşullu kabul puanı asgari ortaklık puanından düşük olamaz.");
        RuleFor(x => x.MinimumDataConfidenceScore).InclusiveBetween(0, 100).WithMessage("Asgari veri güven puanı 0 ile 100 arasında olmalıdır.");
    }
}

public sealed class AdjustmentRequestValidator : AbstractValidator<AdjustmentRequest>
{
    public AdjustmentRequestValidator()
    {
        RuleFor(x => x.Amount).NotEqual(0).WithMessage("Düzeltme tutarı sıfır olamaz.");
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500).WithMessage("Düzeltme nedeni 5 ile 500 karakter arasında olmalıdır.");
    }
}

public sealed class RuleSetRequestValidator : AbstractValidator<RuleSetRequest>
{
    public RuleSetRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Kural seti adı zorunludur ve 160 karakteri geçemez.");
}

public sealed class BrandUpdateRequestValidator : AbstractValidator<BrandUpdateRequest>
{
    public BrandUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Marka adı zorunludur ve 160 karakteri geçemez.");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithMessage("Para birimi üç harfli ISO kodu olmalıdır.");
        RuleFor(x => x.Country).NotEmpty().Length(2).WithMessage("Ülke iki harfli ülke kodu olmalıdır.");
        RuleFor(x => x.ContactEmail).EmailAddress().WithMessage("Geçerli bir iletişim e-posta adresi girin.").When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}

public sealed class RuleRequestValidator : AbstractValidator<RuleRequest>
{
    public RuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Kural adı zorunludur ve 160 karakteri geçemez.");
        RuleFor(x => x.Weight).GreaterThanOrEqualTo(0).WithMessage("Kural ağırlığı negatif olamaz.");
        RuleFor(x => x).Must(x => x.Operator != RuleOperator.Between || x.SecondaryValue.HasValue && x.SecondaryValue > x.Value)
            .WithMessage("Aralık kuralında üst sınır alt sınırdan büyük olmalıdır.");
    }
}

public sealed class ConditionUpdateRequestValidator : AbstractValidator<ConditionUpdateRequest>
{
    public ConditionUpdateRequestValidator()
    {
        RuleFor(x => x.Status).Must(x => x is ConditionStatus.Satisfied or ConditionStatus.Waived).WithMessage("Koşul tamamlandı veya feragat edildi olarak işaretlenebilir.");
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500).When(x => x.Status == ConditionStatus.Waived)
            .WithMessage("Feragat nedeni 5 ile 500 karakter arasında olmalıdır.");
        RuleFor(x => x.EvidenceUrl).MaximumLength(1000).WithMessage("Kanıt bağlantısı 1000 karakteri geçemez.");
    }
}

public sealed class DealLifecycleRequestValidator : AbstractValidator<DealLifecycleRequest>
{
    public DealLifecycleRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500)
        .WithMessage("İşlem nedeni 5 ile 500 karakter arasında olmalıdır.");
}

public sealed class DealTemplateRequestValidator : AbstractValidator<DealTemplateRequest>
{
    public DealTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Şablon adı zorunludur ve 160 karakteri geçemez.");
        RuleFor(x => x.ContractMonths).InclusiveBetween(1, 120).WithMessage("Sözleşme süresi 1 ile 120 ay arasında olmalıdır.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage("Gösterim sırası negatif olamaz.");
        this.NonNegative(x => x.MonthlyRetainer, "Aylık sabit ücret");this.NonNegative(x => x.MinimumMonthlyFee, "Asgari ücret");
        this.Rate(x => x.RevenueShareRate, "Gelir payı oranı");this.Rate(x => x.IncrementalRate, "Büyüme payı oranı");this.Rate(x => x.ProfitShareRate, "Kâr payı oranı");
        RuleForEach(x => x.CommissionTiers).SetValidator(new CommissionTierValidator());
    }
}
