using FluentValidation;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Validation;

public sealed class WorkTaskValidator : AbstractValidator<WorkTaskRequest>
{
    public WorkTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Görev kimliği eksik.");
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("Marka seçin.");
        RuleFor(x => x.AssigneeId).NotEmpty().WithMessage("Sorumlu çalışan seçin.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Görev başlığı 1–200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotNull().MaximumLength(4000).WithMessage("Açıklama en fazla 4000 karakter olabilir.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Geçerli bir öncelik seçin.");
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Geçerli bir görev türü seçin.");
        RuleFor(x => x.DueOn).InclusiveBetween(new DateOnly(2020, 1, 1), new DateOnly(2100, 12, 31)).WithMessage("2020–2100 arasında bir son tarih seçin.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Görev sürümü geçersiz. Listeyi yenileyin.");
        RuleFor(x => x).Must(x => x.Kind switch
        {
            WorkKind.General => x.DealId is null && x.Year is null && x.Month is null,
            WorkKind.MonthlyClose => x.DealId.HasValue && x.Year is >= 2020 and <= 2100 && x.Month is >= 1 and <= 12,
            WorkKind.ContractRenewal => x.DealId.HasValue && x.Year is null && x.Month is null,
            _ => false
        }).WithMessage("Kapanış için anlaşma ve dönem, yenileme için anlaşma seçin; genel görevde bağlı kayıt bırakmayın.");
    }
}
public sealed class FollowUpValidator : AbstractValidator<FollowUpRequest>
{
    public FollowUpValidator()
    {
        RuleFor(x => x.Stage).IsInEnum().WithMessage("Geçerli bir takip aşaması seçin.");
        RuleFor(x => x.WaitingReason).NotNull().MaximumLength(1000).WithMessage("Bekleme nedeni en fazla 1000 karakter olabilir.");
        RuleFor(x => x.NextStep).NotNull().MaximumLength(1000).WithMessage("Sonraki adım en fazla 1000 karakter olabilir.");
        RuleFor(x => x.WaitingReason).NotEmpty().When(x => x.Stage is LeadStage.OnHold or LeadStage.WaitingForInformation).WithMessage("Bekleyen bilgi veya bekleme nedenini yazın.");
        RuleFor(x => x.NextStep).NotEmpty().When(x => x.NextContactOn.HasValue).WithMessage("Sonraki görüşmede yapılacak adımı yazın.");
        RuleFor(x => x.NextContactOn).Must(x => x is null || x.Value.Year is >= 2020 and <= 2100).WithMessage("Geçerli bir sonraki görüşme tarihi seçin.");
        RuleFor(x => x.NextContactOn).NotNull().When(x => x.Stage == LeadStage.MeetingPlanned).WithMessage("Planlanan görüşmenin tarihini seçin.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0).WithMessage("Takip kaydının sürümü geçersiz. Sayfayı yenileyin.");
        RuleFor(x => x.SourceChannel).IsInEnum().When(x => x.SourceChannel.HasValue).WithMessage("Geçerli bir kaynak kanalı seçin.");
        RuleFor(x => x.SourceNote).MaximumLength(Pipeline.MaxSourceNoteLength).When(x => x.SourceNote is not null)
            .WithMessage($"Kaynak notu en fazla {Pipeline.MaxSourceNoteLength} karakter olabilir.");
    }
}

public sealed class PipelineLossValidator : AbstractValidator<PipelineLossRequest>
{
    public PipelineLossValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Kayıp nedenini yazın.")
            .MaximumLength(Pipeline.MaxReasonLength).WithMessage($"Kayıp nedeni en fazla {Pipeline.MaxReasonLength} karakter olabilir.");
    }
}
public sealed class ContactNoteValidator : AbstractValidator<ContactNoteRequest>
{
    public ContactNoteValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Görüşme notunun kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000).WithMessage("Görüşme notu 1–4000 karakter olmalıdır.");
        RuleFor(x => x.ContactOn).Must(x => x.Year is >= 2020 and <= 2100).WithMessage("Geçerli bir görüşme tarihi seçin.");
    }
}

public sealed class TimeEntryValidator : AbstractValidator<TimeEntryRequest>
{
    public TimeEntryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Saat girişi kimliği eksik. Formu yeniden açın.");
        RuleFor(x => x.WeekStart).Must(WorkPlanning.ValidWeek)
            .WithMessage("Hafta başlangıcı Pazartesi olmalı ve 2020 ile 2100 yılları arasında bulunmalıdır.");
        RuleFor(x => x.Hours).Must(x => TimeTracking.HoursError(x) is null)
            .WithMessage(x => TimeTracking.HoursError(x.Hours) ?? "Geçerli bir saat değeri girin.");
        RuleFor(x => x.Note).NotNull().MaximumLength(1000).WithMessage("Not en fazla 1000 karakter olabilir.");
    }
}
