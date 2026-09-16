namespace OvoGrowthOS.Domain;

public enum WorkTemplateKind { BrandStart, MonthlyClose }

public sealed class WorkTemplateRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public WorkTemplateKind Kind { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid? DealId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkTemplateTask
{
    public Guid RunId { get; set; }
    public string Step { get; set; } = "";
    public Guid TaskId { get; set; }
}

public sealed class WeeklyCapacity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public decimal WorkingHours { get; set; }
    public decimal UnavailableHours { get; set; }
    public int Revision { get; set; }
}

public sealed class TaskHourPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public DateOnly WeekStart { get; set; }
    public decimal Hours { get; set; }
    public int Revision { get; set; }
}

public sealed record WorkTemplateStep(string Key, string Title, string Description, int DaysAfterStart, WorkKind Kind);
public sealed record CapacityResult(decimal? AvailableHours, decimal PlannedHours, decimal CompletedTaskPlannedHours,
    decimal? RemainingHours, decimal? LoadRatio, bool? Overloaded);

public static class WorkPlanning
{
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    public static bool ValidWeek(DateOnly day) => day.Year is >= 2020 and <= 2100 && day.DayOfWeek == DayOfWeek.Monday;
    public static CapacityResult Compare(WeeklyCapacity? capacity, decimal planned, decimal completedPlanned, decimal additional = 0)
    {
        var available = capacity is null ? (decimal?)null : capacity.WorkingHours - capacity.UnavailableHours;
        var total = planned + additional;
        return new(available, total, completedPlanned, available - total, available > 0 ? total / available : null,
            available.HasValue ? total > available : null);
    }
    public static IReadOnlyList<WorkTemplateStep> Steps(WorkTemplateKind kind) => kind switch
    {
        WorkTemplateKind.BrandStart => [
            new("contacts", "İletişim ve çalışma sorumlularını netleştir", "Marka ve OVO tarafındaki sorumluları, iletişim yolunu ve ilk görüşmeyi netleştirin.", 0, WorkKind.General),
            new("sources", "İlk veri ve belge ihtiyaçlarını belirle", "Satış, maliyet ve reklam kaynaklarını listeleyin. Şifre veya özel anahtarları görev açıklamasına yazmayın.", 2, WorkKind.General),
            new("kickoff", "Başlangıç planını birlikte gözden geçir", "İlk ayın hedeflerini ve yapılacak işleri görüşün. Bu görev anlaşma kabulü veya ticari onay değildir.", 5, WorkKind.General)],
        WorkTemplateKind.MonthlyClose => [
            new("sources", "Aylık kaynak raporları kontrol et", "Satış, iade, maliyet ve reklam tutarlarını aynı ay ve para birimiyle hazırlayın.", 0, WorkKind.General),
            new("close", "Aylık kapanışı ve ikinci kişi kontrolünü takip et", "Dönemi incelemeye gönderin; hazırlayan dışındaki yetkili kişi onaylasın. Görevi tamamlamak dönemi onaylamaz veya kilitlemez.", 2, WorkKind.MonthlyClose),
            new("report", "Aylık raporu ve takip işlerini gözden geçir", "Kapanmış sonucu ve hedef farklarını inceleyin. Müşteriye paylaşım ayrıca yetkili kişi tarafından yapılır.", 4, WorkKind.General)],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
