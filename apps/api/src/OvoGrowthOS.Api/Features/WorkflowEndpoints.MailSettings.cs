using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record MailSettingsRequest(bool Enabled, string Host, int Port, bool Secure, string User, string FromAddress,
    string FromName, string? Password, bool RemovePassword, int Revision);
public sealed record MailTestRequest(int Revision, bool Confirm);

public static partial class WorkflowEndpoints
{
    private static void MapMailSettings(RouteGroupBuilder group)
    {
        group.MapGet("/settings", async (AppDbContext db, SmtpSettingsProvider provider, ClaimsPrincipal actor) =>
        {
            var row = await db.MailConfigurations.AsNoTracking().SingleOrDefaultAsync();
            var effective = await provider.GetAsync();
            MailboxAddress.TryParse(effective.From, out var from);
            return Results.Ok(new
            {
                revision = row?.Revision ?? 0, source = row is null ? "Environment" : "Panel", enabled = row?.Enabled ?? effective.Enabled,
                host = row?.Host ?? effective.Host, port = row?.Port ?? effective.Port, secure = row?.Secure ?? effective.Secure,
                user = row?.User ?? effective.User, fromAddress = row?.FromAddress ?? from?.Address ?? "", fromName = row?.FromName ?? from?.Name ?? "OVO Digital",
                passwordStored = row is not null && row.ProtectedPassword.Length > 0, effective.Configured, effective.Ready,
                forceDisabled = provider.ForceDisabled, testRecipient = actor.FindFirstValue(ClaimTypes.Email), row?.UpdatedAt, row?.LastTestAt
            });
        });
        group.MapPut("/settings", SaveMailSettings).RequireRateLimiting("login");
        group.MapPost("/settings/test", TestMailSettings).RequireRateLimiting("login");
    }

    private static async Task<IResult> SaveMailSettings(MailSettingsRequest r, AppDbContext db, SmtpSettingsProvider provider, ClaimsPrincipal actor)
    {
        var host = r.Host?.Trim().ToLowerInvariant(); var user = r.User?.Trim(); var address = r.FromAddress?.Trim(); var name = r.FromName?.Trim();
        var password = r.Password?.Replace(" ", ""); // Google's displayed app password may contain grouping spaces.
        if (host != "smtp.gmail.com" || !(r.Port == 465 && r.Secure || r.Port == 587 && !r.Secure)
            || user is null || address is null || name is null || name.Length > 160 || name.Any(char.IsControl)
            || user.Length > 0 && !SmtpSettings.IsAddress(user) || address.Length > 0 && !SmtpSettings.IsAddress(address)
            || password is { Length: > 0 } && (password.Length != 16 || password.Any(c => !char.IsAsciiLetter(c)))
            || r.RemovePassword && (!string.IsNullOrEmpty(password) || r.Enabled))
            return Results.BadRequest(new { error = "Gmail sunucusu smtp.gmail.com olmalı. 465 için SSL/TLS, 587 için STARTTLS seçin. Geçerli e-posta adresleri ve Google'ın 16 harfli uygulama şifresini kullanın; normal hesap şifrenizi girmeyin. Şifreyi kaldırırken gönderimi kapatın." });
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"MailConfigurations\" IN SHARE ROW EXCLUSIVE MODE");
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        var row = await db.MailConfigurations.SingleOrDefaultAsync();
        if ((row?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "E-posta ayarları değişmiş. Sayfayı yenileyip güncel bilgileri kontrol edin." });
        if (row is not null && !string.Equals(row.User, user, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(password) && !r.RemovePassword)
            return Results.BadRequest(new { error = "Gönderici hesabını değiştirdiğinizde yeni uygulama şifresini girin veya kayıtlı şifreyi kaldırın." });
        row ??= new MailConfiguration { Revision = 0 };
        row.Enabled = r.Enabled; row.Host = host; row.Port = r.Port; row.Secure = r.Secure;
        row.User = user; row.FromAddress = address; row.FromName = name;
        if (r.RemovePassword) row.ProtectedPassword = "";
        else if (!string.IsNullOrEmpty(password)) row.ProtectedPassword = provider.Protect(password);
        if (row.Enabled && !provider.FromSaved(row).Configured)
            return Results.BadRequest(new { error = "Gönderimi açmak için gönderici bilgileri, uygulama şifresi ve sunucudaki panel adresi eksiksiz olmalı. Şifre boşsa korunur; ilk panel kaydında sunucu şifresi otomatik taşınmaz." });
        if (row.Revision == 0) db.MailConfigurations.Add(row);
        row.Revision++; row.UpdatedAt = DateTimeOffset.UtcNow;
        // Never audit request/entity contents: they contain a recoverable credential.
        db.AuditRecords.Add(new AuditRecord { UserId = actor.FindFirstValue(ClaimTypes.Email)!, Action = "MailSettingsChanged", EntityType = "MailConfiguration", EntityId = "1" });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { message = "E-posta ayarları kaydedildi. Kaydetmek deneme e-postası göndermez.", row.Revision });
    }

    private static async Task<IResult> TestMailSettings(MailTestRequest r, AppDbContext db, SmtpSettingsProvider provider, ISmtpTestSender sender, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!r.Confirm) return Results.BadRequest(new { error = "Deneme iletisinin yalnız kendi hesap adresinize gönderilmesini onaylayın." });
        if (provider.ForceDisabled) return Results.Conflict(new { error = "Sunucuda bütün gönderimler durdurulmuş; deneme gönderimi de kapalı." });
        var row = await db.MailConfigurations.SingleOrDefaultAsync(ct);
        if (row is null || row.Revision != r.Revision) return Results.Conflict(new { error = "Önce ayarları kaydedin; değişiklik varsa sayfayı yenileyin." });
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        var settings = provider.FromSaved(row);
        var recipient = actor.FindFirstValue(ClaimTypes.Email);
        if (!settings.Configured || !SmtpSettings.IsAddress(recipient)) return Results.BadRequest(new { error = "E-posta ayarları eksik veya şifre çözülemiyor. Bilgileri ve anahtar deposunu kontrol edin." });
        if (row.LastTestAt > DateTimeOffset.UtcNow.AddMinutes(-1)) return Results.Conflict(new { error = "Son denemeden sonra bir dakika bekleyin; önce gelen kutusu ve istenmeyen postaları kontrol edin." });
        row.LastTestAt = DateTimeOffset.UtcNow; row.Revision++;
        db.AuditRecords.Add(new AuditRecord { UserId = recipient!, Action = "MailTestRequested", EntityType = "MailConfiguration", EntityId = "1" });
        // Persist the attempt before SMTP. Concurrent/stale requests cannot send a second test.
        await db.SaveChangesAsync(ct);
        var accepted = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await sender.SendAsync(settings, recipient!, timeout.Token); accepted = true;
        }
        catch { /* Do not expose SMTP replies, credentials or network errors. Never automatically retry. */ }
        db.AuditRecords.Add(new AuditRecord { UserId = recipient!, Action = accepted ? "MailTestAccepted" : "MailTestUncertain", EntityType = "MailConfiguration", EntityId = "1" });
        await db.SaveChangesAsync(CancellationToken.None);
        return Results.Ok(new { accepted, message = accepted ? "E-posta sunucusu deneme iletisini kabul etti. Gelen kutunuza ulaştığını kendiniz kontrol edin; bu teslim garantisi değildir." : "Gönderim doğrulanamadı. İleti ulaşmış olabilir; tekrar denemeden önce gelen kutusunu kontrol edin. SMTP bilgilerini ve uygulama şifresini gözden geçirin." });
    }
}
