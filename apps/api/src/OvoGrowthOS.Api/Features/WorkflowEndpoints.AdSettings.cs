using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record AdSettingsRequest(int Revision, string Platform, string? AccountId, string? ClientId,
    string? Secret, string? ClientSecret, string? DeveloperToken);
public sealed record AdTestRequest(int Revision, string Platform);

public static partial class WorkflowEndpoints
{
    private const string AdApiPurpose = "OVO.BrandAdCredentials.v1";

    private static void MapAdSettings(WebApplication app)
    {
        var group = app.MapGroup("/api/brands/{id:guid}/ad-settings").RequireAuthorization("AdminOnly");
        group.MapGet("/", GetAdSettings).RequireRateLimiting("user-action");
        group.MapPut("/", SaveAdSettings).RequireRateLimiting("admin-action");
        group.MapPost("/test", TestAdSettings).RequireRateLimiting("admin-action");
        app.MapGet("/api/brands/{id:guid}/ad-spend", ReadAdSpend)
            .RequireAuthorization("OperationsWrite").RequireRateLimiting("user-action");
    }

    private static async Task<IResult> GetAdSettings(Guid id, string? platform, AppDbContext db, IDataProtectionProvider protection, CancellationToken ct)
    {
        if (!TryParseAdPlatform(platform, out var parsed)) return Results.BadRequest(new { error = "Platform yalnız Meta veya Google olabilir." });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        var row = await db.BrandAdSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id && x.Platform == parsed, ct);
        var secretStored = row is not null && TryUnprotectAd(protection, row.ProtectedSecret, out _);
        var clientSecretStored = row is not null && TryUnprotectAd(protection, row.ProtectedClientSecret, out _);
        var developerTokenStored = row is not null && TryUnprotectAd(protection, row.ProtectedDeveloperToken, out _);
        return Results.Ok(new
        {
            revision = row?.Revision ?? 0, platform = parsed.ToString(),
            accountId = row?.AccountId ?? "", clientId = row?.ClientId ?? "",
            secretStored, clientSecretStored, developerTokenStored,
            configured = row is not null && IsAdSettingsValid(row, protection),
            row?.UpdatedAt, row?.LastTestAt
        });
    }

    private static async Task<IResult> SaveAdSettings(Guid id, AdSettingsRequest r, AppDbContext db, IDataProtectionProvider protection, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!TryParseAdPlatform(r.Platform, out var platform)) return Results.BadRequest(new { error = "Platform yalnız Meta veya Google olabilir." });
        var accountId = r.AccountId?.Trim() ?? ""; var clientId = r.ClientId?.Trim() ?? "";
        var secret = r.Secret?.Trim() ?? ""; var clientSecret = r.ClientSecret?.Trim() ?? ""; var developerToken = r.DeveloperToken?.Trim() ?? "";
        if (secret.Length > AdSettings.MaxValueLength || clientSecret.Length > AdSettings.MaxValueLength || developerToken.Length > AdSettings.MaxValueLength)
            return Results.BadRequest(new { error = $"Gizli alanlar en fazla {AdSettings.MaxValueLength} karakter olabilir." });
        if (secret.Length > 0 && secret.Length < 8)
            return Results.BadRequest(new { error = "Erişim jetonu 8 ile 400 karakter arasında olmalı." });
        var validation = AdSettings.Validate(platform, accountId, clientId);
        if (validation is not null) return Results.BadRequest(new { error = validation });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        var row = await db.BrandAdSettings.SingleOrDefaultAsync(x => x.BrandId == id && x.Platform == platform, ct);
        if ((row?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "Reklam ayarları değişmiş. Sayfayı yenileyip güncel bilgileri kontrol edin." });
        var isNew = row is null;
        row ??= new BrandAdSettings { BrandId = id, Platform = platform, Revision = 0 };
        var protector = protection.CreateProtector(AdApiPurpose);
        var storedSecret = TryUnprotectAd(protection, row.ProtectedSecret, out var oldSecret) ? oldSecret : "";
        var storedClientSecret = TryUnprotectAd(protection, row.ProtectedClientSecret, out var oldClientSecret) ? oldClientSecret : "";
        var storedDeveloperToken = TryUnprotectAd(protection, row.ProtectedDeveloperToken, out var oldDeveloperToken) ? oldDeveloperToken : "";
        if (secret.Length == 0 && storedSecret.Length == 0)
            return Results.BadRequest(new { error = platform == AdPlatform.Meta
                ? "Meta erişim jetonu gerekli. Jetonu ilk kez girin; sonraki kayıtlarda boş bırakmak kayıtlı jetonu korur."
                : "Google yenileme jetonu gerekli. Jetonu ilk kez girin; sonraki kayıtlarda boş bırakmak kayıtlı jetonu korur." });
        if (platform == AdPlatform.Google && (clientSecret.Length == 0 && storedClientSecret.Length == 0
            || developerToken.Length == 0 && storedDeveloperToken.Length == 0))
            return Results.BadRequest(new { error = "Google için istemci sırrı ve geliştirici jetonu gereklidir; boş alanlar kayıtlı değerleri korur." });
        row.AccountId = accountId; row.ClientId = clientId;
        if (secret.Length > 0) row.ProtectedSecret = protector.Protect(secret);
        if (clientSecret.Length > 0) row.ProtectedClientSecret = protector.Protect(clientSecret);
        if (developerToken.Length > 0) row.ProtectedDeveloperToken = protector.Protect(developerToken);
        row.Revision++; row.UpdatedAt = DateTimeOffset.UtcNow;
        // Never audit request or entity contents: they contain recoverable credentials.
        db.AuditRecords.Add(new AuditRecord { UserId = actor.FindFirstValue(ClaimTypes.Email)!, Action = "BrandAdSettingsChanged", EntityType = "BrandAdSettings", EntityId = $"{id}:{platform}" });
        if (isNew) db.BrandAdSettings.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "Reklam ayarları başka bir işlemle değişmiş. Sayfayı yenileyip tekrar deneyin." }); }
        return Results.Ok(new { message = "Reklam ayarları kaydedildi. Kaydetmek bağlantı doğrulaması yapmaz.", row.Revision });
    }

    private static async Task<IResult> TestAdSettings(Guid id, AdTestRequest r, AppDbContext db, IDataProtectionProvider protection,
        IAdSpendClient adClient, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!TryParseAdPlatform(r.Platform, out var platform)) return Results.BadRequest(new { error = "Platform yalnız Meta veya Google olabilir." });
        var row = await db.BrandAdSettings.SingleOrDefaultAsync(x => x.BrandId == id && x.Platform == platform, ct);
        if (row is null || row.Revision != r.Revision) return Results.Conflict(new { error = "Önce ayarları kaydedin; değişiklik varsa sayfayı yenileyin." });
        if (row.LastTestAt > DateTimeOffset.UtcNow.AddMinutes(-1)) return Results.Conflict(new { error = "Son doğrulamadan sonra bir dakika bekleyin; reklam platformunda üst üste istek atmaktan kaçının." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        if (!TryBuildConnection(row, protection, out var connection))
            return Results.BadRequest(new { error = "Bağlantı bilgileri eksik veya jeton çözülemiyor. Bilgileri ve anahtar deposunu kontrol edin." });
        var recipient = actor.FindFirstValue(ClaimTypes.Email)!;
        row.LastTestAt = DateTimeOffset.UtcNow;
        db.AuditRecords.Add(new AuditRecord { UserId = recipient, Action = "BrandAdTestRequested", EntityType = "BrandAdSettings", EntityId = $"{id}:{platform}" });
        // Persist the attempt before the provider call; a second test inside a minute cannot start.
        await db.SaveChangesAsync(ct);
        var accepted = await adClient.TestAsync(connection, ct);
        db.AuditRecords.Add(new AuditRecord { UserId = recipient, Action = accepted ? "BrandAdTestAccepted" : "BrandAdTestUncertain", EntityType = "BrandAdSettings", EntityId = $"{id}:{platform}" });
        await db.SaveChangesAsync(CancellationToken.None);
        return Results.Ok(new
        {
            accepted,
            message = platform == AdPlatform.Meta
                ? accepted
                    ? "Meta reklam hesabı erişimi doğrulandı."
                    : "Bağlantı doğrulanamadı. Hesap numarasını ve erişim jetonunu kontrol edin; Meta yöneticisinde uygulamanın reklam okuma yetkisi bulunduğundan emin olun."
                : accepted
                    ? "Google Ads erişimi doğrulandı."
                    : "Bağlantı doğrulanamadı. Hesap numarasını, istemci kimliğini, istemci sırrını, yenileme jetonunu ve geliştirici jetonunu kontrol edin."
        });
    }

    private static async Task<IResult> ReadAdSpend(Guid id, string? platform, string? period, AppDbContext db, IDataProtectionProvider protection,
        IAdSpendClient adClient, CancellationToken ct)
    {
        if (!TryParseAdPlatform(platform, out var parsed)) return Results.BadRequest(new { error = "Platform yalnız Meta veya Google olabilir." });
        if (!StoreOrderPeriod.TryParse(period, out var parsedPeriod)) return Results.BadRequest(new { error = "Dönem 'YYYY-AA' biçiminde olmalı, örneğin 2026-08." });
        if (!await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound(new { error = "Marka bulunamadı." });
        var row = await db.BrandAdSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id && x.Platform == parsed, ct);
        if (row is null || !TryBuildConnection(row, protection, out var connection))
            return Results.BadRequest(new { error = $"{AdSettings.PlatformLabel(parsed)} bağlantı ayarları kayıtlı değil. Önce marka reklam ayarlarını girin." });
        var result = await adClient.FetchAsync(connection, parsedPeriod, ct);
        if (result is null)
            return Results.BadRequest(new { error = $"{AdSettings.PlatformLabel(parsed)} harcaması okunamadı. Bağlantı ayarlarını ve hesap numarasını kontrol edip tekrar deneyin." });
        return Results.Ok(new { platform = parsed.ToString(), period = parsedPeriod.Key, amount = result.Amount, currency = result.Currency, source = result.Source });
    }

    private static bool TryParseAdPlatform(string? value, out AdPlatform platform)
    {
        platform = AdPlatform.Meta;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse(value.Trim(), true, out platform);
    }

    internal static bool IsAdSettingsValid(BrandAdSettings row, IDataProtectionProvider protection)
    {
        if (AdSettings.Validate(row.Platform, row.AccountId, row.ClientId) is not null) return false;
        if (!TryUnprotectAd(protection, row.ProtectedSecret, out var secret) || secret.Length < 8) return false;
        if (row.Platform != AdPlatform.Google) return true;
        return TryUnprotectAd(protection, row.ProtectedClientSecret, out var clientSecret) && clientSecret.Length > 0
            && TryUnprotectAd(protection, row.ProtectedDeveloperToken, out var developerToken) && developerToken.Length > 0;
    }

    internal static bool TryBuildConnection(BrandAdSettings row, IDataProtectionProvider protection, out AdConnection connection)
    {
        connection = null!;
        if (AdSettings.Validate(row.Platform, row.AccountId, row.ClientId) is not null) return false;
        if (!TryUnprotectAd(protection, row.ProtectedSecret, out var secret) || secret.Length < 8) return false;
        var clientSecret = ""; var developerToken = "";
        if (row.Platform == AdPlatform.Google)
        {
            if (!TryUnprotectAd(protection, row.ProtectedClientSecret, out clientSecret) || clientSecret.Length == 0) return false;
            if (!TryUnprotectAd(protection, row.ProtectedDeveloperToken, out developerToken) || developerToken.Length == 0) return false;
        }
        connection = new AdConnection(row.Platform, row.AccountId, row.ClientId, secret, clientSecret, developerToken);
        return true;
    }

    private static bool TryUnprotectAd(IDataProtectionProvider protection, string protectedValue, out string value)
    {
        value = "";
        if (protectedValue.Length == 0) return false;
        try { value = protection.CreateProtector(AdApiPurpose).Unprotect(protectedValue); return value.Length > 0; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }
}
