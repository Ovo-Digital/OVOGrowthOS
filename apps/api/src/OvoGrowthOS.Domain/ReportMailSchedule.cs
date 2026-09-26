namespace OvoGrowthOS.Domain;

public sealed record ReportScheduleWindow(DateTimeOffset OccurrenceAt, DateTimeOffset EndsAt, int TargetYear, int TargetMonth);

public static class ReportMailSchedule
{
    public const int CatchUpDays = 3;
    public const int DeferralLeadDays = 7;

    private static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTimeOffset ToInstant(int year, int month, int day, int hour)
    {
        var local = new DateTime(year, month, Math.Clamp(day, 1, DateTime.DaysInMonth(year, month)),
            Math.Clamp(hour, 0, 23), 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Turkey.GetUtcOffset(local));
    }

    public static (int Year, int Month) Previous(int year, int month) => month == 1 ? (year - 1, 12) : (year, month - 1);
    public static (int Year, int Month) Following(int year, int month) => month == 12 ? (year + 1, 1) : (year, month + 1);

    public static (int Year, int Month) TargetOf(DateTimeOffset occurrence)
    {
        var local = TimeZoneInfo.ConvertTime(occurrence, Turkey);
        return Previous(local.Year, local.Month);
    }

    // The single occurrence that delivers (year, month): the configured day of the following month.
    public static DateTimeOffset OccurrenceFor(int year, int month, int day, int hour)
    {
        var (nextYear, nextMonth) = Following(year, month);
        return ToInstant(nextYear, nextMonth, day, hour);
    }

    // Latest configured occurrence that has started and whose catch-up window is still open.
    public static ReportScheduleWindow? CurrentWindow(DateTimeOffset now, int day, int hour)
    {
        var local = TimeZoneInfo.ConvertTime(now, Turkey);
        for (var step = 0; step < 2; step++)
        {
            var (year, month) = step == 0 ? (local.Year, local.Month) : Previous(local.Year, local.Month);
            var at = ToInstant(year, month, day, hour);
            if (at > now || now > at.AddDays(CatchUpDays)) continue;
            var (targetYear, targetMonth) = Previous(year, month);
            return new ReportScheduleWindow(at, at.AddDays(CatchUpDays), targetYear, targetMonth);
        }
        return null;
    }

    public static bool OwnsEmail(bool scheduled, int day, int hour, DateTimeOffset now, int reportYear, int reportMonth)
    {
        if (!scheduled) return false;
        var at = OccurrenceFor(reportYear, reportMonth, day, hour);
        return now >= at.AddDays(-DeferralLeadDays) && now <= at.AddDays(CatchUpDays);
    }

    public static DateTimeOffset NextOccurrence(DateTimeOffset now, int day, int hour)
    {
        var local = TimeZoneInfo.ConvertTime(now, Turkey);
        var at = ToInstant(local.Year, local.Month, day, hour);
        return at > now ? at : OccurrenceFor(local.Year, local.Month, day, hour);
    }
}
