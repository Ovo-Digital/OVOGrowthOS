using FluentValidation;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Validation;

public sealed class ServiceCostValidator : AbstractValidator<ServiceCostRequest>
{
    public ServiceCostValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Gider kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Geçerli gider türü seçin.");
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(160).WithMessage("Gider için benzersiz referans yazın (en fazla 160 karakter).");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000).WithMessage("Giderin ne olduğunu açıklayın (en fazla 1000 karakter).");
        RuleFor(x => x.IncurredOn).Must(x => x.Year is >= 2020 and <= 2100).WithMessage("Geçerli gider tarihi seçin.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Kayıt sürümü geçersiz. Sayfayı yenileyin.");
        RuleFor(x => x).Must(x => x.Kind == ServiceCostKind.DirectExpense
            ? PositiveMoney(x.Amount) && x.Hours is null && x.HourlyCost is null
            : x.Amount == 0 && x.Hours is > 0 and <= 744 && x.HourlyCost is > 0 and <= 1000000 &&
                decimal.Round(x.Hours.Value, 4) == x.Hours && decimal.Round(x.HourlyCost.Value, 4) == x.HourlyCost &&
                OperatingCosts.TeamAmount(x.Hours.Value, x.HourlyCost.Value) > 0)
            .WithMessage("Doğrudan giderde pozitif tutar; ekip çalışmasında 0–744 arası pozitif saat ve pozitif saat maliyeti girin. En fazla dört ondalık hane kullanın. Ekip toplamını sistem hesaplar.");
    }
    internal static bool PositiveMoney(decimal x) => x > 0 && x <= 99999999999999.9999m && decimal.Round(x, 4) == x;
}
public sealed class CostConfirmationValidator : AbstractValidator<CostConfirmationRequest>
{
    public CostConfirmationValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).WithMessage("Kontrol sonucunu veya yeniden açma nedenini yazın (en fazla 1000 karakter).");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Kayıt sürümü geçersiz. Sayfayı yenileyin.");
    }
}
public sealed class InvestmentEntryValidator : AbstractValidator<InvestmentEntryRequest>
{
    public InvestmentEntryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Kayıt kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Yatırım harcaması veya geri kazanım seçin.");
        RuleFor(x => x.Amount).Must(ServiceCostValidator.PositiveMoney).WithMessage("Pozitif ve en fazla dört ondalık haneli bir tutar girin.");
        RuleFor(x => x.OccurredOn).Must(x => x.Year is >= 2020 and <= 2100).WithMessage("Geçerli gerçekleşme tarihi seçin.");
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(160).WithMessage("Benzersiz bir belge veya karar referansı girin (en fazla 160 karakter).");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000).WithMessage("Harcama veya geri kazanımın dayanağını yazın (en fazla 1000 karakter).");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Kayıt sürümü geçersiz. Sayfayı yenileyin.");
    }
}
