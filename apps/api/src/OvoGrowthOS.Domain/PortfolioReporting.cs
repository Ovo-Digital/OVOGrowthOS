namespace OvoGrowthOS.Domain;

public enum ReportScope { Closed, Approved, Preparation, All }
public sealed record ReportPeriod(int Year, int Month);
public sealed record FinancialTotals(int RecordCount, decimal NetRevenue, decimal OvoFee, decimal OvoInternalCost,
    decimal OvoGrossProfit, decimal BrandContributionProfit, decimal TotalAdSpend, decimal? OvoMargin, decimal? Mer);

public static class PortfolioReporting
{
    public static CommissionStatus PaymentStage(MonthlyPerformanceStatus status) => status switch
    {
        MonthlyPerformanceStatus.Paid => CommissionStatus.Paid,
        MonthlyPerformanceStatus.Invoiced => CommissionStatus.Invoiced,
        MonthlyPerformanceStatus.Locked or MonthlyPerformanceStatus.Approved => CommissionStatus.Approved,
        _ => CommissionStatus.Draft
    };
    public static bool IsClosed(MonthlyPerformanceStatus status) =>
        status is MonthlyPerformanceStatus.Locked or MonthlyPerformanceStatus.Invoiced or MonthlyPerformanceStatus.Paid;

    public static bool Matches(MonthlyPerformanceStatus status, ReportScope scope) => scope switch
    {
        ReportScope.Closed => IsClosed(status),
        ReportScope.Approved => status == MonthlyPerformanceStatus.Approved,
        ReportScope.Preparation => status is MonthlyPerformanceStatus.Draft or MonthlyPerformanceStatus.UnderReview,
        ReportScope.All => true,
        _ => false
    };

    public static FinancialTotals Summarize(IEnumerable<MonthlyPerformance> source)
    {
        var rows = source.ToList();
        var fee = rows.Sum(x => x.OvoFee);
        var profit = rows.Sum(x => x.OvoGrossProfit);
        var revenue = rows.Sum(x => x.NetRevenue);
        var spend = rows.Sum(x => x.TotalAdSpend);
        return new(rows.Count, revenue, fee, rows.Sum(x => x.OvoInternalCost), profit,
            rows.Sum(x => x.BrandContributionProfit), spend,
            fee == 0 ? null : FinancialCalculator.Ratio(profit, fee),
            spend == 0 ? null : FinancialCalculator.Ratio(revenue, spend));
    }

    public static decimal Outstanding(IEnumerable<MonthlyPerformance> source) =>
        source.Where(x => IsClosed(x.Status)).Sum(x => Collections.Balance(x, DateOnly.MinValue).Outstanding);

    public static decimal Paid(IEnumerable<MonthlyPerformance> source) =>
        source.Where(x => IsClosed(x.Status)).Sum(x => Collections.Balance(x, DateOnly.MinValue).Paid);

    public static bool ExpectedInPeriod(Deal deal, ReportPeriod period)
    {
        if (deal.Status is not (DealStatus.Active or DealStatus.Expired or DealStatus.Terminated) || deal.StartDate is null) return false;
        var first = new DateOnly(period.Year, period.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return deal.StartDate <= last && (deal.EndDate is null || deal.EndDate >= first);
    }
}
