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

public static partial class WorkflowEndpoints
{
    private static void MapStoreOrders(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/store-orders", GetStoreOrders).RequireAuthorization("ReadAccess");
        app.MapPost("/api/brands/{id:guid}/store-orders/sync", SyncStoreOrders).RequireAuthorization("AdminOnly").RequireRateLimiting("admin-action");
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
        var settings = await db.BrandApiSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id, ct);
        if (settings is null || !IsStoreUrl(settings.StoreUrl) || !SmtpSettings.IsAddress(settings.ApiUser) || !TryUnprotect(protection, settings.ProtectedPassword, out var password))
            return Results.BadRequest(new { error = "Önce marka API ayarlarını kaydedip bağlantı doğrulamasını deneyin." });
        var token = await tokens.CreateTokenAsync(settings.StoreUrl, settings.ApiUser, Convert.ToBase64String(Encoding.UTF8.GetBytes(password)), ct);
        if (!token.Accepted)
            return Results.BadRequest(new { error = "Mağaza bağlantısı doğrulanamadı. API ayarlarını ve mağaza panelindeki API kullanıcısını kontrol edin." });
        var fetched = await orderClient.FetchAsync(token.Token, settings.StoreUrl, period, ct);
        if (!fetched.Accepted)
            return Results.BadRequest(new { error = "Mağaza siparişleri okunamadı. Mağaza adresini ve API yetkilerini kontrol edip tekrar deneyin." });
        foreach (var order in fetched.Orders)
        {
            if (!period.Contains(order.PlacedOnUtc))
                return Results.BadRequest(new { error = $"Mağaza beklenmedik bir sipariş döndürdü (sipariş #{order.OrderNumber}); dönem sınırı dışında kayıt aktarılmadı." });
            if (order.OrderTotal < 0 || order.PaidAmount < 0 || order.RefundedAmount < 0)
                return Results.BadRequest(new { error = $"Mağaza kaydında eksi tutar var (sipariş #{order.OrderNumber}); aktarım durduruldu, mevcut kayıtlar değişmedi." });
            if (order.Currency.Length is not (0 or 3))
                return Results.BadRequest(new { error = $"Mağaza kaydında okunamayan para birimi var (sipariş #{order.OrderNumber}); aktarım durduruldu, mevcut kayıtlar değişmedi." });
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
        Audit(db, actor, "StoreOrdersSynced", "Brand", id, null, new { period = period.Key, added, updated });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "Siparişler başka bir aktarım sırasında değişmiş. Sayfayı yenileyip tekrar deneyin." }); }
        var payload = await StoreOrdersPayload(db, id, period, 1, protection, ct);
        var message = $"Dönem siparişleri güncellendi: {added} yeni, {updated} güncellenen kayıt."
            + (fetched.Truncated ? " Mağazadaki ilk 5000 sipariş alındı; daha uzun dönemler için dönemleri ayrı ayrı çekin." : "");
        return Results.Ok(new { message, payload });
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
        var configured = settings is not null && IsStoreUrl(settings.StoreUrl) && SmtpSettings.IsAddress(settings.ApiUser)
            && TryUnprotect(protection, settings.ProtectedPassword, out _);
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
