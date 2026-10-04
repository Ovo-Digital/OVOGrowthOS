namespace OvoGrowthOS.Domain;

public sealed record HourDeviationInput(Guid BrandId, string BrandName, Guid TaskId, string TaskTitle,
    decimal PlannedHours, decimal ActualHours, decimal VoidedHours);

public sealed record HourDeviationRow(Guid BrandId, string BrandName, int TaskCount,
    decimal PlannedHours, decimal ActualHours, decimal VoidedHours, decimal DifferenceHours, decimal? CompletionRatio);

// Planned-vs-actual hour deviation for a week range. Read-only reporting: nothing here
// feeds commissions, costs or payroll; it only explains how far execution sat from the plan.
public static class HourDeviation
{
    public static HourDeviationRow Build(Guid brandId, string brandName, int taskCount,
        decimal planned, decimal actual, decimal voided)
    {
        var difference = decimal.Round(actual - planned, 2, MidpointRounding.AwayFromZero);
        return new HourDeviationRow(brandId, brandName, taskCount,
            decimal.Round(planned, 2, MidpointRounding.AwayFromZero),
            decimal.Round(actual, 2, MidpointRounding.AwayFromZero),
            decimal.Round(voided, 2, MidpointRounding.AwayFromZero),
            difference,
            planned == 0 ? null : decimal.Round(actual / planned, 4, MidpointRounding.AwayFromZero));
    }

    public static IReadOnlyList<HourDeviationRow> ByBrand(IEnumerable<HourDeviationInput> rows) =>
        rows.GroupBy(x => new { x.BrandId, x.BrandName })
            .Select(g => Build(g.Key.BrandId, g.Key.BrandName, g.Select(x => x.TaskId).Distinct().Count(),
                g.Sum(x => x.PlannedHours), g.Sum(x => x.ActualHours), g.Sum(x => x.VoidedHours)))
            .OrderByDescending(x => Math.Abs(x.DifferenceHours)).ThenBy(x => x.BrandName, StringComparer.Ordinal)
            .ToList();

    public static HourDeviationRow Total(IEnumerable<HourDeviationInput> rows)
    {
        var list = rows.ToList();
        return Build(Guid.Empty, "", list.Select(x => x.TaskId).Distinct().Count(),
            list.Sum(x => x.PlannedHours), list.Sum(x => x.ActualHours), list.Sum(x => x.VoidedHours));
    }

    public static string StatusLabel(HourDeviationRow row)
    {
        if (row.PlannedHours == 0) return row.ActualHours == 0 ? "Plan ve kayıt yok" : "Yalnız gerçekleşen saat var";
        if (row.DifferenceHours > 0) return "Planın üzerinde";
        if (row.DifferenceHours < 0) return "Planın altında";
        return "Plana eşit";
    }
}
