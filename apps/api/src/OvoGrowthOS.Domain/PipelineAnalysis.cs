using System.Globalization;

namespace OvoGrowthOS.Domain;

public sealed record LossBreakdown(string Key, string Label, int Count, decimal Share);
public sealed record FunnelRow(LeadStage Stage, int Entered, int Converted, int Lost, int Open, decimal? ConversionRate);
public sealed record PipelineAnalysisReport(int WindowMonths, int TotalLosses,
    IReadOnlyList<LossBreakdown> ByStage, IReadOnlyList<LossBreakdown> BySource, IReadOnlyList<LossBreakdown> ByMonth,
    IReadOnlyList<FunnelRow> Funnel, IReadOnlyList<string> Notes);

// Read-only loss and funnel analysis. Reads existing follow-up, stage-history and deal
// records; never changes a stage, a loss record or an evaluation.
public static class PipelineAnalysis
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static PipelineAnalysisReport Build(DateOnly today, int months,
        IReadOnlyCollection<BrandFollowUp> followUps,
        IReadOnlyCollection<BrandStageHistory> histories,
        IReadOnlyCollection<Guid> dealBrandIds)
    {
        var since = today.AddMonths(-months);
        var losses = followUps.Where(x => x.LostOn is { } lostOn && lostOn >= since).ToList();
        var total = losses.Count;

        var byStage = losses.GroupBy(x => x.Stage)
            .Select(g => new LossBreakdown(g.Key.ToString(), Pipeline.StageLabel(g.Key), g.Count(), Share(g.Count(), total)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        var bySource = losses.GroupBy(x => x.SourceChannel)
            .Select(g => new LossBreakdown(g.Key.ToString(), Pipeline.SourceLabel(g.Key), g.Count(), Share(g.Count(), total)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        var byMonth = Enumerable.Range(0, months).Select(i =>
        {
            var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-(i + 1));
            var count = losses.Count(x => x.LostOn!.Value.Year == month.Year && x.LostOn!.Value.Month == month.Month);
            return new LossBreakdown($"{month.Year:0000}-{month.Month:00}", DataQuality.Label(month.Year, month.Month), count, Share(count, total));
        }).ToList();

        var windowHistories = histories.Where(x => DateOnly.FromDateTime(x.EnteredAt.UtcDateTime) >= since).ToList();
        var openStageByBrand = histories.Where(x => x.ExitedAt is null)
            .GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First().Stage);
        var lostBrandIds = followUps.Where(x => x.LostOn is not null).Select(x => x.BrandId).ToHashSet();
        var funnel = Enum.GetValues<LeadStage>().Select(stage =>
        {
            var entered = windowHistories.Where(x => x.Stage == stage).Select(x => x.BrandId).Distinct().ToList();
            var converted = entered.Count(x => dealBrandIds.Contains(x));
            var lost = entered.Count(x => lostBrandIds.Contains(x));
            var open = openStageByBrand.Count(kv => kv.Value == stage && !lostBrandIds.Contains(kv.Key));
            return new FunnelRow(stage, entered.Count, converted, lost, open,
                entered.Count == 0 ? null : decimal.Round(converted * 100m / entered.Count, 1));
        }).ToList();

        var notes = new List<string>
        {
            $"Huni son {months} ay içinde aşama geçmişi olan kayıtlardan hesaplanır; bir aşamaya giren marka sonraki aşamalara da girmiş olabilir, oranlar birbirine bağlı değildir.",
            "Kayıp yalnız kaydı yapılmış kayıtları içerir; ilgisizlik veya sessizlik kayıp olarak sayılmaz.",
            "Bu ekran yalnız bilgi verir; aşama, kayıp kaydı veya değerlendirme değiştirmez."
        };
        return new PipelineAnalysisReport(months, total, byStage, bySource, byMonth, funnel, notes);
    }

    private static decimal Share(int count, int total) =>
        total == 0 ? 0 : decimal.Round(count * 100m / total, 1);
}
