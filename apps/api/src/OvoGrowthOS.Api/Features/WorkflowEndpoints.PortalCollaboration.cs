using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record PortalMessageRequest(string Text, int Revision);
public sealed record PortalTrackingRequest(PortalConversationStatus Status, Guid? OwnerId, int Revision);
public sealed record PortalDataRequestCreate(string Title, string Instructions, DateOnly? DueOn);
public sealed record PortalDataRequestUpdate(PortalRequestStatus Status, string Reason, int Revision);

public static partial class WorkflowEndpoints
{
    private static void MapPortalCollaboration(RouteGroupBuilder portal, RouteGroupBuilder management)
    {
        management.MapGet("/conversation-owners", async (AppDbContext db) => Results.Ok(await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && (x.Role == "Admin" || x.Role == "Partner")).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync()));
        portal.MapGet("/conversations", async (AppDbContext db, ClaimsPrincipal user) =>
            await PortalConversations(db, await PortalBrand(db, user), Guid.Parse(user.FindFirstValue("uid")!)));
        management.MapGet("/conversations", async (Guid brandId, AppDbContext db) => await PortalConversations(db, brandId, null));
        portal.MapPost("/questions/{id:guid}/messages", async (Guid id, PortalMessageRequest r, AppDbContext db, ClaimsPrincipal user) =>
            await AppendPortalMessage(await PortalBrand(db, user), id, r, db, user, false));
        management.MapPost("/questions/{id:guid}/messages", async (Guid brandId, Guid id, PortalMessageRequest r, AppDbContext db, ClaimsPrincipal user) =>
            await AppendPortalMessage(brandId, id, r, db, user, true));
        management.MapPut("/questions/{id:guid}/tracking", UpdatePortalTracking);

        portal.MapGet("/reports/{id:guid}/reading", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var brandId = await PortalBrand(db, user); var actor = Guid.Parse(user.FindFirstValue("uid")!);
            if (!await db.PortalReports.AnyAsync(x => x.Id == id && x.BrandId == brandId && x.RevokedAt == null)) return Results.NotFound();
            var reading = await db.PortalReportReadings.AsNoTracking().SingleOrDefaultAsync(x => x.ReportId == id && x.UserId == actor && x.BrandId == brandId);
            return Results.Ok(new { reading?.FirstViewedAt, reading?.LastViewedAt, reading?.ReviewedAt });
        });
        portal.MapPost("/reports/{id:guid}/viewed", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await RecordPortalReading(id, false, db, user));
        portal.MapPost("/reports/{id:guid}/reviewed", async (Guid id, AppDbContext db, ClaimsPrincipal user) => await RecordPortalReading(id, true, db, user));
        management.MapGet("/readings", async (Guid brandId, AppDbContext db) => Results.Ok(await (
            from reading in db.PortalReportReadings.AsNoTracking()
            join user in db.UserAccounts on reading.UserId equals user.Id
            join report in db.PortalReports on reading.ReportId equals report.Id
            where reading.BrandId == brandId
            orderby reading.LastViewedAt descending
            select new { reading.ReportId, reading.UserId, user.Name, report.Year, report.Month, report.Version, report.RevokedAt,
                reading.FirstViewedAt, reading.LastViewedAt, reading.ReviewedAt }).ToListAsync()));

        portal.MapGet("/requests", async (AppDbContext db, ClaimsPrincipal user) => await ReadPortalRequests(db, await PortalBrand(db, user)));
        management.MapGet("/requests", async (Guid brandId, AppDbContext db) => await ReadPortalRequests(db, brandId));
        management.MapPost("/requests", CreatePortalDataRequest);
        management.MapPut("/requests/{id:guid}", UpdatePortalDataRequest);
        management.MapGet("/report-updates", ReadPortalReportUpdates);
    }

    private static Task<int> LockPortalQuestion(AppDbContext db, Guid id, Guid brandId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalQuestions\" WHERE \"Id\" = {id} AND \"BrandId\" = {brandId} FOR UPDATE");
    private static IResult PortalStale() => Results.Conflict(new { error = "Bu kayıt değişmiş. Yazdığınız metni koruyup güncel konuşmayı veya talebi yeniden açın." });
    private static PortalConversationStatus ConversationStatus(PortalQuestion q) => q.Status ?? (q.AnsweredAt is null ? PortalConversationStatus.Open : PortalConversationStatus.AwaitingCustomer);

    private static async Task<IResult> PortalConversations(AppDbContext db, Guid brandId, Guid? actor)
    {
        var questions = await db.PortalQuestions.AsNoTracking().Include(x => x.Messages)
            .Where(x => x.BrandId == brandId && (actor == null || x.UserId == actor)).OrderByDescending(x => x.CreatedAt).ToListAsync();
        var reports = await db.PortalReports.AsNoTracking().Where(x => x.BrandId == brandId)
            .Select(x => new { x.Id, x.Year, x.Month, x.Version, x.RevokedAt }).ToDictionaryAsync(x => x.Id);
        var owners = actor is null ? await db.UserAccounts.AsNoTracking().Where(x => x.Role == "Admin" || x.Role == "Partner")
            .Select(x => new { x.Id, x.Name, x.IsActive }).ToDictionaryAsync(x => x.Id) : null;
        var customerIds = questions.Select(x => x.UserId).Distinct().ToArray();
        var customers = actor is null ? await db.UserAccounts.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name) : null;
        return Results.Ok(questions.Select(q => new { q.Id, q.ReportId, q.Question, q.Answer, q.CreatedAt, q.AnsweredAt, q.Revision,
            status = ConversationStatus(q), ownerId = actor is null ? q.OwnerId : null,
            customerName = customers?.GetValueOrDefault(q.UserId),
            ownerName = owners is not null && q.OwnerId is { } owner && owners.TryGetValue(owner, out var account) ? account.Name + (account.IsActive ? "" : " (Hesap kapalı)") : null,
            lastMessageAt = q.Messages.Select(x => x.CreatedAt).Append(q.AnsweredAt ?? q.CreatedAt).Max(),
            lastReplyAt = q.Messages.Where(x => x.FromStaff).Select(x => (DateTimeOffset?)x.CreatedAt).Append(q.AnsweredAt).Max(),
            report = reports[q.ReportId],
            messages = q.Messages.OrderBy(x => x.Sequence).Select(x => new { x.Id, x.Text, x.FromStaff, x.CreatedAt }) }));
    }

    private static async Task<IResult> AppendPortalMessage(Guid brandId, Guid id, PortalMessageRequest r, AppDbContext db, ClaimsPrincipal user, bool staff)
    {
        if (string.IsNullOrWhiteSpace(r.Text) || r.Text.Length > 4000 || r.Revision < 1)
            return Results.BadRequest(new { error = "Mesajınızı 1–4000 karakter arasında yazın ve güncel konuşmayı kullanın." });
        var actor = Guid.Parse(user.FindFirstValue("uid")!);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await LockPortalQuestion(db, id, brandId);
        var q = await db.PortalQuestions.SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId && (staff || x.UserId == actor));
        if (q is null) return Results.NotFound();
        if (q.Revision != r.Revision) return PortalStale();
        if (!await db.PortalReports.AnyAsync(x => x.Id == q.ReportId && x.BrandId == brandId && x.RevokedAt == null))
            return Results.Conflict(new { error = "Raporun paylaşımı geri çekildi. Geçmiş korunur; bu konuya yeni mesaj gönderilemez." });
        var since = DateTimeOffset.UtcNow.AddHours(-1);
        if (await db.PortalMessages.CountAsync(x => x.AuthorId == actor && x.CreatedAt >= since) >= 40)
            return Results.Json(new { error = "Son bir saatte 40 mesaj gönderdiniz. Lütfen biraz bekleyin." }, statusCode: 429);
        q.Revision++; q.Status = staff ? PortalConversationStatus.AwaitingCustomer : PortalConversationStatus.Open;
        var message = new PortalMessage { QuestionId = q.Id, AuthorId = actor, FromStaff = staff, Text = r.Text.Trim(), Sequence = q.Revision };
        db.Add(message);
        Audit(db, user, "PortalMessageSent", "Brand", brandId, null, new { q.Id, messageId = message.Id, q.Status, q.Revision });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.Ok(new { q.Revision });
    }

    private static async Task<IResult> UpdatePortalTracking(Guid brandId, Guid id, PortalTrackingRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!Enum.IsDefined(r.Status) || r.Revision < 1) return Results.BadRequest(new { error = "Geçerli bir konu durumu seçin." });
        if (r.OwnerId is { } owner && !await db.UserAccounts.AnyAsync(x => x.Id == owner && x.IsActive && (x.Role == "Admin" || x.Role == "Partner")))
            return Results.BadRequest(new { error = "Yanıt verebilen etkin bir yönetici veya ortak seçin." });
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await LockPortalQuestion(db, id, brandId);
        var q = await db.PortalQuestions.SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId);
        if (q is null) return Results.NotFound(); if (q.Revision != r.Revision) return PortalStale();
        var before = new { status = ConversationStatus(q), q.OwnerId, q.Revision };
        q.Status = r.Status; q.OwnerId = r.OwnerId; q.Revision++;
        Audit(db, user, "PortalConversationTracked", "Brand", brandId, before, new { q.Id, q.Status, q.OwnerId, q.Revision });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.NoContent();
    }

    private static async Task<IResult> RecordPortalReading(Guid id, bool reviewed, AppDbContext db, ClaimsPrincipal user)
    {
        var brandId = await PortalBrand(db, user); var actor = Guid.Parse(user.FindFirstValue("uid")!);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        // The report lock serializes first insert, repeat clicks and revocation, including separate API instances.
        if (tx is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalReports\" WHERE \"Id\" = {id} AND \"BrandId\" = {brandId} FOR UPDATE");
        if (!await db.PortalReports.AnyAsync(x => x.Id == id && x.BrandId == brandId && x.RevokedAt == null)) return Results.NotFound();
        var row = await db.PortalReportReadings.SingleOrDefaultAsync(x => x.ReportId == id && x.UserId == actor);
        var now = DateTimeOffset.UtcNow;
        if (row is null) { row = new PortalReportReading { ReportId = id, UserId = actor, BrandId = brandId, FirstViewedAt = now }; db.Add(row); }
        row.LastViewedAt = now;
        if (reviewed && row.ReviewedAt is null)
        {
            row.ReviewedAt = now;
            Audit(db, user, "PortalReportReviewed", "Brand", brandId, null, new { reportId = id, userId = actor, row.ReviewedAt });
        }
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { row.FirstViewedAt, row.LastViewedAt, row.ReviewedAt });
    }

    private static async Task<IResult> ReadPortalRequests(AppDbContext db, Guid brandId) => Results.Ok(await db.PortalDataRequests.AsNoTracking()
        .Where(x => x.BrandId == brandId).OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.Id, x.Title, x.Instructions, x.DueOn, x.Status, x.Revision, x.CreatedAt, x.UpdatedAt }).ToListAsync());

    private static async Task<IResult> CreatePortalDataRequest(Guid brandId, PortalDataRequestCreate r, AppDbContext db, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 200 || string.IsNullOrWhiteSpace(r.Instructions) || r.Instructions.Length > 2000 || r.DueOn is { Year: < 2020 or > 2100 })
            return Results.BadRequest(new { error = "1–200 karakterlik başlık, 1–2000 karakterlik açıklama ve varsa geçerli bir tarih girin." });
        if (!await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
        var item = new PortalDataRequest { BrandId = brandId, Title = r.Title.Trim(), Instructions = r.Instructions.Trim(), DueOn = r.DueOn };
        db.Add(item); Audit(db, user, "PortalDataRequested", "Brand", brandId, null, new { item.Id, item.Title, item.Instructions, item.DueOn });
        await db.SaveChangesAsync(); return Results.Created($"/api/portal-management/brands/{brandId}/requests/{item.Id}", new { item.Id });
    }

    private static async Task<IResult> UpdatePortalDataRequest(Guid brandId, Guid id, PortalDataRequestUpdate r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!Enum.IsDefined(r.Status) || r.Revision < 1 || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000)
            return Results.BadRequest(new { error = "Geçerli bir durum ve 1–1000 karakterlik işlem gerekçesi girin." });
        var item = await db.PortalDataRequests.SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId);
        if (item is null) return Results.NotFound(); if (item.Revision != r.Revision) return PortalStale();
        var before = new { item.Id, item.Status, item.Revision }; item.Status = r.Status; item.Revision++; item.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, user, "PortalDataRequestChanged", "Brand", brandId, before, new { item.Id, item.Status, item.Revision }, r.Reason.Trim());
        await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static async Task<IResult> ReadPortalReportUpdates(Guid brandId, AppDbContext db)
    {
        var published = await db.PortalReports.AsNoTracking().Where(x => x.BrandId == brandId && x.RevokedAt == null).ToListAsync();
        var latest = published.GroupBy(x => new { x.Year, x.Month }).Select(x => x.OrderByDescending(r => r.Version).First()).ToArray();
        var ids = latest.Select(x => x.PerformanceId).ToArray();
        var periods = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal)
            .Include(x => x.Collection)!.ThenInclude(x => x!.Payments).Where(x => x.BrandId == brandId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return Results.Ok(latest.Select(r =>
        {
            var snapshot = ReadPortalSnapshot(r); var p = periods.GetValueOrDefault(r.PerformanceId);
            var canPublish = p is not null && PortfolioReporting.Matches(p.Status, ReportScope.Closed);
            var changed = p is null || snapshot.Metrics != BrandReporting.Metrics(p) || snapshot.Currency != p.Deal!.Currency || snapshot.BrandName != p.Brand!.Name;
            return new { r.Id, r.Year, r.Month, r.Version, needsUpdate = changed, canPublish };
        }));
    }
}
