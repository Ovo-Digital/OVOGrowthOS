using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record AccountInvitationRequest(string Email, string Name, string Role, Guid? BrandId);
public sealed record ForgotPasswordRequest(string Email);
public sealed record CompleteAccountLinkRequest(string Token, string Password);

public static partial class WorkflowEndpoints
{
    private const string ResetAcknowledgement = "Hesabınız uygunsa ve e-posta hizmeti açıksa şifre belirleme bağlantısı gönderim sırasına alınır. Gelen kutusu ve istenmeyen postaları kontrol edin; ileti gelmezse yöneticinizle görüşün.";
    private static void MapAccountMail(WebApplication app)
    {
        var admin = app.MapGroup("/api/account-mail").RequireAuthorization("AdminOnly");
        MapMailSettings(admin);
        admin.MapGet("/status", async (SmtpSettingsProvider provider) => { var settings = await provider.GetAsync(); return Results.Ok(new { enabled = settings.Enabled, configured = settings.Configured, ready = settings.Ready }); });
        admin.MapGet("/deliveries", async (AppDbContext db) => Results.Ok(await (
            from mail in db.MailDeliveries.AsNoTracking()
            join link in db.AccountLinks on mail.AccountLinkId equals link.Id
            join account in db.UserAccounts on mail.UserId equals account.Id
            orderby mail.CreatedAt descending
            select new { mail.Id, mail.UserId, account.Name, link.Email, link.Purpose, mail.Status, mail.CreatedAt, mail.AttemptedAt, mail.FinishedAt, mail.ErrorCode, link.ExpiresAt, account.InvitationPending }
        ).Take(100).ToListAsync()));
        admin.MapPost("/invitations", CreateAccountInvitation).RequireRateLimiting("login");
        admin.MapPost("/invitations/{id:guid}/resend", ResendAccountInvitation).RequireRateLimiting("login");
        app.MapPost("/api/auth/forgot-password", RequestPasswordLink).AllowAnonymous().RequireRateLimiting("login");
        app.MapPost("/api/auth/complete-account", CompleteAccountLink).AllowAnonymous().RequireRateLimiting("login");
    }

    private static async Task<IResult> CreateAccountInvitation(AccountInvitationRequest r, AppDbContext db, ClaimsPrincipal actor, SmtpSettingsProvider provider, IDataProtectionProvider protection)
    {
        var settings = await provider.GetAsync();
        if (!settings.Ready) return MailUnavailable();
        var email = r.Email?.Trim().ToLowerInvariant();
        if (!SmtpSettings.IsAddress(email) || string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 160 || r.Role is not ("Admin" or "Partner" or "Analyst" or "BrandClient")
            || (r.Role == "BrandClient") != r.BrandId.HasValue)
            return Results.BadRequest(new { error = "Geçerli ad, e-posta ve rol seçin. Müşteri daveti için marka seçimi zorunludur; ekip davetinde marka seçilmez." });
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"UserAccounts\" IN SHARE ROW EXCLUSIVE MODE");
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        if (await db.UserAccounts.AnyAsync(x => x.Email == email)) return Results.Conflict(new { error = "Bu adresin zaten hesabı var. Bekleyen daveti gönderim listesinden yenileyin; mevcut hesap için şifre yenileme kullanılır." });
        if (r.BrandId is { } brand && !await db.Brands.AnyAsync(x => x.Id == brand)) return Results.NotFound();
        var account = new UserAccount { Email = email!, Name = r.Name.Trim(), Role = r.Role, InvitationPending = true, PasswordHash = JwtTokenService.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))) };
        db.UserAccounts.Add(account);
        if (r.BrandId is { } brandId) db.PortalAccesses.Add(new PortalAccess { UserId = account.Id, User = account, BrandId = brandId });
        QueueAccountLink(db, account, AccountLinkPurpose.Invitation, settings, protection);
        Audit(db, actor, "AccountInvited", "UserAccount", account.Id, null, new { account.Email, account.Role, r.BrandId });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { account.Id, message = "Davet sıraya alındı. Kişi bağlantıdan şifresini belirleyince giriş yapabilir." });
    }

    private static async Task<IResult> ResendAccountInvitation(Guid id, AppDbContext db, ClaimsPrincipal actor, SmtpSettingsProvider provider, IDataProtectionProvider protection)
    {
        var settings = await provider.GetAsync();
        if (!settings.Ready) return MailUnavailable();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"UserAccounts\" IN SHARE ROW EXCLUSIVE MODE");
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.InvitationPending);
        if (account is null) return Results.Conflict(new { error = "Yalnız etkin ve henüz kabul edilmemiş davet yenilenebilir." });
        if (!await CanIssueLink(db, id)) return Results.Conflict(new { error = "Bu hesaba kısa sürede bağlantı istendi. Biraz bekleyin; saatte en fazla üç bağlantı oluşturulabilir." });
        await InvalidateAccountLinks(db, id);
        QueueAccountLink(db, account, AccountLinkPurpose.Invitation, settings, protection);
        Audit(db, actor, "AccountInvitationRenewed", "UserAccount", id, null, new { account.Email });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { message = "Yeni davet sıraya alındı. Önceki bağlantılar artık kullanılamaz." });
    }

    private static async Task<IResult> RequestPasswordLink(ForgotPasswordRequest r, AppDbContext db, SmtpSettingsProvider provider, IDataProtectionProvider protection)
    {
        var settings = await provider.GetAsync();
        // Same public answer for unknown, closed, throttled and disabled-mail accounts. No SMTP call on this request.
        if (!settings.Ready || !SmtpSettings.IsAddress(r.Email?.Trim().ToLowerInvariant())) return Results.Ok(new { message = ResetAcknowledgement });
        var email = r.Email!.Trim().ToLowerInvariant();
        var id = await db.UserAccounts.AsNoTracking().Where(x => x.Email == email && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
        if (id is null) return Results.Ok(new { message = ResetAcknowledgement });
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"UserAccounts\" WHERE \"Id\" = {id.Value} FOR UPDATE");
        var account = await db.UserAccounts.SingleAsync(x => x.Id == id);
        if (account.IsActive && account.Email == email && await CanIssueLink(db, account.Id) &&
            (account.Role is "Admin" or "Partner" or "Analyst" || account.Role == "BrandClient" && await db.PortalAccesses.AnyAsync(x => x.UserId == account.Id)))
        {
            await InvalidateAccountLinks(db, account.Id);
            QueueAccountLink(db, account, account.InvitationPending ? AccountLinkPurpose.Invitation : AccountLinkPurpose.PasswordReset, settings, protection);
            await db.SaveChangesAsync();
        }
        if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { message = ResetAcknowledgement });
    }

    private static async Task<IResult> CompleteAccountLink(CompleteAccountLinkRequest r, AppDbContext db)
    {
        if (r.Token is not { Length: 64 } || string.IsNullOrWhiteSpace(r.Password) || r.Password.Length is < 10 or > 256)
            return Results.BadRequest(new { error = "Bağlantıyı kontrol edin; yeni şifre 10–256 karakter arasında olmalıdır." });
        var hash = AccountTokenHash(r.Token);
        var grant = await db.AccountLinks.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (grant is null) return InvalidAccountLink();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"UserAccounts\" WHERE \"Id\" = {grant.UserId} FOR UPDATE");
        var account = await db.UserAccounts.SingleAsync(x => x.Id == grant.UserId);
        var link = await db.AccountLinks.SingleAsync(x => x.Id == grant.Id);
        if (link.UsedAt is not null || link.ExpiresAt <= DateTimeOffset.UtcNow || !account.IsActive || account.Email != link.Email || account.TokenVersion != link.AccountVersion
            || account.InvitationPending != (link.Purpose == AccountLinkPurpose.Invitation)
            || account.Role == "BrandClient" && !await db.PortalAccesses.AnyAsync(x => x.UserId == account.Id)) return InvalidAccountLink();
        account.PasswordHash = JwtTokenService.HashPassword(r.Password); account.InvitationPending = false; account.TokenVersion++; account.UpdatedAt = DateTimeOffset.UtcNow;
        await InvalidateAccountLinks(db, account.Id);
        db.AuditRecords.Add(new AuditRecord { UserId = account.Email, Action = link.Purpose == AccountLinkPurpose.Invitation ? "AccountInvitationAccepted" : "AccountPasswordReset", EntityType = "UserAccount", EntityId = account.Id.ToString() });
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return InvalidAccountLink(); }
        if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { message = "Şifreniz kaydedildi. Önceki oturumlar kapatıldı; yeni şifrenizle giriş yapabilirsiniz." });
    }

    internal static string AccountTokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void QueueAccountLink(AppDbContext db, UserAccount account, AccountLinkPurpose purpose, SmtpSettings settings, IDataProtectionProvider protection)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var link = new AccountLink { UserId = account.Id, Email = account.Email, AccountVersion = account.TokenVersion, Purpose = purpose,
            TokenHash = AccountTokenHash(token), ExpiresAt = DateTimeOffset.UtcNow.Add(purpose == AccountLinkPurpose.Invitation ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(30)) };
        var url = settings.WebOrigin + "/account-access#token=" + token;
        var body = $"OVO Growth OS\n\n{(purpose == AccountLinkPurpose.Invitation ? "Hesabınıza davet edildiniz. Şifrenizi belirlemek için" : "Şifrenizi yenilemek için")} aşağıdaki bağlantıyı açın:\n{url}\n\nBağlantı tek kullanımlıktır; {(purpose == AccountLinkPurpose.Invitation ? "24 saat" : "30 dakika")} geçerlidir. Yeni bağlantı istenirse önceki bağlantı geçersiz olur. Bu bağlantıyı paylaşmayın. Bu işlemi beklemiyorsanız e-postayı yok sayın ve OVO yöneticinizle görüşün.\n\nBu ileti şifre, finansal rapor veya dosya eki içermez.";
        db.AccountLinks.Add(link);
        db.MailDeliveries.Add(new MailDelivery { UserId = account.Id, AccountLinkId = link.Id, ProtectedBody = protection.CreateProtector(AccountMailQueue.ProtectionPurpose).Protect(body) });
    }
    private static async Task InvalidateAccountLinks(AppDbContext db, Guid id)
    {
        foreach (var old in await db.AccountLinks.Where(x => x.UserId == id && x.UsedAt == null).ToListAsync()) { old.UsedAt = DateTimeOffset.UtcNow; old.Revision++; }
    }
    private static async Task<bool> CanIssueLink(AppDbContext db, Guid id)
    {
        var hour = DateTimeOffset.UtcNow.AddHours(-1); var minute = DateTimeOffset.UtcNow.AddMinutes(-1);
        return !await db.AccountLinks.AnyAsync(x => x.UserId == id && x.CreatedAt > minute)
            && await db.AccountLinks.CountAsync(x => x.UserId == id && x.CreatedAt > hour) < 3;
    }
    private static Task<bool> CurrentAdmin(AppDbContext db, ClaimsPrincipal actor)
    {
        var id = Guid.Parse(actor.FindFirstValue("uid")!); var version = int.Parse(actor.FindFirstValue("session_version")!);
        return db.UserAccounts.AnyAsync(x => x.Id == id && x.Role == "Admin" && x.IsActive && !x.InvitationPending && x.TokenVersion == version);
    }
    private static IResult InvalidAccountLink() => Results.BadRequest(new { error = "Bağlantı geçersiz, süresi dolmuş veya kullanılmış. Yeni bağlantı isteyin." });
    private static IResult MailUnavailable() => Results.Problem(statusCode: 503, title: "E-posta gönderimi kapalı veya ayarları eksik. Yöneticiniz SMTP bilgilerini tamamlamalıdır.");
}
