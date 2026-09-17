using FluentValidation;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Validation;

public sealed class CollectionPromiseValidator : AbstractValidator<CollectionPromiseRequest>
{
    public CollectionPromiseValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 4, true).WithMessage("Ödeme sözünü sıfırdan büyük, en fazla dört ondalıklı tutar olarak yazın.");
        RuleFor(x => x.PromisedOn).InclusiveBetween(new DateOnly(2020, 1, 1), new DateOnly(2100, 12, 31)).WithMessage("Geçerli bir ödeme sözü tarihi yazın.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Takip sorumlusu seçin.");
        RuleFor(x => x.ContactNoteId).NotEmpty().WithMessage("Kaynak görüşme notunu seçin.");
        RuleFor(x => x.Reason).NotEmpty().Must(x => x?.Trim().Length >= 5).MaximumLength(1000).WithMessage("Kayıt veya değişiklik nedenini 5–1.000 karakterle açıklayın.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Ödeme sözü sürümü geçersiz. Sayfayı yenileyin.");
        RuleFor(x => x.CollectionRevision).GreaterThanOrEqualTo(0).WithMessage("Tahsilat sürümü geçersiz. Sayfayı yenileyin.");
    }
}
public sealed class CancelPromiseValidator : AbstractValidator<CancelPromiseRequest>
{
    public CancelPromiseValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().Must(x => x?.Trim().Length >= 5).MaximumLength(1000).WithMessage("Kaldırma nedenini 5–1.000 karakterle açıklayın.");
        RuleFor(x => x.Revision).GreaterThan(0).WithMessage("Ödeme sözü sürümü eksik. Sayfayı yenileyin.");
        RuleFor(x => x.CollectionRevision).GreaterThanOrEqualTo(0).WithMessage("Tahsilat sürümü geçersiz. Sayfayı yenileyin.");
    }
}
public sealed class PromiseTaskValidator : AbstractValidator<PromiseTaskRequest>
{
    public PromiseTaskValidator()
    {
        RuleFor(x => x.DueOn).InclusiveBetween(new DateOnly(2020, 1, 1), new DateOnly(2100, 12, 31)).WithMessage("Geçerli bir iş son tarihi yazın.");
        RuleFor(x => x.Revision).GreaterThan(0).WithMessage("Ödeme sözü sürümü eksik. Sayfayı yenileyin.");
        RuleFor(x => x.CollectionRevision).GreaterThanOrEqualTo(0).WithMessage("Tahsilat sürümü geçersiz. Sayfayı yenileyin.");
    }
}
