using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

// Once per month, reads the previous period's ad spend for every brand with valid ad settings.
// Brand-level outcomes are retried after a cooldown until they succeed; the summary is written on the first attempt.
public sealed class AdSpendSyncQueue(AppDbContext db, IDataProtectionProvider protection, IAdSpendClient adClient)
{
    private const string SystemActor = "sistem (otomatik)";
    private static readonly TimeSpan RetryCooldown = TimeSpan.FromHours(6);

    public async Task<int> RunDue(DateTimeOffset now, CancellationToken ct = default)
    {
        var trNow = now.ToOffset(TimeSpan.FromHours(3));
        var target = trNow.Date.AddMonths(-1);
        var periodKey = $"{target.Year:0000}-{target.Month:00}";
        if (!StoreOrderPeriod.TryParse(periodKey, out var period)) return 0;
        var rows = await db.BrandAdSettings.AsNoTracking()
            .Join(db.Brands.AsNoTracking(), s => s.BrandId, b => b.Id, (s, b) => new { s.BrandId, b.Name, s.Platform })
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;
        var admins = await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && !x.InvitationPending && x.Role == "Admin").ToListAsync(ct);
        var retryBefore = now - RetryCooldown;
        var summaryExists = await db.AuditRecords.AsNoTracking()
            .AnyAsync(x => x.Action == "AdSpendAutoSyncSummary" && x.EntityId == periodKey, ct);
        var read = 0; var failed = 0; var attempted = 0; var fixedCount = 0;
        var items = new List<object>(); var texts = new List<string>(); var failures = new List<string>();
        foreach (var row in rows)
        {
            var key = $"{row.BrandId}:{row.Platform}:{periodKey}";
            if (await db.AuditRecords.AsNoTracking().AnyAsync(x =>
                x.Action == "AdSpendAutoSync" && x.EntityId == key && x.NewValueJson.Contains("\"ok\":true"), ct)) continue;
            var lastFailureAt = await db.AuditRecords.AsNoTracking()
                .Where(x => x.Action == "AdSpendAutoSync" && x.EntityId == key && x.NewValueJson.Contains("\"ok\":false"))
                .OrderByDescending(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefaultAsync(ct);
            if (lastFailureAt is not null && lastFailureAt >= retryBefore) continue;
            var hadFailure = lastFailureAt is not null;
            attempted++;
            var settings = await db.BrandAdSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == row.BrandId && x.Platform == row.Platform, ct);
            AdSpendResult? result = null; string? error = null; AdConnection connection = null!;
            if (settings is null || !WorkflowEndpoints.IsAdSettingsValid(settings, protection))
                error = "Reklam bağlantı ayarları eksik veya jeton çözülemiyor.";
            else if (!WorkflowEndpoints.TryBuildConnection(settings, protection, out connection))
                error = "Reklam bağlantı ayarları eksik veya jeton çözülemiyor.";
            else
            {
                result = await adClient.FetchAsync(connection, period, ct);
                if (result is null) error = $"{AdSettings.PlatformLabel(row.Platform)} harcaması okunamadı. Bağlantı ayarlarını ve hesap numarasını kontrol edin.";
            }
            var campaignCount = 0;
            if (error is null)
            {
                // Campaign breakdown is stored best effort; a missing breakdown never fails the period read.
                var campaigns = await adClient.FetchCampaignsAsync(connection, period, ct);
                if (campaigns is not null) campaignCount = await StoreCampaignsAsync(db, row.BrandId, row.Platform, period, campaigns, now, ct);
            }
            var ok = error is null && result is not null;
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = SystemActor, Action = "AdSpendAutoSync", EntityType = "BrandAdSettings", EntityId = key,
                NewValueJson = JsonSerializer.Serialize(new
                {
                    period = periodKey, platform = row.Platform.ToString(), ok,
                    amount = result?.Amount ?? 0m, currency = result?.Currency ?? "", source = result?.Source ?? "",
                    campaigns = campaignCount, error = error ?? ""
                }),
                Reason = ok
                    ? $"{row.Name} ({AdSettings.PlatformLabel(row.Platform)}) {periodKey} reklam harcaması okundu: {result!.Amount.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))} {result.Currency}".TrimEnd()
                    : $"{row.Name} ({AdSettings.PlatformLabel(row.Platform)}) {periodKey} reklam harcaması okunamadı: {error}",
                CreatedAt = now
            });
            if (ok)
            {
                read++;
                items.Add(new { brand = row.Name, platform = row.Platform.ToString(), amount = result!.Amount, currency = result.Currency });
                texts.Add($"{row.Name} {result.Amount.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))} {result.Currency}".TrimEnd());
                if (hadFailure)
                {
                    var fixedKey = $"ad-sync-fixed:{periodKey}:{row.BrandId}:{row.Platform}";
                    foreach (var admin in admins)
                    {
                        if (await db.UserNotifications.AsNoTracking().AnyAsync(x => x.UserId == admin.Id && x.EventKey == fixedKey, ct)) continue;
                        db.Add(new UserNotification
                        {
                            UserId = admin.Id, Kind = NotificationKind.AdSpendSync, EventKey = fixedKey,
                            CreatedAt = now, Email = admin.Email, AccountVersion = admin.TokenVersion, EmailStatus = null
                        });
                        fixedCount++;
                    }
                }
            }
            else
            {
                failed++;
                failures.Add($"{row.Name}: {error}");
            }
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        }
        if (!summaryExists && attempted > 0)
        {
            var summary = JsonSerializer.Serialize(new { read, failed, items, failures });
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = SystemActor, Action = "AdSpendAutoSyncSummary", EntityType = "Period", EntityId = periodKey,
                NewValueJson = summary, CreatedAt = now,
                Reason = BuildReason(periodKey, texts, failed)
            });
            foreach (var admin in admins)
                db.Add(new UserNotification
                {
                    UserId = admin.Id, Kind = NotificationKind.AdSpendSync, EventKey = $"ad-sync:{periodKey}",
                    CreatedAt = now, Email = admin.Email, AccountVersion = admin.TokenVersion, EmailStatus = null
                });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); return 0; }
            return admins.Count;
        }
        return fixedCount;
    }

    private static async Task<int> StoreCampaignsAsync(AppDbContext db, Guid brandId, AdPlatform platform,
        StoreOrderPeriod period, IEnumerable<AdCampaignResult> campaigns, DateTimeOffset now, CancellationToken ct)
    {
        var rows = AdCampaigns.Normalize(campaigns, "TRY").ToList();
        var existing = await db.AdCampaignSpends
            .Where(x => x.BrandId == brandId && x.Year == period.Year && x.Month == period.Month && x.Platform == platform)
            .ToListAsync(ct);
        if (existing.Count == 0 && rows.Count == 0) return 0;
        if (existing.Count > 0) db.AdCampaignSpends.RemoveRange(existing);
        foreach (var row in rows)
            db.AdCampaignSpends.Add(new AdCampaignSpend
            {
                BrandId = brandId, Year = period.Year, Month = period.Month, Platform = platform,
                CampaignName = row.CampaignName, Spend = row.Spend, Currency = row.Currency, ReadAt = now
            });
        return rows.Count;
    }

    private static string BuildReason(string periodKey, List<string> texts, int failed)
    {
        var parts = texts.Take(6).ToList();
        if (texts.Count > 6) parts.Add($"+{texts.Count - 6} marka daha");
        var body = texts.Count > 0 ? $"{string.Join(", ", parts)} okundu" : "hiç marka okunamadı";
        return failed == 0
            ? $"Otomatik reklam harcaması okundu ({periodKey}): {body}."
            : $"Otomatik reklam harcaması senkronu ({periodKey}): {body}; {failed} marka başarısız.";
    }
}
