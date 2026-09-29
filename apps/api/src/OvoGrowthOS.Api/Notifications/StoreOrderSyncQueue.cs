using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

// Once per month, syncs the previous period for every brand with saved store API settings.
// Brand-level outcomes are tracked separately: a failed brand is retried after a cooldown until it succeeds,
// and the period summary/notification is written only on the first attempt.
public sealed class StoreOrderSyncQueue(AppDbContext db, IDataProtectionProvider protection, IStoreTokenClient tokens, IStoreOrderClient orderClient)
{
    private const string SystemActor = "sistem (otomatik)";
    private static readonly TimeSpan RetryCooldown = TimeSpan.FromHours(6);

    public async Task<int> RunDue(DateTimeOffset now, CancellationToken ct = default)
    {
        var trNow = now.ToOffset(TimeSpan.FromHours(3));
        var target = trNow.Date.AddMonths(-1);
        var periodKey = $"{target.Year:0000}-{target.Month:00}";
        if (!StoreOrderPeriod.TryParse(periodKey, out var period)) return 0;
        var rows = await db.BrandApiSettings.AsNoTracking()
            .Join(db.Brands.AsNoTracking(), s => s.BrandId, b => b.Id, (s, b) => new { s.BrandId, b.Name })
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;
        var admins = await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && !x.InvitationPending && x.Role == "Admin").ToListAsync(ct);
        var retryBefore = now - RetryCooldown;
        var summaryExists = await db.AuditRecords.AsNoTracking()
            .AnyAsync(x => x.Action == "StoreOrdersAutoSync" && x.EntityId == periodKey, ct);
        var synced = 0; var failed = 0; var added = 0; var updated = 0; var attempted = 0; var fixedCount = 0;
        var failures = new List<string>();
        foreach (var row in rows)
        {
            if (await SyncedFor(db, row.BrandId, periodKey, ct)) continue;
            var failureKey = $"{row.BrandId}:{periodKey}";
            var lastFailure = await db.AuditRecords.AsNoTracking()
                .Where(x => x.Action == "StoreOrdersAutoSyncBrand" && x.EntityId == failureKey)
                .OrderByDescending(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefaultAsync(ct);
            if (lastFailure is not null && lastFailure >= retryBefore) continue;
            attempted++;
            var outcome = await WorkflowEndpoints.ExecuteStoreOrderSync(db, protection, tokens, orderClient, row.BrandId, period, SystemActor, ct);
            if (outcome.Ok)
            {
                synced++; added += outcome.Added; updated += outcome.Updated;
                if (lastFailure is not null)
                {
                    var fixedKey = $"store-sync-fixed:{periodKey}:{row.BrandId}";
                    foreach (var admin in admins)
                    {
                        if (await db.UserNotifications.AsNoTracking().AnyAsync(x => x.UserId == admin.Id && x.EventKey == fixedKey, ct)) continue;
                        db.Add(new UserNotification
                        {
                            UserId = admin.Id, Kind = NotificationKind.StoreSync, EventKey = fixedKey,
                            CreatedAt = now, Email = admin.Email, AccountVersion = admin.TokenVersion, EmailStatus = null
                        });
                        fixedCount++;
                    }
                    try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); }
                }
            }
            else
            {
                failed++; failures.Add($"{row.Name}: {outcome.Error}");
                db.AuditRecords.Add(new AuditRecord
                {
                    UserId = SystemActor, Action = "StoreOrdersAutoSyncBrand", EntityType = "Brand", EntityId = failureKey,
                    NewValueJson = JsonSerializer.Serialize(new { period = periodKey, error = outcome.Error }),
                    Reason = $"{row.Name} ({periodKey}) otomatik senkronu başarısız: {outcome.Error}", CreatedAt = now
                });
                try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); }
            }
        }
        if (!summaryExists && attempted > 0)
        {
            var reason = failed == 0
                ? $"Otomatik sipariş senkronu ({periodKey}) tamamlandı: {synced} marka, {added} yeni, {updated} güncellenen kayıt."
                : $"Otomatik sipariş senkronu ({periodKey}): {synced} marka güncellendi, {failed} marka başarısız oldu.";
            db.AuditRecords.Add(new AuditRecord
            {
                UserId = SystemActor, Action = "StoreOrdersAutoSync", EntityType = "Period", EntityId = periodKey,
                NewValueJson = JsonSerializer.Serialize(new { synced, failed, added, updated, failures }),
                Reason = reason, CreatedAt = now
            });
            foreach (var admin in admins)
                db.Add(new UserNotification
                {
                    UserId = admin.Id, Kind = NotificationKind.StoreSync, EventKey = $"store-sync:{periodKey}",
                    CreatedAt = now, Email = admin.Email, AccountVersion = admin.TokenVersion, EmailStatus = null
                });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); return 0; }
            return admins.Count;
        }
        return fixedCount;
    }

    private static Task<bool> SyncedFor(AppDbContext db, Guid brandId, string periodKey, CancellationToken ct)
    {
        var brandKey = brandId.ToString();
        var marker = $"\"period\":\"{periodKey}\"";
        return db.AuditRecords.AsNoTracking().AnyAsync(x =>
            x.Action == "StoreOrdersSynced" && x.EntityId == brandKey && x.NewValueJson.Contains(marker), ct);
    }
}
