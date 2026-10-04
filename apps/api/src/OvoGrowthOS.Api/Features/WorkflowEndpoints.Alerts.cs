using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapAlerts(WebApplication app)
    {
        app.MapGet("/api/alerts", ReadAlerts).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ReadAlerts(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TeamWork.Today(now);
        var y = today.Year; var m = today.Month;
        var periodLabel = DataQuality.Label(y, m);

        var quality = await BuildQualityReport(db, y, m, null);
        var missing = quality.Brands
            .Where(x => x.Readiness == "missing" && x.DealId is not null)
            .Select(x => new MissingCloseInput(x.BrandId, x.BrandName)).ToList();

        var closedStatuses = new[] { MonthlyPerformanceStatus.Locked, MonthlyPerformanceStatus.Invoiced, MonthlyPerformanceStatus.Paid };
        var closed = await db.MonthlyPerformances.AsNoTracking()
            .Where(x => closedStatuses.Contains(x.Status))
            .Include(x => x.Deal)
            .Include(x => x.Collection).ThenInclude(x => x!.Payments)
            .Include(x => x.Collection).ThenInclude(x => x!.Promise)
            .ToListAsync();
        var brandNames = await db.Brands.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);

        var receivables = new List<OverdueReceivableInput>();
        var promises = new List<OverduePromiseInput>();
        foreach (var currency in closed.Select(x => x.Collection?.Currency ?? x.Deal?.Currency).OfType<string>()
                     .Where(c => c.Length > 0).Select(c => c.ToUpperInvariant()).Distinct())
        {
            var plan = CollectionPlanning.Build(closed, today, currency, 4);
            foreach (var row in plan.Items)
            {
                var name = brandNames.GetValueOrDefault(row.BrandId, row.BrandName);
                var promise = row.Promise;
                if (promise is { State: CollectionPromises.ReminderOverdue, Remaining: > 0 })
                    promises.Add(new OverduePromiseInput(row.BrandId, name,
                        today.DayNumber - promise.PromisedOn.DayNumber, promise.Remaining, currency));
                else if (row.Balance.OverdueDays > 0)
                    receivables.Add(new OverdueReceivableInput(row.BrandId, name, row.Balance.OverdueDays, row.Balance.Outstanding, currency));
            }
        }

        var latestByBrand = closed
            .GroupBy(x => x.BrandId)
            .Select(g => g.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).First())
            .ToList();
        var targetRows = await db.Evaluations.AsNoTracking()
            .Where(x => x.Status == EvaluationStatus.Approved || x.Status == EvaluationStatus.Analyzed)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new { x.BrandId, x.RecommendedTargetMer })
            .ToListAsync();
        var breakEvenByBrand = new Dictionary<Guid, decimal>();
        foreach (var row in targetRows) breakEvenByBrand.TryAdd(row.BrandId, row.RecommendedTargetMer);
        var merBreaches = new List<MerBreachInput>();
        foreach (var row in latestByBrand)
        {
            if (row.TotalAdSpend <= 0 || row.Mer <= 0) continue;
            if (!breakEvenByBrand.TryGetValue(row.BrandId, out var be) || be <= 0) continue;
            if (row.Mer >= be) continue;
            merBreaches.Add(new MerBreachInput(row.BrandId,
                brandNames.GetValueOrDefault(row.BrandId, "Marka bilgisi yok"), row.Id,
                $"{row.Month:00}/{row.Year}", row.Mer, be));
        }

        var follows = await db.BrandFollowUps.AsNoTracking().Where(x => x.LostOn == null).ToDictionaryAsync(x => x.BrandId);
        var pipelineBrands = await db.Brands.AsNoTracking()
            .Where(x => PipelineStatuses.Contains(x.Status)).Select(x => new { x.Id }).ToListAsync();
        var histories = (await db.BrandStageHistories.AsNoTracking().Where(x => x.ExitedAt == null).ToListAsync())
            .GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.ToList());
        var timedOut = 0; var longestLeadDays = 0;
        foreach (var brand in pipelineBrands.Where(x => follows.ContainsKey(x.Id)))
        {
            var openRow = histories.GetValueOrDefault(brand.Id)?.FirstOrDefault();
            if (openRow is null) continue;
            var days = Pipeline.StageDays(openRow, now);
            if (days is not > AlertEngine.StageTimeoutDays) continue;
            timedOut++;
            longestLeadDays = Math.Max(longestLeadDays, days.Value);
        }

        var overdueTasks = await db.WorkTasks.AsNoTracking()
            .CountAsync(x => x.CompletedAt == null && x.DueOn < today);

        var report = AlertEngine.Build(today, periodLabel, missing, receivables, promises, merBreaches,
            timedOut, longestLeadDays, overdueTasks);
        const int maxItems = 50;
        return Results.Ok(new
        {
            today = report.Today.ToString("yyyy-MM-dd"),
            periodLabel = report.PeriodLabel,
            summary = report.Summary,
            items = report.Items.Take(maxItems),
            hidden = Math.Max(0, report.Items.Count - maxItems),
            notes = new[]
            {
                "Uyarılar mevcut kayıtlardan okunur; hiçbir kaydı değiştirmez, görev açmaz ve dönem onaylamaz.",
                "Reklam verimliliği uyarısı yalnız kilitlenmiş dönemlerin gerçekleşen MER değeri ile onaylı değerlendirme başa baş hedefini karşılaştırır; bütçe veya fiyat kararı vermez.",
                "Alacak ve ödeme sözü gecikmeleri tahsilat kaydından gelir; tahsilat tutarını kendisi değiştirmez."
            }
        });
    }
}
