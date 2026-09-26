using FluentValidation;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Validation;

public sealed class ScopeItemValidator : AbstractValidator<ScopeItemRequest>
{
    public ScopeItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Kapsam kalemi kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Kapsam başlığı 1–200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotNull().MaximumLength(2000).WithMessage("Kapsam açıklaması en fazla 2000 karakter olabilir.");
    }
}
public sealed class ScopeCreateValidator : AbstractValidator<ScopeCreateRequest>
{
    public ScopeCreateValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Talep kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Talep başlığı 1–200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotNull().MaximumLength(2000).WithMessage("Talep açıklaması en fazla 2000 karakter olabilir.");
    }
}
public sealed class ScopeRemoveValidator : AbstractValidator<ScopeRemoveRequest>
{
    public ScopeRemoveValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).WithMessage("Kapsam kalemini çıkarma nedenini yazın (en fazla 1000 karakter).");
    }
}
public sealed class ScopeDecisionValidator : AbstractValidator<ScopeDecisionRequest>
{
    public ScopeDecisionValidator()
    {
        RuleFor(x => x.Decision).NotEmpty().WithMessage("Talebi onaylayın veya reddedin.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000).WithMessage("Karar notunu yazın (en fazla 1000 karakter).");
    }
}
