using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record AdCampaignReadRequest(string Platform, string Period);

public static partial class WorkflowEndpoints
{
    private static void MapAdCampaigns(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/ad-campaigns", ReadStoredAdCampaigns)
            .RequireAuthorization("ReadAccess").RequireRateLimiting("user-action");
        app.MapPost("/api/brands/{id:guid}/ad-campaigns", ReadLiveAdCampaigns)
            .RequireAuthorization("OperationsWrite").RequireRateLimiting("user-action");
    }

    private static async Task<IResult> ReadStoredAdCampaigns(Guid id, string? period, AppDbContext db, CancellationToken ct)
    {
        if (!StoreOrderPeriod.TryParse(period, out var parsedPeriod))
            return Results.BadRequest(new { error = "Dönem 'YYYY-AA' biçiminde olmalı, örneğin 2026-09." });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        var rows = await db.AdCampaignSpends.AsNoTracking()
            .Where(x => x.BrandId == id && x.Year == parsedPeriod.Year && x.Month == parsedPeriod.Month)
            .ToListAsync(ct);
        return Results.Ok(new
        {
            period = parsedPeriod.Key,
            campaigns = AdCampaigns.Group(rows),
            totals = AdCampaigns.Totals(rows),
            readAt = rows.Count == 0 ? (DateTimeOffset?)null : rows.Max(x => x.ReadAt)
        });
    }

    private static async Task<IResult> ReadLiveAdCampaigns(Guid id, AdCampaignReadRequest r, AppDbContext db,
        IDataProtectionProvider protection, IAdSpendClient adClient, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!TryParseAdPlatform(r.Platform, out var platform))
            return Results.BadRequest(new { error = "Platform yalnız Meta veya Google olabilir." });
        if (!StoreOrderPeriod.TryParse(r.Period, out var parsedPeriod))
            return Results.BadRequest(new { error = "Dönem 'YYYY-AA' biçiminde olmalı, örneğin 2026-09." });
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (brand is null) return Results.NotFound(new { error = "Marka bulunamadı." });
        var settings = await db.BrandAdSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id && x.Platform == platform, ct);
        if (settings is null || !TryBuildConnection(settings, protection, out var connection))
            return Results.BadRequest(new { error = $"{AdSettings.PlatformLabel(platform)} bağlantı ayarları kayıtlı değil. Önce marka reklam ayarlarını girin." });
        var campaigns = await adClient.FetchCampaignsAsync(connection, parsedPeriod, ct);
        if (campaigns is null)
            return Results.BadRequest(new { error = $"{AdSettings.PlatformLabel(platform)} kampanya harcamaları okunamadı. Bağlantı ayarlarını ve hesap numarasını kontrol edip tekrar deneyin." });
        var now = DateTimeOffset.UtcNow;
        var existing = await db.AdCampaignSpends
            .Where(x => x.BrandId == id && x.Year == parsedPeriod.Year && x.Month == parsedPeriod.Month && x.Platform == platform)
            .ToListAsync(ct);
        if (existing.Count > 0) db.AdCampaignSpends.RemoveRange(existing);
        foreach (var row in campaigns)
            db.AdCampaignSpends.Add(new AdCampaignSpend
            {
                BrandId = id, Year = parsedPeriod.Year, Month = parsedPeriod.Month, Platform = platform,
                CampaignName = row.CampaignName, Spend = row.Spend, Currency = row.Currency, ReadAt = now
            });
        var total = campaigns.Sum(x => x.Spend);
        var currency = campaigns.Select(x => x.Currency).FirstOrDefault(x => x.Length == 3) ?? "";
        db.AuditRecords.Add(new AuditRecord
        {
            UserId = actor.FindFirstValue(ClaimTypes.Email)!, Action = "AdCampaignsRead", EntityType = "AdCampaignSpend",
            EntityId = $"{id}:{platform}:{parsedPeriod.Key}",
            Reason = $"{brand.Name} ({AdSettings.PlatformLabel(platform)}) {parsedPeriod.Key} kampanya harcaması okundu: {campaigns.Count} kampanya, toplam {total.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))} {currency}".TrimEnd()
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Results.Conflict(new { error = "Kampanya kayıtları başka bir işlemle değişmiş. Sayfayı yenileyip tekrar deneyin." }); }
        var rows = await db.AdCampaignSpends.AsNoTracking()
            .Where(x => x.BrandId == id && x.Year == parsedPeriod.Year && x.Month == parsedPeriod.Month)
            .ToListAsync(ct);
        return Results.Ok(new
        {
            period = parsedPeriod.Key,
            campaigns = AdCampaigns.Group(rows),
            totals = AdCampaigns.Totals(rows),
            readAt = rows.Count == 0 ? (DateTimeOffset?)null : rows.Max(x => x.ReadAt),
            message = $"{AdSettings.PlatformLabel(platform)} kampanya kırılımı okundu ve kaydedildi."
        });
    }
}
