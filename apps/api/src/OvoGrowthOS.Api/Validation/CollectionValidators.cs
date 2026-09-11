using FluentValidation;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Validation;

public sealed class InvoiceValidator : AbstractValidator<InvoiceRequest>
{
    public InvoiceValidator()
    {
        RuleFor(x => x.Reference).NotNull().MaximumLength(100).WithMessage("Fatura referansı en fazla 100 karakter olabilir.");
        RuleFor(x => x.Reason).NotNull().MaximumLength(1000).WithMessage("Değişiklik nedeni en fazla 1000 karakter olabilir.");
        RuleFor(x => x.InvoiceOn).Must(x => x is null || x.Value.Year is >= 2020 and <= 2100).WithMessage("Geçerli bir fatura tarihi seçin.");
        RuleFor(x => x.DueOn).Must(x => x is null || x.Value.Year is >= 2020 and <= 2100).WithMessage("Geçerli bir vade seçin.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
public sealed class PaymentValidator : AbstractValidator<PaymentRequest>
{
    public PaymentValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Ödeme kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(160).WithMessage("Ödemenin benzersiz referansını yazın (en fazla 160 karakter).");
        RuleFor(x => x.Note).NotNull().MaximumLength(1000).WithMessage("Not en fazla 1000 karakter olabilir.");
        RuleFor(x => x.PaidOn).Must(x => x.Year is >= 2020 and <= 2100).WithMessage("Geçerli bir ödeme tarihi seçin.");
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(99999999999999.9999m).WithMessage("Geçerli ve sıfırdan büyük bir ödeme tutarı girin.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
public sealed class VoidPaymentValidator : AbstractValidator<VoidPaymentRequest>
{
    public VoidPaymentValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).WithMessage("Hatalı ödeme kaydını iptal etme nedenini yazın (en fazla 1000 karakter).");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
