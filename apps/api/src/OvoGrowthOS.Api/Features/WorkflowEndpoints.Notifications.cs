using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record NotificationPreferenceRequest(bool DailyTasksEmail, bool TaskDueEmail, bool PortalMessagesEmail, bool PortalReportsEmail, int Revision);

public static partial class WorkflowEndpoints
{
    private static void MapNotifications(WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization("SessionAccess");
        group.MapPost("/refresh", async (ClaimsPrincipal actor, NotificationService service, CancellationToken ct) =>
        { await service.Refresh(Guid.Parse(actor.FindFirstValue("uid")!), DateTimeOffset.UtcNow, ct); return Results.NoContent(); }).RequireRateLimiting("login");
        group.MapGet("/", async (ClaimsPrincipal actor, AppDbContext db, NotificationService service, SmtpSettings settings) =>
        {
            var id = Guid.Parse(actor.FindFirstValue("uid")!);
            var user = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == id);
            var pref = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            var rows = await db.UserNotifications.AsNoTracking().Where(x => x.UserId == id && x.CreatedAt >= cutoff).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
            var visible = new List<object>();
            foreach (var n in rows)
            {
                var content = await service.Resolve(n, user, DateTimeOffset.UtcNow);
                if (content is not null) visible.Add(new { n.Id, n.Kind, content.Title, content.Href, n.CreatedAt, n.ReadAt, n.EmailStatus, n.ErrorCode });
            }
            return Results.Ok(new { items = visible, preferences = pref is null ? null : new { pref.DailyTasksEmail, pref.TaskDueEmail, pref.PortalMessagesEmail, pref.PortalReportsEmail, pref.Revision }, emailReady = settings.Ready });
        });
        group.MapPut("/preferences", async (NotificationPreferenceRequest r, ClaimsPrincipal actor, AppDbContext db) =>
        {
            var id = Guid.Parse(actor.FindFirstValue("uid")!);
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            if (tx is not null) await NotificationService.LockUser(db, id);
            var p = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == id);
            if (p is null || p.Revision != r.Revision) return Results.Conflict(new { error = "Tercihler değişmiş. Sayfayı yenileyip tekrar deneyin." });
            p.DailyTasksEmail = r.DailyTasksEmail; p.TaskDueEmail = r.TaskDueEmail;
            p.PortalMessagesEmail = r.PortalMessagesEmail; p.PortalReportsEmail = r.PortalReportsEmail; p.Revision++;
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
