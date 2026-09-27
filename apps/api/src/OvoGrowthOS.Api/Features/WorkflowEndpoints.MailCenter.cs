using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record MailCenterResendRequest(string Reason);

public static partial class WorkflowEndpoints
{
    private const int MailCenterLimit = 500;

    private sealed record MailCenterItem(Guid Id, string Source, string Type, string Recipient, string Name, Guid UserId,
        string Status, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? AttemptedAt, DateTimeOffset? FinishedAt, string? Resend);

    private static void MapMailCenter(WebApplication app)
    {
        var group = app.MapGroup("/api/mail-center").RequireAuthorization("AdminOnly");
        group.MapGet("/messages", ListMailCenterMessages);
        group.MapPost("/notifications/{id:guid}/resend", ResendNotificationMail);
    }

    private static async Task<IResult> ListMailCenterMessages(DateTimeOffset? from, DateTimeOffset? to, string? type, string? status, string? q,
        int? page, int? pageSize, AppDbContext db, HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        type = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToLowerInvariant();
        var match = string.IsNullOrWhiteSpace(q) ? null : q.Trim().ToLowerInvariant();
        var statusKnown = status is null or "Pending" or "Sending" or "Sent" or "Uncertain" or "Cancelled";
        var mailStatus = status switch
        {
            "Pending" => MailDeliveryStatus.Pending,
            "Sending" => MailDeliveryStatus.Sending,
            "Sent" => MailDeliveryStatus.Sent,
            "Uncertain" => MailDeliveryStatus.Uncertain,
            "Cancelled" => MailDeliveryStatus.Cancelled,
            _ => MailDeliveryStatus.Cancelled
        };
        var testAction = status switch { null => null, "Pending" => "MailTestRequested", "Sent" => "MailTestAccepted", "Uncertain" => "MailTestUncertain", _ => "" };

        var items = new List<MailCenterItem>();
        if (type is null or "invitation" or "password")
        {
            var rows = await db.MailDeliveries.AsNoTracking()
                .Join(db.AccountLinks.AsNoTracking(), row => row.AccountLinkId, link => link.Id, (row, link) => new { row, link })
                .Join(db.UserAccounts.AsNoTracking(), x => x.row.UserId, account => account.Id, (x, account) => new { x.row, x.link, account })
                .Where(x => (!from.HasValue || x.row.CreatedAt >= from.Value) && (!to.HasValue || x.row.CreatedAt <= to.Value)
                    && (type == null || (type == "invitation") == (x.link.Purpose == AccountLinkPurpose.Invitation))
                    && (status == null || (statusKnown && x.row.Status == mailStatus))
                    && (match == null || x.link.Email.ToLower().Contains(match)))
                .OrderByDescending(x => x.row.CreatedAt)
                .Take(MailCenterLimit).ToListAsync();
            foreach (var x in rows)
            {
                var kind = x.link.Purpose == AccountLinkPurpose.Invitation ? "invitation" : "password";
                items.Add(new MailCenterItem(x.row.Id, "account", kind, x.link.Email, x.account.Name, x.account.Id,
                    MailStatusKey(x.row.Status), MailReason(x.row.Status, x.row.ErrorCode), x.row.CreatedAt, x.row.AttemptedAt, x.row.FinishedAt,
                    kind == "invitation" && x.account.IsActive && x.account.InvitationPending && x.row.Status != MailDeliveryStatus.Sending ? "invitation" : null));
            }
        }
        if (type is null or "report" or "daily" or "task" or "conversation")
        {
            var rows = await db.UserNotifications.AsNoTracking()
                .Join(db.UserAccounts.AsNoTracking(), row => row.UserId, account => account.Id, (row, account) => new { row, account })
                .Where(x => x.row.EmailStatus != null
                    && (!from.HasValue || x.row.CreatedAt >= from.Value) && (!to.HasValue || x.row.CreatedAt <= to.Value)
                    && (type == null
                        || type == "report" && x.row.Kind == NotificationKind.PortalReport
                        || type == "daily" && x.row.Kind == NotificationKind.DailyTasks
                        || type == "task" && x.row.Kind == NotificationKind.TaskDue
                        || type == "conversation" && x.row.Kind != NotificationKind.PortalReport && x.row.Kind != NotificationKind.DailyTasks && x.row.Kind != NotificationKind.TaskDue)
                    && (status == null || (statusKnown && x.row.EmailStatus == mailStatus))
                    && (match == null || x.row.Email.ToLower().Contains(match)))
                .OrderByDescending(x => x.row.CreatedAt)
                .Take(MailCenterLimit).ToListAsync();
            foreach (var x in rows)
            {
                var kind = NotificationType(x.row.Kind);
                var blocked = x.row.Email != x.account.Email || x.row.AccountVersion != x.account.TokenVersion || !x.account.IsActive || x.account.InvitationPending;
                items.Add(new MailCenterItem(x.row.Id, "notification", kind, x.row.Email, x.account.Name, x.row.UserId,
                    MailStatusKey(x.row.EmailStatus!.Value), MailReason(x.row.EmailStatus!.Value, x.row.ErrorCode), x.row.CreatedAt, x.row.AttemptedAt, null,
                    !blocked && x.row.EmailStatus is MailDeliveryStatus.Cancelled or MailDeliveryStatus.Uncertain ? "notification" : null));
            }
        }
        if (type is null or "test")
        {
            var tests = await db.AuditRecords.AsNoTracking()
                .Where(x => (x.Action == "MailTestRequested" || x.Action == "MailTestAccepted" || x.Action == "MailTestUncertain")
                    && (!from.HasValue || x.CreatedAt >= from.Value) && (!to.HasValue || x.CreatedAt <= to.Value)
                    && (testAction == null || x.Action == testAction)
                    && (match == null || x.UserId.ToLower().Contains(match)))
                .OrderByDescending(x => x.CreatedAt).Take(MailCenterLimit).ToListAsync();
            foreach (var row in tests)
            {
                var key = row.Action switch { "MailTestAccepted" => "Sent", "MailTestUncertain" => "Uncertain", _ => "Pending" };
                items.Add(new MailCenterItem(row.Id, "test", "test", row.UserId, "Deneme e-postası", Guid.Empty,
                    key, MailReason(key), row.CreatedAt, null, null, null));
            }
        }

        var ordered = items.OrderByDescending(x => x.CreatedAt).ToList();
        var summary = ordered.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var current = Math.Max(page ?? 1, 1);
        return Results.Ok(new
        {
            items = ordered.Skip((current - 1) * size).Take(size), total = ordered.Count, page = current, pageSize = size, summary,
            note = $"“E-posta sunucusu kabul etti” teslim edildiği veya okunduğu anlamına gelmez; dış sağlayıcıdan kanıt gelmeden teslim veya açılma oranı gösterilmez. Liste en fazla {MailCenterLimit} kayıtla sınırlıdır; saklama süresi gerçek kullanım verisiyle belirlenir ve ücretsiz Gmail sınırsız toplu gönderici kabul edilmez."
        });
    }

    private static async Task<IResult> ResendNotificationMail(Guid id, MailCenterResendRequest r, AppDbContext db, ClaimsPrincipal actor)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 500)
            return Results.BadRequest(new { error = "Yeniden gönderim için kısa bir gerekçe yazın (en fazla 500 karakter)." });
        var owner = await db.UserNotifications.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync();
        if (owner is null) return Results.NotFound();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await NotificationService.LockUser(db, owner.Value);
        if (!await CurrentAdmin(db, actor)) return Results.Unauthorized();
        var mail = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id);
        if (mail is null) return Results.NotFound();
        if (mail.EmailStatus is null) return Results.Conflict(new { error = "Bu bildirim için e-posta istenmemiş; yeniden gönderim yapılmaz." });
        if (mail.EmailStatus is MailDeliveryStatus.Pending) return Results.Conflict(new { error = "Bu ileti zaten gönderim sırasındadır." });
        if (mail.EmailStatus is MailDeliveryStatus.Sending) return Results.Conflict(new { error = "Gönderim şu anda deneniyor; ikinci gönderim yapılmaz." });
        if (mail.EmailStatus is MailDeliveryStatus.Sent) return Results.Conflict(new { error = "Bu ileti e-posta sunucusuna iletildi; tekrar gönderilmez." });
        var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mail.UserId);
        if (account is null || !account.IsActive || account.InvitationPending || account.Email != mail.Email || account.TokenVersion != mail.AccountVersion)
            return Results.Conflict(new { error = "Alıcı hesabı artık uygun değil; yeniden gönderim yapılmadı." });
        mail.EmailStatus = MailDeliveryStatus.Pending; mail.CreatedAt = DateTimeOffset.UtcNow;
        mail.AttemptedAt = null; mail.ErrorCode = ""; mail.Revision++;
        Audit(db, actor, "NotificationMailResent", "UserNotification", mail.Id, null, new { mail.Kind, mail.Email }, r.Reason.Trim());
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { message = "Yeniden gönderim sıraya alındı. Gönderimden önce alıcının hesabı, marka erişimi, kişisel tercihi ve rapor durumu yeniden kontrol edilir." });
    }

    private static string NotificationType(NotificationKind kind) => kind switch
    {
        NotificationKind.PortalReport => "report",
        NotificationKind.DailyTasks => "daily",
        NotificationKind.TaskDue => "task",
        _ => "conversation"
    };

    private static string MailStatusKey(MailDeliveryStatus status) => status switch
    {
        MailDeliveryStatus.Pending => "Pending",
        MailDeliveryStatus.Sending => "Sending",
        MailDeliveryStatus.Sent => "Sent",
        MailDeliveryStatus.Uncertain => "Uncertain",
        _ => "Cancelled"
    };

    private static string? MailReason(MailDeliveryStatus status, string? errorCode) => status switch
    {
        MailDeliveryStatus.Pending => "Gönderim sırasını bekliyor; henüz deneme yapılmadı.",
        MailDeliveryStatus.Sending => "E-posta sunucusuna gönderim deneniyor.",
        MailDeliveryStatus.Sent => "E-posta sunucusu kabul etti. Bu teslim edildiği veya okunduğu anlamına gelmez.",
        MailDeliveryStatus.Uncertain => errorCode == "Interrupted"
            ? "Gönderim yarıda kesildi. Sonuç doğrulanamadığı için otomatik tekrar yapılmaz."
            : "E-posta sunucusu sonucu doğrulayamadı. Otomatik tekrar yapılmaz; önce alıcıyla kontrol edin.",
        _ => errorCode switch
        {
            "NoLongerEligible" => "Gönderimden önce kurallar değişti: alıcının hesabı, tercihi, marka erişimi veya rapor durumu artık uygun değil.",
            "AccountOrLinkChanged" => "Hesap veya bağlantı değişti ya da bağlantının süresi doldu.",
            "ProtectionKeyUnavailable" => "Gönderim anahtarına erişilemedi. Sunucudaki anahtarların kalıcı olduğunu kontrol edin.",
            _ => "Gönderim gönderilmek üzereyken iptal edildi."
        }
    };

    private static string? MailReason(string status) => status switch
    {
        "Pending" => "Deneme istendi; sonuç henüz yazılmadı.",
        "Sent" => "Deneme iletisini e-posta sunucusu kabul etti.",
        "Uncertain" => "Deneme iletisinin sonucu doğrulanamadı.",
        _ => null
    };
}
