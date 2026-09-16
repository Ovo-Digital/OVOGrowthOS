using FluentValidation;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Validation;

public sealed class TemplateScopeValidator : AbstractValidator<TemplateScope>
{
    public TemplateScopeValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Geçerli bir iş şablonu seçin.");
        RuleFor(x => x.StartOn).InclusiveBetween(new DateOnly(2020, 1, 1), new DateOnly(2100, 12, 26)).WithMessage("2020–2100 arasında başlangıç tarihi seçin; son adımlar da bu aralıkta kalmalıdır.");
        RuleFor(x => x).Must(x => x.Kind == WorkTemplateKind.BrandStart ? x.DealId is null && x.Year == 0 && x.Month == 0
            : x.Kind == WorkTemplateKind.MonthlyClose && x.DealId.HasValue && x.Year is >= 2020 and <= 2100 && x.Month is >= 1 and <= 12)
            .WithMessage("Başlangıç şablonunda anlaşma/ay seçmeyin; kapanış şablonunda markanın anlaşmasını ve ayını seçin.");
    }
}
public sealed class TemplateApplyValidator : AbstractValidator<TemplateApplyRequest>
{
    public TemplateApplyValidator()
    {
        RuleFor(x => x.Scope).NotNull().SetValidator(new TemplateScopeValidator());
        RuleFor(x => x.Items).NotNull().Must(x => x is { Count: 3 }).WithMessage("Üç şablon adımını kontrol edin.");
        RuleForEach(x => x.Items).NotNull().ChildRules(item => {
            item.RuleFor(x => x.Step).NotEmpty(); item.RuleFor(x => x.AssigneeId).NotEmpty().WithMessage("Her adım için sorumlu seçin.");
            item.RuleFor(x => x.DueOn).InclusiveBetween(new DateOnly(2020, 1, 1), new DateOnly(2100, 12, 31)).WithMessage("Her adım için geçerli son tarih seçin.");
        });
    }
}
public sealed class WeeklyCapacityValidator : AbstractValidator<WeeklyCapacityRequest>
{
    public WeeklyCapacityValidator()
    {
        RuleFor(x => x.WeekStart).Must(WorkPlanning.ValidWeek).WithMessage("2020–2100 arasında haftanın pazartesi gününü seçin.");
        RuleFor(x => x.WorkingHours).InclusiveBetween(0, 168).PrecisionScale(5, 2, true).WithMessage("Haftalık çalışma saatini 0–168 arasında, en fazla iki ondalıkla yazın.");
        RuleFor(x => x.UnavailableHours).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.WorkingHours).PrecisionScale(5, 2, true).WithMessage("Kullanılamayan saat, çalışma saatinden fazla olamaz.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).NotEmpty().Must(x => x?.Trim().Length >= 5).MaximumLength(1000).WithMessage("Değişikliği kısaca açıklayın (5–1.000 karakter); özel izin veya sağlık bilgisi yazmayın.");
    }
}
public sealed class TaskHourPlanValidator : AbstractValidator<TaskHourPlanRequest>
{
    public TaskHourPlanValidator()
    {
        RuleFor(x => x.WeekStart).Must(WorkPlanning.ValidWeek).WithMessage("2020–2100 arasında haftanın pazartesi gününü seçin.");
        RuleFor(x => x.Hours).InclusiveBetween(0, 168).PrecisionScale(5, 2, true).WithMessage("Planlanan saati 0–168 arasında, en fazla iki ondalıkla yazın.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0); RuleFor(x => x.TaskRevision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).NotEmpty().Must(x => x?.Trim().Length >= 5).MaximumLength(1000).WithMessage("Planlama nedenini 5–1.000 karakterle açıklayın.");
    }
}
