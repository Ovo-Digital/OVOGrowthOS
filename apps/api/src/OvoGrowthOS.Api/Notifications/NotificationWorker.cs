using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

public sealed class NotificationMailQueue(AppDbContext db, NotificationService notifications, SmtpSettingsProvider provider, IAccountMailSender sender)
{
    public async Task<bool> ProcessOne(CancellationToken ct = default)
    {
        var settings = await provider.GetAsync(ct);
        if (!settings.Ready) return false;
        var staleBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var stale = await db.UserNotifications.Where(x => x.EmailStatus == MailDeliveryStatus.Sending && x.AttemptedAt < staleBefore).Take(20).ToListAsync(ct);
        foreach (var n in stale) { n.EmailStatus = MailDeliveryStatus.Uncertain; n.ErrorCode = "Interrupted"; n.Revision++; }
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return true; }
        var mail = await db.UserNotifications.Where(x => x.EmailStatus == MailDeliveryStatus.Pending).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (mail is null) return false;
        mail.EmailStatus = MailDeliveryStatus.Sending; mail.AttemptedAt = DateTimeOffset.UtcNow; mail.Revision++;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return true; }
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx is not null) await NotificationService.LockUser(db, mail.UserId, ct);
        var user = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == mail.UserId, ct);
        var pref = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == mail.UserId, ct);
        var content = await notifications.Resolve(mail, user, DateTimeOffset.UtcNow, true, ct);
        if (content is null || pref?.WantsEmail(mail.Kind) != true || user.Email != mail.Email || user.TokenVersion != mail.AccountVersion || mail.CreatedAt < DateTimeOffset.UtcNow.AddHours(-24))
        { mail.EmailStatus = MailDeliveryStatus.Cancelled; mail.ErrorCode = "NoLongerEligible"; }
        else
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await sender.SendAsync(mail.Id, user.Email, "OVO Growth OS bildiriminiz", $"{content.Title}\n\nAyrıntılar için hesabınızla giriş yapın: {settings.WebOrigin}{content.Href}\n\nE-posta tercihlerinizi panelde Bildirimler bölümünden değiştirebilirsiniz.", timeout.Token);
                mail.EmailStatus = MailDeliveryStatus.Sent;
            }
            catch { mail.EmailStatus = MailDeliveryStatus.Uncertain; mail.ErrorCode = "SmtpNotConfirmed"; }
        }
        mail.Revision++;
        await db.SaveChangesAsync(CancellationToken.None);
        if (tx is not null) await tx.CommitAsync(CancellationToken.None);
        return true;
    }
}

public sealed class NotificationWorker(IServiceScopeFactory scopes, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Guid[] ids;
                await using (var scope = scopes.CreateAsyncScope())
                    ids = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserAccounts.Where(x => x.IsActive && !x.InvitationPending).Select(x => x.Id).ToArrayAsync(stoppingToken);
                foreach (var id in ids)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationService>().Refresh(id, DateTimeOffset.UtcNow, stoppingToken);
                }
                for (var i = 0; i < 50; i++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<NotificationMailQueue>().ProcessOne(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("Bildirimler şu anda kontrol edilemedi; sonraki kontrolde yeniden değerlendirilecek."); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
