using FluentValidation;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Validation;

public sealed class MonthlyTargetValidator : AbstractValidator<MonthlyTargetRequest>
{
    public MonthlyTargetValidator()
    {
        RuleFor(x => x.NetRevenueGoal).GreaterThanOrEqualTo(0).PrecisionScale(18, 4, true).WithMessage("Net ciro hedefini negatif olmayan, en fazla dört ondalıklı tutar olarak yazın.");
        RuleFor(x => x.AdBudget).GreaterThanOrEqualTo(0).PrecisionScale(18, 4, true).WithMessage("Reklam bütçesini negatif olmayan, en fazla dört ondalıklı tutar olarak yazın.");
        RuleFor(x => x.ContributionMarginGoal).InclusiveBetween(0, 1).PrecisionScale(18, 4, true).WithMessage("Katkı marjı hedefi %0–%100 arasında olmalıdır.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Hedef sorumlusu seçin.");
        RuleFor(x => x.Reason).NotEmpty().Must(x => x?.Trim().Length >= 5).MaximumLength(1000).WithMessage("Hedef belirleme/değişiklik nedenini 5–1.000 karakterle açıklayın.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Hedef sürümü geçersiz. Sayfayı yenileyin.");
    }
}
public sealed class TargetActionValidator : AbstractValidator<TargetActionRequest>
{
    public TargetActionValidator()
    {
        RuleFor(x => x.Metric).IsInEnum().WithMessage("Geçerli gösterge seçin.");
        RuleFor(x => x.AssigneeId).NotEmpty().WithMessage("Takip sorumlusu seçin.");
        RuleFor(x => x.DueOn).InclusiveBetween(new DateOnly(2020, 1, 1), new DateOnly(2100, 12, 31)).WithMessage("Geçerli bir son tarih yazın.");
        RuleFor(x => x.Description).NotEmpty().Must(x => x?.Trim().Length >= 5).MaximumLength(2000).WithMessage("Yapılacak işi 5–2.000 karakterle açıklayın.");
        RuleFor(x => x.TargetRevision).GreaterThan(0).WithMessage("Hedef sürümü eksik. Sayfayı yenileyin.");
        RuleFor(x => x.PerformanceUpdatedAt).NotEmpty().WithMessage("Gerçekleşen sonuç sürümü eksik. Sayfayı yenileyin.");
    }
}
