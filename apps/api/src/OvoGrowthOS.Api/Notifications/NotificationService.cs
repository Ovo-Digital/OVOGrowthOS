using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

public sealed record NotificationContent(string Title, string Href);

public sealed class NotificationService(AppDbContext db, SmtpSettings settings)
{
    public static bool Staff(UserAccount user) => user.Role is "Admin" or "Partner" or "Analyst";
    public static bool Manager(UserAccount user) => user.Role is "Admin" or "Partner";
    public static Task<int> LockUser(AppDbContext db, Guid id, CancellationToken ct = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"UserAccounts\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    public async Task Refresh(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx is not null) await LockUser(db, userId, ct);
        var user = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive || user.InvitationPending) return;
        var pref = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (pref is null) { pref = new NotificationPreference { UserId = userId, StartedAt = now }; db.Add(pref); }
        var since = pref.StartedAt > now.AddDays(-30) ? pref.StartedAt : now.AddDays(-30);
        var existing = (await db.UserNotifications.Where(x => x.UserId == userId).Select(x => x.EventKey).ToListAsync(ct)).ToHashSet();
        void Add(NotificationKind kind, Guid? source, string key, DateTimeOffset happened, DateOnly? day = null)
        {
            if (!existing.Add(key)) return;
            db.Add(new UserNotification { UserId = userId, Kind = kind, SourceId = source, EventKey = key, Day = day,
                CreatedAt = now, Email = user.Email, AccountVersion = user.TokenVersion,
                EmailStatus = settings.Ready && pref.WantsEmail(kind) && happened >= now.AddHours(-24) ? MailDeliveryStatus.Pending : null });
        }
        var today = TeamWork.Today(now);
        if (Staff(user))
        {
            var tasks = await db.WorkTasks.AsNoTracking().Where(x => x.AssigneeId == userId && x.CompletedAt == null).ToListAsync(ct);
            if (tasks.Count > 0 && now.ToOffset(TimeSpan.FromHours(3)).Hour >= 9)
                Add(NotificationKind.DailyTasks, null, $"daily:{today:yyyy-MM-dd}", now, today);
            foreach (var task in tasks.Where(x => x.DueOn <= today.AddDays(1)))
                Add(NotificationKind.TaskDue, task.Id, $"due:{task.Id}:{task.DueOn:yyyy-MM-dd}", now, task.DueOn);
        }
        var brandId = await db.PortalAccesses.Where(x => x.UserId == userId).Select(x => (Guid?)x.BrandId).SingleOrDefaultAsync(ct);
        if (user.Role == "BrandClient" && brandId.HasValue)
        {
            var reports = await db.PortalReports.AsNoTracking().Where(x => x.BrandId == brandId && x.RevokedAt == null && x.PublishedAt >= since).ToListAsync(ct);
            foreach (var report in reports) Add(NotificationKind.PortalReport, report.Id, $"report:{report.Id}", report.PublishedAt);
        }
        if (Manager(user) || user.Role == "BrandClient" && brandId.HasValue)
        {
            var questions = await db.PortalQuestions.AsNoTracking().Where(x =>
                user.Role == "BrandClient" ? x.UserId == userId && x.BrandId == brandId :
                x.OwnerId == userId || x.OwnerId == null && user.Role == "Admin").ToListAsync(ct);
            var activeReports = (await db.PortalReports.Where(x => x.RevokedAt == null).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
            var ids = questions.Where(x => activeReports.Contains(x.ReportId)).Select(x => x.Id).ToArray();
            foreach (var q in questions.Where(x => activeReports.Contains(x.ReportId)))
            {
                if (Manager(user) && q.CreatedAt >= since) Add(NotificationKind.PortalQuestion, q.Id, $"question:{q.Id}", q.CreatedAt);
                if (user.Role == "BrandClient" && q.AnsweredAt is { } answered && answered >= since)
                    Add(NotificationKind.PortalReply, q.Id, $"answer:{q.Id}", answered);
            }
            var messages = await db.PortalMessages.AsNoTracking().Where(x => ids.Contains(x.QuestionId) && x.CreatedAt >= since && x.AuthorId != userId).ToListAsync(ct);
            foreach (var message in messages.Where(x => x.FromStaff == (user.Role == "BrandClient")))
                Add(message.FromStaff ? NotificationKind.PortalReply : NotificationKind.PortalQuestion, message.QuestionId, $"message:{message.Id}", message.CreatedAt);
        }
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    // Resolve again at read/send time. No stored message, report snapshot or financial text is exposed.
    public async Task<NotificationContent?> Resolve(UserNotification n, UserAccount user, DateTimeOffset now, bool lockSource = false, CancellationToken ct = default)
    {
        if (!user.IsActive || user.InvitationPending || n.UserId != user.Id) return null;
        if (n.Kind == NotificationKind.DailyTasks)
        {
            if (!Staff(user) || n.Day != TeamWork.Today(now)) return null;
            var dates = await db.WorkTasks.Where(x => x.AssigneeId == user.Id && x.CompletedAt == null).Select(x => x.DueOn).ToListAsync(ct);
            var today = TeamWork.Today(now);
            return dates.Count > 0 ? new($"Günlük görev özeti: {dates.Count} açık, {dates.Count(x => x == today)} bugün, {dates.Count(x => x < today)} gecikmiş", "/work") : null;
        }
        if (n.Kind == NotificationKind.TaskDue)
        {
            if (!Staff(user)) return null;
            if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"WorkTasks\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
            var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == n.SourceId && x.AssigneeId == user.Id && x.CompletedAt == null, ct);
            return task is not null && task.DueOn == n.Day && task.DueOn <= TeamWork.Today(now).AddDays(1)
                ? new($"Görevinizin son tarihi: {task.DueOn:dd.MM.yyyy}", $"/brands/{task.BrandId}#team-work") : null;
        }
        var brand = await db.PortalAccesses.Where(x => x.UserId == user.Id).Select(x => (Guid?)x.BrandId).SingleOrDefaultAsync(ct);
        if (n.Kind == NotificationKind.PortalReport)
        {
            if (user.Role != "BrandClient" || brand is null) return null;
            if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalReports\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
            return await db.PortalReports.AnyAsync(x => x.Id == n.SourceId && x.BrandId == brand && x.RevokedAt == null, ct)
                ? new("Markanız için yeni bir rapor paylaşıldı", "/portal") : null;
        }
        if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalQuestions\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
        var question = await db.PortalQuestions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == n.SourceId, ct);
        if (question is null) return null;
        if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalReports\" WHERE \"Id\" = {question.ReportId} FOR SHARE", ct);
        if (!await db.PortalReports.AnyAsync(x => x.Id == question.ReportId && x.BrandId == question.BrandId && x.RevokedAt == null, ct)) return null;
        if (n.Kind == NotificationKind.PortalReply)
            return user.Role == "BrandClient" && question.UserId == user.Id && question.BrandId == brand
                ? new("Sorunuza yeni bir yanıt geldi", "/portal") : null;
        return Manager(user) && (question.OwnerId == user.Id || question.OwnerId is null && user.Role == "Admin")
            ? new("Müşteri konuşmasında yeni bir mesaj var", "/portal-management") : null;
    }
}
