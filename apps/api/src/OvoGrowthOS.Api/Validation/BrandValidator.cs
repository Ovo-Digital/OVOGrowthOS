using FluentValidation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Validation;

public sealed class BrandValidator : AbstractValidator<Brand>
{
    public BrandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Marka adı zorunludur ve 160 karakteri geçemez.");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithMessage("Para birimi üç harfli ISO kodu olmalıdır.");
        RuleFor(x => x.ContactEmail).EmailAddress().WithMessage("Geçerli bir iletişim e-posta adresi girin.").When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        When(x => x.Economics is not null, () => RuleFor(x => x.Economics!.GrossMarginRate).InclusiveBetween(0, 1).WithMessage("Brüt kâr marjı 0 ile 1 arasında olmalıdır."));
    }
}
