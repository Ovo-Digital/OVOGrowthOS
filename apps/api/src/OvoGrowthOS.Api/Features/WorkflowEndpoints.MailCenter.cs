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
        bool InRange(DateTimeOffset at) => (!from.HasValue || at >= from.Value) && (!to.HasValue || at <= to.Value);

        var items = new List<MailCenterItem>();
        if (type is null or "invitation" or "password")
        {
            var rows = await db.MailDeliveries.AsNoTracking()
                .Where(x => (!from.HasValue || x.CreatedAt >= from.Value) && (!to.HasValue || x.CreatedAt <= to.Value))
                .OrderByDescending(x => x.CreatedAt).Take(MailCenterLimit).ToListAsync();
            var linkIds = rows.Select(x => x.AccountLinkId).Distinct().ToArray();
            var userIds = rows.Select(x => x.UserId).Distinct().ToArray();
            var links = await db.AccountLinks.AsNoTracking().Where(x => linkIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            var accounts = await db.UserAccounts.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            foreach (var row in rows)
            {
                if (!links.TryGetValue(row.AccountLinkId, out var link) || !accounts.TryGetValue(row.UserId, out var account)) continue;
                var kind = link.Purpose == AccountLinkPurpose.Invitation ? "invitation" : "password";
                if (type is not null && kind != type) continue;
                if (status is not null && MailStatusKey(row.Status) != status) continue;
                items.Add(new MailCenterItem(row.Id, "account", kind, link.Email, account.Name, account.Id,
                    MailStatusKey(row.Status), MailReason(row.Status, row.ErrorCode), row.CreatedAt, row.AttemptedAt, row.FinishedAt,
                    kind == "invitation" && account.IsActive && account.InvitationPending && row.Status != MailDeliveryStatus.Sending ? "invitation" : null));
            }
        }
        if (type is null or "report" or "daily" or "task" or "conversation")
        {
            var rows = await db.UserNotifications.AsNoTracking()
                .Where(x => x.EmailStatus != null && (!from.HasValue || x.CreatedAt >= from.Value) && (!to.HasValue || x.CreatedAt <= to.Value))
                .OrderByDescending(x => x.CreatedAt).Take(MailCenterLimit).ToListAsync();
            var userIds = rows.Select(x => x.UserId).Distinct().ToArray();
            var accounts = await db.UserAccounts.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            foreach (var row in rows)
            {
                if (!accounts.TryGetValue(row.UserId, out var account)) continue;
                var kind = NotificationType(row.Kind);
                if (type is not null && kind != type) continue;
                if (status is not null && MailStatusKey(row.EmailStatus!.Value) != status) continue;
                var blocked = row.Email != account.Email || row.AccountVersion != account.TokenVersion || !account.IsActive || account.InvitationPending;
                items.Add(new MailCenterItem(row.Id, "notification", kind, row.Email, account.Name, row.UserId,
                    MailStatusKey(row.EmailStatus!.Value), MailReason(row.EmailStatus!.Value, row.ErrorCode), row.CreatedAt, row.AttemptedAt, null,
                    !blocked && row.EmailStatus is MailDeliveryStatus.Cancelled or MailDeliveryStatus.Uncertain ? "notification" : null));
            }
        }
        if (type is null or "test")
        {
            var tests = await db.AuditRecords.AsNoTracking()
                .Where(x => (x.Action == "MailTestRequested" || x.Action == "MailTestAccepted" || x.Action == "MailTestUncertain")
                    && (!from.HasValue || x.CreatedAt >= from.Value) && (!to.HasValue || x.CreatedAt <= to.Value))
                .OrderByDescending(x => x.CreatedAt).Take(MailCenterLimit).ToListAsync();
            foreach (var row in tests)
            {
                var key = row.Action switch { "MailTestAccepted" => "Sent", "MailTestUncertain" => "Uncertain", _ => "Pending" };
                if (status is not null && key != status) continue;
                items.Add(new MailCenterItem(row.Id, "test", "test", row.UserId, "Deneme e-postası", Guid.Empty,
                    key, MailReason(key), row.CreatedAt, null, null, null));
            }
        }

        var filtered = items.Where(x => InRange(x.CreatedAt))
            .Where(x => match is null || x.Recipient.ToLowerInvariant().Contains(match))
            .OrderByDescending(x => x.CreatedAt).ToList();
        var summary = filtered.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var current = Math.Max(page ?? 1, 1);
        return Results.Ok(new
        {
            items = filtered.Skip((current - 1) * size).Take(size), total = filtered.Count, page = current, pageSize = size, summary,
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
