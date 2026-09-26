using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record BrandMailPolicyRequest(bool ReportEmailEnabled, string SubjectTemplate, string BodyTemplate,
    string Reason, int Revision, bool ScheduledReportEnabled = false, int ScheduledSendDay = 5, int ScheduledSendHour = 9);

public static partial class WorkflowEndpoints
{
    private static void MapBrandMailPolicy(RouteGroupBuilder management)
    {
        var group = management.MapGroup("/email-policy").RequireAuthorization("AdminOnly");
        group.MapGet("/", async (Guid brandId, AppDbContext db, SmtpSettingsProvider provider, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
            var row = await db.BrandMailPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId);
            var policy = row ?? new BrandMailPolicy { BrandId = brandId, Revision = 0 };
            var settings = await provider.GetAsync();
            var schedule = await ReadSchedule(db, brandId, policy, DateTimeOffset.UtcNow);
            return Results.Ok(new
            {
                policy.ReportEmailEnabled, policy.SubjectTemplate, policy.BodyTemplate, policy.Revision,
                updatedAt = row?.UpdatedAt, emailReady = settings.Ready,
                policy.ScheduledReportEnabled, policy.ScheduledSendDay, policy.ScheduledSendHour, schedule
            });
        });
        group.MapPut("/", async (Guid brandId, BrandMailPolicyRequest r, AppDbContext db, ClaimsPrincipal actor) =>
        {
            if (!ReportMailTemplate.Valid(r.SubjectTemplate, r.BodyTemplate) || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 500)
                return Results.BadRequest(new { error = "Konu en fazla 180, mesaj 2000, gerekçe 500 karakter olmalı. Yalnız {marka}, {donem} ve mesajda {baglanti} kullanılabilir. Mesajda {baglanti} zorunludur; HTML, dış bağlantı ve konu satırında satır sonu kullanmayın." });
            if (r.ScheduledSendDay is < 1 or > 31 || r.ScheduledSendHour is < 0 or > 23)
                return Results.BadRequest(new { error = "Zamanlanmış gönderim için ayın günü 1 ile 31, saat 0 ile 23 arasında olmalı." });
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            // The existing brand row also serializes the first policy insert and in-flight report mail.
            if (tx is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"Brands\" WHERE \"Id\" = {brandId} FOR UPDATE");
            if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
            if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
            var row = await db.BrandMailPolicies.SingleOrDefaultAsync(x => x.BrandId == brandId);
            if ((row?.Revision ?? 0) != r.Revision) return Results.Conflict(new { error = "Markanın e-posta kuralı değişmiş. Sayfayı yenileyip güncel bilgileri kontrol edin." });
            var before = new { reportEmailEnabled = row?.ReportEmailEnabled ?? false, scheduledReportEnabled = row?.ScheduledReportEnabled ?? false };
            if (row is null) { row = new BrandMailPolicy { BrandId = brandId, Revision = 0 }; db.Add(row); }
            row.ReportEmailEnabled = r.ReportEmailEnabled; row.SubjectTemplate = r.SubjectTemplate.Trim(); row.BodyTemplate = r.BodyTemplate.Trim();
            row.ScheduledReportEnabled = r.ScheduledReportEnabled; row.ScheduledSendDay = r.ScheduledSendDay; row.ScheduledSendHour = r.ScheduledSendHour;
            row.Revision++; row.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, actor, "BrandMailPolicyChanged", "Brand", brandId, before,
                new { row.ReportEmailEnabled, row.ScheduledReportEnabled, row.ScheduledSendDay, row.ScheduledSendHour, row.Revision, reason = r.Reason.Trim() });
            await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
            return Results.Ok(new
            {
                message = r.ScheduledReportEnabled && r.ReportEmailEnabled
                    ? $"Markanın rapor e-posta kuralı kaydedildi. Zamanlanmış gönderim her ayın {r.ScheduledSendDay}. günü saat {r.ScheduledSendHour:00}:00'da çalışır; yalnız bir önceki ayın paylaşılmış raporunu kapsar."
                    : "Markanın rapor e-posta kuralı kaydedildi. Zamanlanmış gönderim kapalı; önceden e-posta oluşturulmamış bildirimler geriye dönük gönderilmez."
            });
        });
        group.MapGet("/preview/{reportId:guid}", PreviewBrandReportMail);
    }

    private static async Task<object> ReadSchedule(AppDbContext db, Guid brandId, BrandMailPolicy policy, DateTimeOffset now)
    {
        var current = ReportMailSchedule.CurrentWindow(now, policy.ScheduledSendDay, policy.ScheduledSendHour);
        var occurrence = current?.OccurrenceAt ?? ReportMailSchedule.NextOccurrence(now, policy.ScheduledSendDay, policy.ScheduledSendHour);
        var (targetYear, targetMonth) = current is null ? ReportMailSchedule.TargetOf(occurrence) : (current.TargetYear, current.TargetMonth);
        var report = await db.PortalReports.AsNoTracking()
            .Where(x => x.BrandId == brandId && x.Year == targetYear && x.Month == targetMonth && x.RevokedAt == null)
            .OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        return new
        {
            enabled = policy.ScheduledReportEnabled && policy.ReportEmailEnabled,
            day = policy.ScheduledSendDay, hour = policy.ScheduledSendHour,
            nextOccurrenceAt = occurrence, targetYear, targetMonth,
            targetReportId = report, targetReportPublished = report.HasValue,
            sendingNow = current is not null
        };
    }

    private static async Task<IResult> PreviewBrandReportMail(Guid brandId, Guid reportId, AppDbContext db, SmtpSettingsProvider provider, HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        var report = await db.PortalReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reportId && x.BrandId == brandId);
        if (report is null) return Results.NotFound();
        var brandName = await db.Brands.Where(x => x.Id == brandId).Select(x => x.Name).SingleAsync();
        var policy = await db.BrandMailPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId) ?? new BrandMailPolicy();
        var settings = await provider.GetAsync(); var now = DateTimeOffset.UtcNow;
        var owns = ReportMailSchedule.OwnsEmail(policy.ScheduledReportEnabled && policy.ReportEmailEnabled,
            policy.ScheduledSendDay, policy.ScheduledSendHour, now, report.Year, report.Month);
        var scheduleAt = ReportMailSchedule.OccurrenceFor(report.Year, report.Month, policy.ScheduledSendDay, policy.ScheduledSendHour);
        var accounts = await db.PortalAccesses.AsNoTracking().Where(x => x.BrandId == brandId).Select(x => x.User).OrderBy(x => x.Name).ToListAsync();
        var ids = accounts.Select(x => x.Id).ToArray();
        var prefs = await db.NotificationPreferences.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId);
        var events = await db.UserNotifications.AsNoTracking().Where(x => ids.Contains(x.UserId) && x.Kind == NotificationKind.PortalReport && x.SourceId == reportId).ToDictionaryAsync(x => x.UserId);
        var recipients = accounts.Select(user =>
        {
            prefs.TryGetValue(user.Id, out var pref); events.TryGetValue(user.Id, out var notification);
            var reasons = new List<string>(); var deferred = false;
            if (!settings.Ready) reasons.Add("Genel e-posta hizmeti kapalı veya hazır değil.");
            if (!policy.ReportEmailEnabled) reasons.Add("Markanın rapor e-posta izni kapalı.");
            if (!user.IsActive) reasons.Add("Hesap kapalı.");
            if (user.InvitationPending) reasons.Add("Hesap daveti henüz tamamlanmadı.");
            if (user.Role != "BrandClient") reasons.Add("Müşteri hesabı yetkisi yok.");
            if (pref?.PortalReportsEmail != true) reasons.Add("Kişinin rapor e-posta tercihi kapalı.");
            if (report.RevokedAt is not null) reasons.Add("Rapor paylaşımı geri çekilmiş.");
            if (notification is null || notification.EmailStatus is null)
            {
                if (owns) deferred = true;
                else if (notification is null)
                {
                    if (report.PublishedAt < now.AddHours(-24)) reasons.Add("Yeni rapor bildiriminin 24 saatlik süresi geçti.");
                    if (pref is null || report.PublishedAt < pref.StartedAt) reasons.Add("Rapor, kişinin bildirim takibi başlamadan önce paylaşılmış.");
                }
                else reasons.Add("Bildirim oluşurken e-posta istenmedi; sonradan izin açmak onu göndermez.");
            }
            else if (notification.EmailStatus != MailDeliveryStatus.Pending)
                reasons.Add(notification.EmailStatus switch
                {
                    MailDeliveryStatus.Sent => "Bu bildirim daha önce e-posta sunucusuna iletildi; tekrar gönderilmez.",
                    MailDeliveryStatus.Sending => "Gönderim işleniyor; ikinci gönderim yapılmaz.",
                    MailDeliveryStatus.Uncertain => "Önceki gönderimin sonucu belirsiz; otomatik tekrar yapılmaz.",
                    MailDeliveryStatus.Cancelled => "Önceki gönderim iptal edildi; otomatik tekrar yapılmaz.",
                    _ => "Bildirim oluşurken e-posta istenmedi; sonradan izin açmak onu göndermez."
                });
            else
            {
                if (notification.Email != user.Email || notification.AccountVersion != user.TokenVersion) reasons.Add("Hesap bilgileri değişmiş; bekleyen eski ileti iptal edilir.");
                if (notification.CreatedAt < now.AddHours(-24)) reasons.Add("Bekleyen bildirimin gönderim süresi geçti.");
            }
            return new { user.Id, user.Name, user.Email, eligible = reasons.Count == 0, deferred, scheduledFor = deferred ? scheduleAt : (DateTimeOffset?)null, reasons };
        }).ToArray();
        var period = $"{report.Month:00}/{report.Year}"; var link = settings.WebOrigin + "/portal";
        return Results.Ok(new
        {
            report.Id, report.Year, report.Month, report.Version, recipients,
            subject = ReportMailTemplate.Render(policy.SubjectTemplate, brandName, period, link),
            body = ReportMailTemplate.Render(policy.BodyTemplate, brandName, period, link),
            deferredUntil = owns ? scheduleAt : (DateTimeOffset?)null,
            note = owns
                ? $"Bu dönem zamanlanmış gönderime dahildir; e-posta {scheduleAt:dd.MM.yyyy HH:mm}'da gönderilir. Bu bir ön izlemedir; e-posta göndermez. Yalnız bu markaya bağlı hesaplar listelenir. Gönderim anında güncel izinler yeniden kontrol edilir."
                : "Bu bir ön izlemedir; e-posta göndermez. Yalnız bu markaya bağlı hesaplar listelenir. Gönderim anında güncel izinler yeniden kontrol edilir. Aylık otomatik gönderim bu dönem için geçerli değil."
        });
    }
}
