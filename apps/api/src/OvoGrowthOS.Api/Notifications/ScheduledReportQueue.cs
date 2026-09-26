using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

public sealed class ScheduledReportQueue(AppDbContext db, SmtpSettingsProvider provider)
{
    public async Task RunDue(DateTimeOffset now, CancellationToken ct = default)
    {
        var settings = await provider.GetAsync(ct);
        if (!settings.Ready) return;
        var policies = await db.BrandMailPolicies.AsNoTracking()
            .Where(x => x.ScheduledReportEnabled && x.ReportEmailEnabled).ToListAsync(ct);
        foreach (var policy in policies)
        {
            var window = ReportMailSchedule.CurrentWindow(now, policy.ScheduledSendDay, policy.ScheduledSendHour);
            if (window is null) continue;
            try { await Materialize(policy, window, now, ct); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        }
    }

    private async Task Materialize(BrandMailPolicy policy, ReportScheduleWindow window, DateTimeOffset now, CancellationToken ct)
    {
        var report = await db.PortalReports.AsNoTracking()
            .Where(x => x.BrandId == policy.BrandId && x.Year == window.TargetYear && x.Month == window.TargetMonth && x.RevokedAt == null)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (report is null) return;
        var status = await db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.Id == report.PerformanceId).Select(x => (MonthlyPerformanceStatus?)x.Status).SingleOrDefaultAsync(ct);
        if (status is null || !PortfolioReporting.Matches(status.Value, ReportScope.Closed)) return;
        var accounts = await db.PortalAccesses.AsNoTracking().Where(x => x.BrandId == policy.BrandId)
            .Select(x => x.User).OrderBy(x => x.Name).ToListAsync(ct);
        if (accounts.Count == 0) return;
        var ids = accounts.Select(x => x.Id).ToArray();
        var prefs = await db.NotificationPreferences.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId);
        var events = await db.UserNotifications.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.Kind == NotificationKind.PortalReport && x.SourceId == report.Id)
            .ToDictionaryAsync(x => x.UserId);
        foreach (var user in accounts)
        {
            prefs.TryGetValue(user.Id, out var pref);
            if (!user.IsActive || user.InvitationPending || user.Role != "BrandClient" || pref?.PortalReportsEmail != true) continue;
            if (events.TryGetValue(user.Id, out var notification))
            {
                if (notification.EmailStatus is not null) continue;
                db.Attach(notification);
                notification.EmailStatus = MailDeliveryStatus.Pending;
                notification.CreatedAt = now; notification.AttemptedAt = null; notification.ErrorCode = "";
                notification.Email = user.Email; notification.AccountVersion = user.TokenVersion; notification.Revision++;
            }
            else
            {
                db.Add(new UserNotification
                {
                    UserId = user.Id, Kind = NotificationKind.PortalReport, SourceId = report.Id, EventKey = $"report:{report.Id}",
                    CreatedAt = now, Email = user.Email, AccountVersion = user.TokenVersion, EmailStatus = MailDeliveryStatus.Pending
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
