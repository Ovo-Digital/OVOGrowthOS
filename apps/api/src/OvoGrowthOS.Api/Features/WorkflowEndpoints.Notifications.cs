using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record NotificationPreferenceRequest(bool DailyTasksEmail, bool TaskDueEmail, bool PortalMessagesEmail, bool PortalReportsEmail, int Revision,
    bool PromiseRemindersEmail = false, bool WeeklyDigestEmail = false);

public static partial class WorkflowEndpoints
{
    private static void MapNotifications(WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization("SessionAccess");
        group.MapPost("/refresh", async (ClaimsPrincipal actor, NotificationService service, CancellationToken ct) =>
        { await service.Refresh(Guid.Parse(actor.FindFirstValue("uid")!), DateTimeOffset.UtcNow, ct); return Results.NoContent(); }).RequireRateLimiting("user-action");
        group.MapGet("/", async (ClaimsPrincipal actor, AppDbContext db, NotificationService service, SmtpSettingsProvider provider) =>
        {
            var settings = await provider.GetAsync();
            var id = Guid.Parse(actor.FindFirstValue("uid")!);
            var user = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == id);
            var pref = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            var rows = await db.UserNotifications.AsNoTracking().Where(x => x.UserId == id && x.CreatedAt >= cutoff).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
            var contents = await service.ResolveMany(rows, user, DateTimeOffset.UtcNow);
            var visible = new List<object>();
            for (var i = 0; i < rows.Count; i++)
            {
                var n = rows[i]; var content = contents[i];
                if (content is not null) visible.Add(new { n.Id, n.Kind, content.Title, content.Href, n.CreatedAt, n.ReadAt, n.EmailStatus, n.ErrorCode });
            }
            return Results.Ok(new { items = visible, preferences = pref is null ? null : new { pref.DailyTasksEmail, pref.TaskDueEmail, pref.PortalMessagesEmail, pref.PortalReportsEmail, pref.PromiseRemindersEmail, pref.WeeklyDigestEmail, pref.Revision }, emailReady = settings.Ready });
        });
        group.MapPut("/preferences", async (NotificationPreferenceRequest r, ClaimsPrincipal actor, AppDbContext db) =>
        {
            var id = Guid.Parse(actor.FindFirstValue("uid")!);
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            if (tx is not null) await NotificationService.LockUser(db, id);
            var p = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == id);
            if (p is null || p.Revision != r.Revision) return Results.Conflict(new { error = "Tercihler değişmiş. Sayfayı yenileyip tekrar deneyin." });
            p.DailyTasksEmail = r.DailyTasksEmail; p.TaskDueEmail = r.TaskDueEmail;
            p.PortalMessagesEmail = r.PortalMessagesEmail; p.PortalReportsEmail = r.PortalReportsEmail;
            p.PromiseRemindersEmail = r.PromiseRemindersEmail; p.WeeklyDigestEmail = r.WeeklyDigestEmail; p.Revision++;
            await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.NoContent();
        });
        group.MapPost("/{id:guid}/read", async (Guid id, ClaimsPrincipal actor, AppDbContext db) =>
        {
            var userId = Guid.Parse(actor.FindFirstValue("uid")!);
            var n = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (n is null) return Results.NotFound();
            if (n.ReadAt is null) { n.ReadAt = DateTimeOffset.UtcNow; n.Revision++; await db.SaveChangesAsync(); }
            return Results.NoContent();
        });
    }
}
