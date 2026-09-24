using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Mail;

public sealed class AccountMailQueue(AppDbContext db, IDataProtectionProvider protection, IAccountMailSender sender, SmtpSettings settings)
{
    internal const string ProtectionPurpose = "OVO.AccountMail.v1";

    public async Task<bool> ProcessOne(CancellationToken cancellationToken = default)
    {
        if (!settings.Ready) return false;
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        var stale = await db.MailDeliveries.Where(x => x.Status == MailDeliveryStatus.Sending && x.AttemptedAt < cutoff).Take(20).ToListAsync(cancellationToken);
        foreach (var item in stale) Finish(item, MailDeliveryStatus.Uncertain, "Interrupted");
        try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return true; }
        var mail = await db.MailDeliveries.Where(x => x.Status == MailDeliveryStatus.Pending).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (mail is null) return false;
        mail.Status = MailDeliveryStatus.Sending; mail.AttemptedAt = DateTimeOffset.UtcNow; mail.Revision++;
        try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return true; }

        // Claim is committed first. A crash after SMTP acceptance never automatically sends it again.
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (tx is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"UserAccounts\" WHERE \"Id\" = {mail.UserId} FOR UPDATE", cancellationToken);
        var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mail.UserId, cancellationToken);
        var link = await db.AccountLinks.AsNoTracking().SingleAsync(x => x.Id == mail.AccountLinkId, cancellationToken);
        var valid = account is not null && account.IsActive && account.Email == link.Email && account.TokenVersion == link.AccountVersion
            && link.UsedAt is null && link.ExpiresAt > DateTimeOffset.UtcNow
            && account.InvitationPending == (link.Purpose == AccountLinkPurpose.Invitation)
            && (account.Role is "Admin" or "Partner" or "Analyst" || account.Role == "BrandClient" && await db.PortalAccesses.AnyAsync(x => x.UserId == account.Id, cancellationToken));
        if (!valid) Finish(mail, MailDeliveryStatus.Cancelled, "AccountOrLinkChanged");
        else
        {
            try
            {
                var body = protection.CreateProtector(ProtectionPurpose).Unprotect(mail.ProtectedBody);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await sender.SendAsync(mail.Id, link.Email, link.Purpose == AccountLinkPurpose.Invitation ? "OVO Growth OS hesap davetiniz" : "OVO Growth OS şifre yenileme", body, timeout.Token);
                Finish(mail, MailDeliveryStatus.Sent, "");
            }
            catch (System.Security.Cryptography.CryptographicException) { Finish(mail, MailDeliveryStatus.Cancelled, "ProtectionKeyUnavailable"); }
            catch { Finish(mail, MailDeliveryStatus.Uncertain, "SmtpNotConfirmed"); } // Never persist SMTP exceptions: they may contain credentials or message content.
        }
        await db.SaveChangesAsync(CancellationToken.None);
        if (tx is not null) await tx.CommitAsync(CancellationToken.None);
        return true;
    }

    private static void Finish(MailDelivery mail, MailDeliveryStatus status, string error)
    {
        mail.Status = status; mail.ErrorCode = error; mail.FinishedAt = DateTimeOffset.UtcNow; mail.ProtectedBody = ""; mail.Revision++;
    }
}

public sealed class AccountMailWorker(IServiceScopeFactory scopes, ILogger<AccountMailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < 10 && !stoppingToken.IsCancellationRequested; i++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<AccountMailQueue>().ProcessOne(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("Hesap e-posta kuyruğu kontrol edilemedi; sonraki kontrolde yeniden değerlendirilecek."); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
