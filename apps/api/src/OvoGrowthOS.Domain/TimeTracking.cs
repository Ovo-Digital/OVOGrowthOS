namespace OvoGrowthOS.Domain;

public sealed class TaskTimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public decimal Hours { get; set; }
    public string Note { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VoidedAt { get; set; }
    public string VoidedBy { get; set; } = "";
    public string VoidReason { get; set; } = "";
}

public sealed record TimeEntrySummary(decimal PlannedHours, decimal ActualHours, decimal VoidedHours, decimal RemainingHours, bool Complete);

public static class TimeTracking
{
    public const decimal MaxWeekHours = 168;

    public static string? HoursError(decimal hours)
    {
        if (hours <= 0) return "Gerçekleşen saat sıfırdan büyük olmalıdır.";
        if (hours > MaxWeekHours) return $"Bir hafta için en fazla {MaxWeekHours} saat girilebilir.";
        if (decimal.Round(hours, 4) != hours) return "Saat değerini en fazla dört ondalık haneyle yazın.";
        return null;
    }

    public static TimeEntrySummary Summary(IEnumerable<TaskHourPlan> plans, IEnumerable<TaskTimeEntry> entries)
    {
        var all = entries.ToList();
        var planned = plans.Sum(x => x.Hours);
        var actual = all.Where(x => x.VoidedAt is null).Sum(x => x.Hours);
        var voided = all.Where(x => x.VoidedAt is not null).Sum(x => x.Hours);
        // Planned and actual are reported side by side; neither value is derived from the other.
        return new(planned, actual, voided, planned - actual, planned > 0 && actual > 0);
    }
}
