using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

// Once per month, syncs the previous period for every brand with saved store API settings.
public sealed class StoreOrderSyncQueue(AppDbContext db, IDataProtectionProvider protection, IStoreTokenClient tokens, IStoreOrderClient orderClient)
{
    private const string SystemActor = "sistem (otomatik)";

    public async Task<int> RunDue(DateTimeOffset now, CancellationToken ct = default)
    {
        var trNow = now.ToOffset(TimeSpan.FromHours(3));
        var target = trNow.Date.AddMonths(-1);
        var periodKey = $"{target.Year:0000}-{target.Month:00}";
        if (!StoreOrderPeriod.TryParse(periodKey, out var period)) return 0;
        if (await db.AuditRecords.AsNoTracking().AnyAsync(x => x.Action == "StoreOrdersAutoSync" && x.EntityId == periodKey, ct)) return 0;
        var rows = await db.BrandApiSettings.AsNoTracking()
            .Join(db.Brands.AsNoTracking(), s => s.BrandId, b => b.Id, (s, b) => new { s.BrandId, b.Name })
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;
        var synced = 0; var failed = 0; var added = 0; var updated = 0;
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var outcome = await WorkflowEndpoints.ExecuteStoreOrderSync(db, protection, tokens, orderClient, row.BrandId, period, SystemActor, ct);
            if (outcome.Ok) { synced++; added += outcome.Added; updated += outcome.Updated; }
            else { failed++; failures.Add($"{row.Name}: {outcome.Error}"); }
        }
        var reason = failed == 0
            ? $"Otomatik sipariş senkronu ({periodKey}) tamamlandı: {synced} marka, {added} yeni, {updated} güncellenen kayıt."
            : $"Otomatik sipariş senkronu ({periodKey}): {synced} marka güncellendi, {failed} marka başarısız oldu.";
        db.AuditRecords.Add(new AuditRecord
        {
            UserId = SystemActor, Action = "StoreOrdersAutoSync", EntityType = "Period", EntityId = periodKey,
            NewValueJson = JsonSerializer.Serialize(new { synced, failed, added, updated, failures }),
            Reason = reason, CreatedAt = now
        });
        var admins = await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && !x.InvitationPending && x.Role == "Admin").ToListAsync(ct);
        foreach (var admin in admins)
            db.Add(new UserNotification
            {
                UserId = admin.Id, Kind = NotificationKind.StoreSync, EventKey = $"store-sync:{periodKey}",
                CreatedAt = now, Email = admin.Email, AccountVersion = admin.TokenVersion, EmailStatus = null
            });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return 0; }
        return admins.Count;
    }
}
