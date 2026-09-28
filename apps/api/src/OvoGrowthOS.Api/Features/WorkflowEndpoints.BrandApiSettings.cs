using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Integration;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record BrandApiSettingsRequest(int Revision, string StoreUrl, string ApiUser, string? Password, string? Platform = null);
public sealed record BrandApiTestRequest(int Revision);

public static partial class WorkflowEndpoints
{
    private const string BrandApiPurpose = "OVO.BrandApiCredentials.v1";

    private static void MapBrandApiSettings(WebApplication app)
    {
        var group = app.MapGroup("/api/brands/{id:guid}/api-settings").RequireAuthorization("AdminOnly");
        group.MapGet("/", GetBrandApiSettings);
        group.MapPut("/", SaveBrandApiSettings).RequireRateLimiting("admin-action");
        group.MapPost("/test", TestBrandApiSettings).RequireRateLimiting("admin-action");
    }

    private static async Task<IResult> GetBrandApiSettings(Guid id, AppDbContext db, IDataProtectionProvider protection, CancellationToken ct)
    {
        var brand = await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!brand) return Results.NotFound(new { error = "Marka bulunamadı." });
        var row = await db.BrandApiSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id, ct);
        var passwordStored = row is not null && TryUnprotect(protection, row.ProtectedPassword, out _);
        return Results.Ok(new
        {
            revision = row?.Revision ?? 0, platform = (row?.Platform ?? StorePlatform.GrandNode).ToString(),
            storeUrl = row?.StoreUrl ?? "", apiUser = row?.ApiUser ?? "",
            passwordStored, configured = row is not null && IsApiSettingsValid(row, protection),
            row?.UpdatedAt, row?.LastTestAt
        });
    }

    private static async Task<IResult> SaveBrandApiSettings(Guid id, BrandApiSettingsRequest r, AppDbContext db, IDataProtectionProvider protection, ClaimsPrincipal actor, CancellationToken ct)
    {
        var storeUrl = r.StoreUrl?.Trim(); var apiUser = r.ApiUser?.Trim() ?? ""; var password = string.IsNullOrEmpty(r.Password) ? "" : r.Password.Trim();
        if (!TryParseStorePlatform(r.Platform, out var platform))
            return Results.BadRequest(new { error = "Platform yalnız GrandNode veya Shopify olabilir." });
        if (password.Length > 400 || (password.Length > 0 && password.Length < 8))
            return Results.BadRequest(new { error = "Şifre veya jeton 8 ile 400 karakter arasında olmalı." });
        if (platform == StorePlatform.Shopify)
        {
            if (!IsShopifyUrl(storeUrl))
                return Results.BadRequest(new { error = "Shopify için mağaza adresi https ile başlayan ve .myshopify.com ile biten bir adres olmalı." });
        }
        else if (!IsStoreUrl(storeUrl) || !SmtpSettings.IsAddress(apiUser))
            return Results.BadRequest(new { error = "Mağaza adresi https ile başlayan geçerli bir adres olmalı. API kullanıcısı geçerli bir e-posta olmalı." });
        var brand = await db.Brands.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!brand) return Results.NotFound(new { error = "Marka bulunamadı." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        var row = await db.BrandApiSettings.SingleOrDefaultAsync(x => x.BrandId == id, ct);
        if ((row?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "API ayarları değişmiş. Sayfayı yenileyip güncel bilgileri kontrol edin." });
        var isNew = row is null;
        row ??= new BrandApiSettings { BrandId = id, Revision = 0 };
        if (password.Length == 0 && row.ProtectedPassword.Length == 0)
            return Results.BadRequest(new { error = platform == StorePlatform.Shopify
                ? "Admin API jetonu gerekli. Jetonu ilk kez girin; sonraki kayıtlarda boş bırakmak kayıtlı jetonu korur."
                : "API kullanıcısı şifresi gerekli. Şifreyi ilk kez girin; sonraki kayıtlarda boş bırakmak kayıtlı şifreyi korur." });
        row.Platform = platform; row.StoreUrl = storeUrl!; row.ApiUser = apiUser;
        if (password.Length > 0) row.ProtectedPassword = protection.CreateProtector(BrandApiPurpose).Protect(password);
        row.Revision++; row.UpdatedAt = DateTimeOffset.UtcNow;
        // Never audit request or entity contents: they contain a recoverable credential.
        db.AuditRecords.Add(new AuditRecord { UserId = actor.FindFirstValue(ClaimTypes.Email)!, Action = "BrandApiSettingsChanged", EntityType = "BrandApiSettings", EntityId = id.ToString() });
        if (isNew) db.BrandApiSettings.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "API ayarları başka bir işlemle değişmiş. Sayfayı yenileyip tekrar deneyin." }); }
        return Results.Ok(new { message = "API ayarları kaydedildi. Kaydetmek mağaza bağlantısını doğrulamaz.", row.Revision });
    }

    private static async Task<IResult> TestBrandApiSettings(Guid id, BrandApiTestRequest r, AppDbContext db, IDataProtectionProvider protection,
        IStoreTokenClient storeToken, IStoreOrderClient orders, ClaimsPrincipal actor, CancellationToken ct)
    {
        var row = await db.BrandApiSettings.SingleOrDefaultAsync(x => x.BrandId == id, ct);
        if (row is null || row.Revision != r.Revision) return Results.Conflict(new { error = "Önce ayarları kaydedin; değişiklik varsa sayfayı yenileyin." });
        if (row.LastTestAt > DateTimeOffset.UtcNow.AddMinutes(-1)) return Results.Conflict(new { error = "Son doğrulamadan sonra bir dakika bekleyin; mağaza tarafında üst üste istek atmaktan kaçının." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        if (!TryUnprotect(protection, row.ProtectedPassword, out var password) || !IsApiSettingsValid(row, protection, password))
            return Results.BadRequest(new { error = "Bağlantı bilgileri eksik veya jeton çözülemiyor. Bilgileri ve anahtar deposunu kontrol edin." });
        var recipient = actor.FindFirstValue(ClaimTypes.Email)!;
        row.LastTestAt = DateTimeOffset.UtcNow;
        db.AuditRecords.Add(new AuditRecord { UserId = recipient, Action = "BrandApiTestRequested", EntityType = "BrandApiSettings", EntityId = id.ToString() });
        // Persist the attempt before the store call; a second test inside a minute cannot start.
        await db.SaveChangesAsync(ct);
        bool accepted;
        if (row.Platform == StorePlatform.Shopify)
        {
            accepted = await orders.TestAsync(StorePlatform.Shopify, password, row.StoreUrl, ct);
        }
        else
        {
            var passwordBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
            var token = await storeToken.CreateTokenAsync(row.StoreUrl, row.ApiUser, passwordBase64, ct);
            accepted = token.Accepted;
        }
        db.AuditRecords.Add(new AuditRecord { UserId = recipient, Action = accepted ? "BrandApiTestAccepted" : "BrandApiTestUncertain", EntityType = "BrandApiSettings", EntityId = id.ToString() });
        await db.SaveChangesAsync(CancellationToken.None);
        return Results.Ok(new
        {
            accepted,
            message = row.Platform == StorePlatform.Shopify
                ? accepted
                    ? "Shopify mağaza erişimi doğrulandı; sipariş aktarımı için bağlantı hazır."
                    : "Bağlantı doğrulanamadı. Mağaza adresini ve Admin API jetonunu kontrol edin; Shopify yöneticisinde uygulamanın sipariş okuma yetkisi bulunduğundan emin olun."
                : accepted
                    ? "Mağaza erişimi doğrulandı; API kullanıcısıyla jeton alındı. Sipariş aktarımı için bağlantı hazır."
                    : "Bağlantı doğrulanamadı. Mağaza adresini, API kullanıcısı e-postasını ve şifresini kontrol edin; GrandNode panelinde bu e-posta için API kullanıcısının etkin olduğundan emin olun."
        });
    }

    private static bool TryParseStorePlatform(string? value, out StorePlatform platform)
    {
        platform = StorePlatform.GrandNode;
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Enum.TryParse(value.Trim(), true, out platform);
    }

    private static bool IsStoreUrl(string? storeUrl) =>
        Uri.TryCreate(storeUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host.Length > 0 && uri.UserInfo.Length == 0 && storeUrl!.Length <= 300;

    private static bool IsShopifyUrl(string? storeUrl) =>
        IsStoreUrl(storeUrl) && Uri.TryCreate(storeUrl, UriKind.Absolute, out var uri) && uri.Host.EndsWith(".myshopify.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsApiSettingsValid(BrandApiSettings row, IDataProtectionProvider protection, string password) =>
        row.Platform == StorePlatform.Shopify
            ? IsShopifyUrl(row.StoreUrl) && password.Length >= 8
            : IsStoreUrl(row.StoreUrl) && SmtpSettings.IsAddress(row.ApiUser) && password.Length > 0;

    private static bool IsApiSettingsValid(BrandApiSettings row, IDataProtectionProvider protection) =>
        TryUnprotect(protection, row.ProtectedPassword, out var password) && IsApiSettingsValid(row, protection, password);

    private static bool TryUnprotect(IDataProtectionProvider protection, string protectedPassword, out string password)
    {
        password = "";
        if (protectedPassword.Length == 0) return false;
        try { password = protection.CreateProtector(BrandApiPurpose).Unprotect(protectedPassword); return password.Length > 0; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }
}
