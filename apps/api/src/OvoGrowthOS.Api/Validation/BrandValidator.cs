using FluentValidation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Validation;

public sealed class BrandValidator : AbstractValidator<Brand>
{
    public BrandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        When(x => x.Economics is not null, () => RuleFor(x => x.Economics!.GrossMarginRate).InclusiveBetween(0, 1));
    }
}
