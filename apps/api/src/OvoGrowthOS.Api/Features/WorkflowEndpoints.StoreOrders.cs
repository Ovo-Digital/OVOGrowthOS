using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record StoreOrderSyncRequest(string Period);
public sealed record StoreOrderBackfillRequest(int Months);

internal sealed record StoreOrderSyncOutcome(bool Ok, bool Conflict, string Error, int Added, int Updated, bool Truncated);

public static partial class WorkflowEndpoints
{
    private static void MapStoreOrders(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/store-orders", GetStoreOrders).RequireAuthorization("ReadAccess");
        app.MapPost("/api/brands/{id:guid}/store-orders/sync", SyncStoreOrders).RequireAuthorization("AdminOnly").RequireRateLimiting("admin-action");
        app.MapPost("/api/brands/{id:guid}/store-orders/backfill", BackfillStoreOrders).RequireAuthorization("AdminOnly").RequireRateLimiting("admin-action");
    }

    private static async Task<IResult> GetStoreOrders(Guid id, string? period, int? page, AppDbContext db, IDataProtectionProvider protection, CancellationToken ct)
    {
        if (!StoreOrderPeriod.TryParse(period, out var parsed)) return Results.BadRequest(new { error = "Dönem 'YYYY-AA' biçiminde olmalı, örneğin 2026-08." });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        return Results.Ok(await StoreOrdersPayload(db, id, parsed, page ?? 1, protection, ct));
    }

    private static async Task<IResult> SyncStoreOrders(Guid id, StoreOrderSyncRequest r, AppDbContext db, IDataProtectionProvider protection,
        IStoreTokenClient tokens, IStoreOrderClient orderClient, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!StoreOrderPeriod.TryParse(r.Period, out var period)) return Results.BadRequest(new { error = "Dönem 'YYYY-AA' biçiminde olmalı, örneğin 2026-08." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        if (!await db.Brands.AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        var outcome = await ExecuteStoreOrderSync(db, protection, tokens, orderClient, id, period, User(actor), ct);
        if (!outcome.Ok)
            return outcome.Conflict ? Results.Conflict(new { error = outcome.Error }) : Results.BadRequest(new { error = outcome.Error });
        var payload = await StoreOrdersPayload(db, id, period, 1, protection, ct);
        var message = $"Dönem siparişleri güncellendi: {outcome.Added} yeni, {outcome.Updated} güncellenen kayıt."
            + (outcome.Truncated ? " Sayfalamadaki üst sınıra gelindi; daha uzun dönemler için dönemleri ayrı ayrı çekin." : "");
        return Results.Ok(new { message, payload });
    }

    private static async Task<IResult> BackfillStoreOrders(Guid id, StoreOrderBackfillRequest r, AppDbContext db, IDataProtectionProvider protection,
        IStoreTokenClient tokens, IStoreOrderClient orderClient, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (r.Months is < 1 or > 12) return Results.BadRequest(new { error = "Geriye dönük çekim 1 ile 12 ay arasında olmalı." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        if (!await db.Brands.AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        var trNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3));
        var results = new List<object>();
        var okCount = 0; var failedCount = 0;
        for (var monthsBack = r.Months; monthsBack >= 1; monthsBack--)
        {
            var date = trNow.Date.AddMonths(-monthsBack);
            if (!StoreOrderPeriod.TryParse($"{date.Year:0000}-{date.Month:00}", out var period)) continue;
            var outcome = await ExecuteStoreOrderSync(db, protection, tokens, orderClient, id, period, User(actor), ct);
            if (outcome.Ok) { okCount++; results.Add(new { period = period.Key, ok = true, added = outcome.Added, updated = outcome.Updated, error = "" }); }
            else { failedCount++; results.Add(new { period = period.Key, ok = false, added = 0, updated = 0, error = outcome.Error }); }
        }
        var message = failedCount == 0
            ? $"Geriye dönük çekim tamamlandı: {okCount} dönem güncellendi."
            : $"Geriye dönük çekim: {okCount} dönem güncellendi, {failedCount} dönem başarısız oldu.";
        return Results.Ok(new { message, results });
    }

    // Shared by the manual endpoint and the scheduled queue; errors are already safe Turkish text.
    internal static async Task<StoreOrderSyncOutcome> ExecuteStoreOrderSync(AppDbContext db, IDataProtectionProvider protection,
        IStoreTokenClient tokens, IStoreOrderClient orderClient, Guid id, StoreOrderPeriod period, string actorEmail, CancellationToken ct)
    {
        StoreOrderSyncOutcome Fail(string error, bool conflict = false) { db.ChangeTracker.Clear(); return new(false, conflict, error, 0, 0, false); }
        var settings = await db.BrandApiSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id, ct);
        if (settings is null || !IsApiSettingsValid(settings, protection))
            return Fail("Önce marka API ayarlarını kaydedip bağlantı doğrulamasını deneyin.");
        if (!TryUnprotect(protection, settings.ProtectedPassword, out var password))
            return Fail("Önce marka API ayarlarını kaydedip bağlantı doğrulamasını deneyin.");
        string token;
        if (settings.Platform == StorePlatform.Shopify)
        {
            token = password;
        }
        else
        {
            var storeToken = await tokens.CreateTokenAsync(settings.StoreUrl, settings.ApiUser, Convert.ToBase64String(Encoding.UTF8.GetBytes(password)), ct);
            if (!storeToken.Accepted)
                return Fail("Mağaza bağlantısı doğrulanamadı. Mağaza adresini ve API ayarlarını kontrol edin.");
            token = storeToken.Token;
        }
        var fetched = await orderClient.FetchAsync(token, settings.StoreUrl, period, settings.Platform, ct);
        if (!fetched.Accepted)
            return Fail("Mağaza siparişleri okunamadı. Mağaza adresini ve API yetkilerini kontrol edip tekrar deneyin.");
        foreach (var order in fetched.Orders)
        {
            if (!period.Contains(order.PlacedOnUtc))
                return Fail($"Mağaza beklenmedik bir sipariş döndürdü (sipariş #{order.OrderNumber}); dönem sınırı dışında kayıt aktarılmadı.");
            if (order.OrderTotal < 0 || order.PaidAmount < 0 || order.RefundedAmount < 0)
                return Fail($"Mağaza kaydında eksi tutar var (sipariş #{order.OrderNumber}); aktarım durduruldu, mevcut kayıtlar değişmedi.");
            if (order.Currency.Length is not (0 or 3))
                return Fail($"Mağaza kaydında okunamayan para birimi var (sipariş #{order.OrderNumber}); aktarım durduruldu, mevcut kayıtlar değişmedi.");
        }
        var now = DateTimeOffset.UtcNow;
        var rows = await db.StoreOrderStagings.Where(x => x.BrandId == id && x.PlacedOnUtc >= period.UtcStart && x.PlacedOnUtc < period.UtcEnd).ToListAsync(ct);
        var added = 0; var updated = 0;
        foreach (var order in fetched.Orders)
        {
            var row = rows.SingleOrDefault(x => x.SourceOrderId == order.SourceOrderId);
            if (row is null)
            {
                row = new StoreOrderStaging { BrandId = id, SourceOrderId = order.SourceOrderId };
                db.Add(row); rows.Add(row); added++;
            }
            else updated++;
            row.SourceStoreId = order.SourceStoreId; row.OrderNumber = order.OrderNumber; row.PlacedOnUtc = order.PlacedOnUtc;
            row.Currency = order.Currency; row.OrderTotal = order.OrderTotal; row.PaidAmount = order.PaidAmount;
            row.RefundedAmount = order.RefundedAmount; row.OrderStatus = order.OrderStatus; row.PaymentStatus = order.PaymentStatus;
            row.ImportedAt = now;
        }
        db.AuditRecords.Add(new AuditRecord { UserId = actorEmail, Action = "StoreOrdersSynced", EntityType = "Brand", EntityId = id.ToString(),
            NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { period = period.Key, added, updated }) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Fail("Siparişler başka bir aktarım sırasında değişmiş. Sayfayı yenileyip tekrar deneyin.", conflict: true); }
        return new(true, false, "", added, updated, fetched.Truncated);
    }

    private static async Task<object> StoreOrdersPayload(AppDbContext db, Guid brandId, StoreOrderPeriod period, int page, IDataProtectionProvider protection, CancellationToken ct)
    {
        var rows = await db.StoreOrderStagings.AsNoTracking()
            .Where(x => x.BrandId == brandId && x.PlacedOnUtc >= period.UtcStart && x.PlacedOnUtc < period.UtcEnd)
            .OrderBy(x => x.PlacedOnUtc).ThenBy(x => x.OrderNumber)
            .ToListAsync(ct);
        var currencies = rows.Select(x => x.Currency).Where(x => x.Length > 0).Distinct().ToList();
        var summary = StoreOrderSummary.Summarize(rows, currencies.Count == 1 ? currencies[0] : "");
        var panelQuery = db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.BrandId == brandId && x.Year == period.Year && x.Month == period.Month);
        decimal? panelSales = await panelQuery.AnyAsync(ct) ? await panelQuery.SumAsync(x => x.GrossSales, ct) : null;
        var settings = await db.BrandApiSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId, ct);
        var configured = settings is not null && IsApiSettingsValid(settings, protection);
        const int size = 50;
        var current = Math.Max(1, page);
        return new
        {
            period = period.Key,
            configured,
            lastSyncAt = rows.Count == 0 ? null : rows.Max(x => (DateTimeOffset?)x.ImportedAt),
            summary,
            panel = summary.ComparePanel(panelSales),
            total = rows.Count,
            page = current,
            pageSize = size,
            items = rows.Skip((current - 1) * size).Take(size).Select(x => new
            {
                x.OrderNumber, placedOnUtc = x.PlacedOnUtc, x.Currency, x.OrderTotal, x.PaidAmount, x.RefundedAmount,
                x.OrderStatus, x.PaymentStatus, x.ImportedAt
            })
        };
    }
}
