using System.Text;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

public static class WeeklyDigestReport
{
    private static readonly System.Globalization.CultureInfo Tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    public static async Task<string> BuildAsync(AppDbContext db, DateTimeOffset now, string webOrigin, CancellationToken ct = default)
    {
        var today = TeamWork.Today(now);
        var body = new StringBuilder();
        body.AppendLine($"Haftalık yönetim özeti — {DataQuality.Label(today.Year, today.Month)}");
        body.AppendLine($"Tarih: {today:dd.MM.yyyy}");
        body.AppendLine();

        body.AppendLine("1) Alacaklar");
        await AppendPromisesAsync(db, today, body, ct);
        body.AppendLine();

        body.AppendLine("2) Onay bekleyenler");
        await AppendApprovalsAsync(db, body, ct);
        body.AppendLine();

        body.AppendLine("3) Veri kalitesi");
        await AppendQualityAsync(db, today, body, ct);
        body.AppendLine();

        body.AppendLine("4) Hedeflerin altındaki markalar");
        await AppendTargetsAsync(db, today, body, ct);
        body.AppendLine();

        body.AppendLine($"Detaylar için sisteme giriş yapın: {webOrigin}/reports");
        body.AppendLine("E-posta tercihlerinizi panelde Bildirimler bölümünden değiştirebilirsiniz.");
        return body.ToString();
    }

    private static async Task AppendPromisesAsync(AppDbContext db, DateOnly today, StringBuilder body, CancellationToken ct)
    {
        var from = today.AddDays(-60);
        var until = today.AddDays(7);
        var promises = await db.CollectionPromises.AsNoTracking()
            .Where(x => !x.IsCancelled && x.PromisedOn >= from && x.PromisedOn <= until)
            .ToListAsync(ct);
        var rows = new List<(DateOnly PromisedOn, bool Overdue, string Brand, decimal Remaining, string Currency, string Owner)>();
        if (promises.Count > 0)
        {
            var periodIds = promises.Select(x => x.MonthlyPerformanceId).Distinct().ToList();
            var periods = await db.MonthlyPerformances.AsNoTracking()
                .Include(x => x.Brand)
                .Include(x => x.Deal)
                .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
                .Where(x => periodIds.Contains(x.Id))
                .ToListAsync(ct);
            var byId = periods.ToDictionary(x => x.Id);
            var ownerIds = promises.Select(x => x.OwnerId).Distinct().ToList();
            var owners = await db.UserAccounts.AsNoTracking().Where(x => ownerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
            foreach (var promise in promises)
            {
                if (!byId.TryGetValue(promise.MonthlyPerformanceId, out var period)) continue;
                var balance = CollectionPromises.Balance(period, today);
                if (balance is null) continue;
                var overdue = balance.State == "Overdue";
                var approaching = balance.State == "Waiting" && promise.PromisedOn <= today.AddDays(7);
                if (!overdue && !approaching) continue;
                rows.Add((promise.PromisedOn, overdue, period.Brand?.Name ?? "Marka", balance.Remaining,
                    period.Collection?.Currency ?? period.Deal?.Currency ?? "", owners.TryGetValue(promise.OwnerId, out var owner) ? owner : "-"));
            }
        }
        if (rows.Count == 0)
        {
            body.AppendLine("Gecikmiş veya yaklaşan ödeme sözü yok.");
            return;
        }
        foreach (var row in rows.OrderBy(x => !x.Overdue).ThenBy(x => x.PromisedOn).Take(15))
        {
            var state = row.Overdue ? "GECİKMİŞ" : "yaklaşan";
            body.AppendLine($"- {row.Brand}: {row.Remaining.ToString("N2", Tr)} {row.Currency} — vade {row.PromisedOn:dd.MM.yyyy}, sorumlu {row.Owner} ({state})");
        }
        if (rows.Count > 15) body.AppendLine($"- … ve {rows.Count - 15} sözleşme daha.");
    }

    private static async Task AppendApprovalsAsync(AppDbContext db, StringBuilder body, CancellationToken ct)
    {
        var underReview = await (from p in db.MonthlyPerformances.AsNoTracking()
                                 join b in db.Brands.AsNoTracking() on p.BrandId equals b.Id
                                 where p.Status == MonthlyPerformanceStatus.UnderReview
                                 orderby p.UpdatedAt
                                 select new { b.Name, p.Year, p.Month }).ToListAsync(ct);
        var pendingBrand = await (from p in db.MonthlyPerformances.AsNoTracking()
                                  join b in db.Brands.AsNoTracking() on p.BrandId equals b.Id
                                  where p.Status == MonthlyPerformanceStatus.Approved
                                      && !db.PeriodApprovals.Any(a => a.BrandId == p.BrandId && a.Year == p.Year && a.Month == p.Month)
                                  orderby p.UpdatedAt
                                  select new { b.Name, p.Year, p.Month }).ToListAsync(ct);
        if (underReview.Count == 0 && pendingBrand.Count == 0)
        {
            body.AppendLine("Onay bekleyen kayıt yok.");
            return;
        }
        foreach (var row in underReview.Take(10))
            body.AppendLine($"- Ekibin incelemesi bekliyor: {row.Name} — {DataQuality.Label(row.Year, row.Month)}");
        foreach (var row in pendingBrand.Take(10))
            body.AppendLine($"- Müşteri onayı bekliyor: {row.Name} — {DataQuality.Label(row.Year, row.Month)}");
        var extra = Math.Max(0, underReview.Count - 10) + Math.Max(0, pendingBrand.Count - 10);
        if (extra > 0) body.AppendLine($"- … ve {extra} kayıt daha.");
    }

    private static async Task AppendQualityAsync(AppDbContext db, DateOnly today, StringBuilder body, CancellationToken ct)
    {
        var report = await OvoGrowthOS.Api.Features.WorkflowEndpoints.BuildQualityReport(db, today.Year, today.Month, null);
        if (report.Summary.Total == 0)
        {
            body.AppendLine("Bu ay için beklenen kayıt yok.");
            return;
        }
        body.AppendLine($"- {report.Label}: {report.Summary.Attention} dikkat bekliyor, {report.Summary.Missing} kayıt eksik, {report.Summary.Ready} hazır");
    }

    private static async Task AppendTargetsAsync(AppDbContext db, DateOnly today, StringBuilder body, CancellationToken ct)
    {
        var targets = await db.MonthlyTargets.AsNoTracking()
            .Where(x => x.Year == today.Year && x.Month == today.Month)
            .ToListAsync(ct);
        if (targets.Count == 0)
        {
            body.AppendLine("Bu ay için hedef kaydı yok.");
            return;
        }
        var brandIds = targets.Select(x => x.BrandId).Distinct().ToList();
        var brands = await db.Brands.AsNoTracking().Where(x => brandIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var periods = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal)
            .Where(x => x.Year == today.Year && x.Month == today.Month && brandIds.Contains(x.BrandId))
            .ToListAsync(ct);
        var byBrand = periods.GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First());
        var lines = new List<string>();
        foreach (var target in targets)
        {
            byBrand.TryGetValue(target.BrandId, out var period);
            var comparison = MonthlyTargetEngine.Compare(target, period);
            foreach (var metric in comparison.Metrics.Where(x => x.NeedsAttention == true))
            {
                var name = brands.TryGetValue(target.BrandId, out var brand) ? brand : "Marka";
                lines.Add(metric.Metric switch
                {
                    TargetMetric.NetRevenue => $"- {name}: net ciro hedefin {(0m - (metric.RelativeDifference ?? 0m)).ToString("P0", Tr)} altında",
                    TargetMetric.AdSpend => $"- {name}: reklam bütçesi hedefin {(metric.RelativeDifference ?? 0m).ToString("P0", Tr)} üzerinde",
                    _ => $"- {name}: katkı marjı hedefin {(0m - (metric.PercentagePointDifference ?? 0m)).ToString("0.#", Tr)} puan altında"
                });
            }
        }
        if (lines.Count == 0)
        {
            body.AppendLine("Hedef altına düşen marka yok.");
            return;
        }
        foreach (var line in lines.Take(10)) body.AppendLine(line);
        if (lines.Count > 10) body.AppendLine($"- … ve {lines.Count - 10} sapma daha.");
    }
}
