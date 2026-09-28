namespace OvoGrowthOS.Domain;

public sealed record SectorSample(decimal GrossSales, decimal GrossProfit, decimal Returns, decimal? NetRevenue, decimal? NetRevenueGoal);
public sealed record SectorMetric(int BrandCount, decimal? AverageGrossMargin, decimal? AverageReturnShare, decimal? AverageTargetAchievement);

public static class SectorComparison
{
    public static SectorMetric Build(IEnumerable<SectorSample> samples)
    {
        var rows = samples.ToList();
        return new SectorMetric(rows.Count,
            Average(rows.Where(x => x.GrossSales > 0).Select(x => x.GrossProfit / x.GrossSales)),
            Average(rows.Where(x => x.GrossSales > 0).Select(x => x.Returns / x.GrossSales)),
            Average(rows.Where(x => x.NetRevenueGoal is > 0 && x.NetRevenue.HasValue)
                .Select(x => x.NetRevenue!.Value / x.NetRevenueGoal!.Value)));
    }

    private static decimal? Average(IEnumerable<decimal> ratios)
    {
        var rows = ratios.ToList();
        return rows.Count == 0 ? null : Math.Round(rows.Average(), 4);
    }
}
