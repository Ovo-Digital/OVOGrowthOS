using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record CollectionPromiseRequest([property: JsonRequired] decimal Amount, DateOnly PromisedOn, Guid ContactNoteId,
    Guid OwnerId, string Reason, [property: JsonRequired] int Revision, [property: JsonRequired] int CollectionRevision);
public sealed record CancelPromiseRequest(string Reason, [property: JsonRequired] int Revision, [property: JsonRequired] int CollectionRevision);
public sealed record PromiseTaskRequest(DateOnly DueOn, [property: JsonRequired] int Revision, [property: JsonRequired] int CollectionRevision);

public static partial class WorkflowEndpoints
{
    private static void MapCollectionPromises(WebApplication app)
    {
        var group = app.MapGroup("/api/performance/{id:guid}/collection/promise").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (Guid id, AppDbContext db) =>
        {
            var p = await CollectionQuery(db).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var today = TeamWork.Today(DateTimeOffset.UtcNow);
            var promise = p.Collection?.Promise;
            var task = promise?.TaskId is { } taskId ? await db.WorkTasks.AsNoTracking().Where(x => x.Id == taskId)
                .Select(x => new { x.Id, x.Title, x.DueOn, x.CompletedAt, assigneeName = x.Assignee!.Name }).SingleAsync() : null;
            return Results.Ok(new { p.Id, p.BrandId, p.Year, p.Month, currency = p.Collection?.Currency ?? p.Deal!.Currency,
                balance = Collections.Balance(p, today), collectionRevision = p.Collection?.Revision ?? 0,
                canRecord = CollectionPromises.CanRecord(p, today), promise = promise is null ? null : await PromiseSnapshot(db, promise),
                expectation = CollectionPromises.Balance(p, today), task });
        });
        group.MapGet("/history", async (Guid id, AppDbContext db, int page = 1) => Results.Ok(await Page(
            db.AuditRecords.AsNoTracking().Where(x => x.EntityType == "CollectionPromise" && x.EntityId == id.ToString())
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id), page, 20)));
        group.MapPut("/", SaveCollectionPromise).AddEndpointFilter<ValidationFilter<CollectionPromiseRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/cancel", CancelCollectionPromise).AddEndpointFilter<ValidationFilter<CancelPromiseRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/task", CreatePromiseTask).AddEndpointFilter<ValidationFilter<PromiseTaskRequest>>().RequireAuthorization("OperationsWrite");
    }

    // Audit snapshots keep the source wording and owner at the time of the change, not just opaque identifiers.
    private static async Task<object> PromiseSnapshot(AppDbContext db, CollectionPromise promise)
    {
        var note = await db.BrandContactNotes.AsNoTracking().SingleAsync(x => x.Id == promise.ContactNoteId);
        var owner = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == promise.OwnerId);
        return new { promise.Amount, promise.PromisedOn, promise.ContactNoteId, sourceContactOn = note.ContactOn, sourceText = note.Text,
            promise.OwnerId, ownerName = owner.Name, ownerActive = owner.IsActive, promise.IsCancelled, promise.Revision, promise.RecordedAt, promise.TaskId };
    }

    private static async Task<IResult> SaveCollectionPromise(Guid id, CollectionPromiseRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await CollectionQuery(db).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        if ((p.Collection?.Revision ?? 0) != r.CollectionRevision) return CollectionConflict();
        if ((p.Collection?.Promise?.Revision ?? 0) != r.Revision) return PromiseConflict();
        if (!CollectionPromises.CanRecord(p, today)) return Results.Conflict(new { error = "Ödeme sözü için fatura takibi başlamış, inceleme gerektirmeyen ve kalan alacağı olan bir dönem gerekir." });
        if (r.Amount > Collections.Balance(p, today).Outstanding) return Results.Conflict(new { error = "Ödeme sözü kalan alacaktan fazla olamaz." });
        if (!await db.UserAccounts.AnyAsync(x => x.Id == r.OwnerId && x.IsActive && x.Role != "BrandClient"))
            return Results.Conflict(new { error = "Ödeme sözünü etkin bir çalışana atayın." });
        var note = await db.BrandContactNotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.ContactNoteId && x.BrandId == p.BrandId);
        if (note is null) return Results.Conflict(new { error = "Bu markaya ait kaynak görüşme notunu seçin." });
        if (note.ContactOn > today || note.ContactOn > r.PromisedOn) return Results.BadRequest(new { error = "Görüşme tarihi gelecekte veya söz verilen ödeme tarihinden sonra olamaz." });
        var promise = p.Collection!.Promise;
        var old = promise is null ? null : await PromiseSnapshot(db, promise);
        if (promise is null) { promise = new CollectionPromise { MonthlyPerformanceId = id }; db.Add(promise); p.Collection.Promise = promise; }
        promise.Amount = r.Amount; promise.PromisedOn = r.PromisedOn; promise.ContactNoteId = r.ContactNoteId; promise.OwnerId = r.OwnerId;
        promise.IsCancelled = false; promise.Revision++; promise.RecordedAt = DateTimeOffset.UtcNow;
        promise.PaymentIdsAtRecording = p.Collection.Payments.Select(x => x.Id).ToArray();
        Audit(db, user, "CollectionPromiseSaved", "CollectionPromise", id, old, await PromiseSnapshot(db, promise), r.Reason.Trim());
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { promise.Revision });
    }

    private static async Task<IResult> CancelCollectionPromise(Guid id, CancelPromiseRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await CollectionQuery(db).SingleOrDefaultAsync(x => x.Id == id);
        if (p?.Collection?.Promise is not { } promise) return Results.NotFound();
        if (p.Collection.Revision != r.CollectionRevision) return CollectionConflict();
        if (promise.Revision != r.Revision || promise.IsCancelled) return PromiseConflict();
        var old = await PromiseSnapshot(db, promise);
        promise.IsCancelled = true; promise.Revision++;
        Audit(db, user, "CollectionPromiseCancelled", "CollectionPromise", id, old, await PromiseSnapshot(db, promise), r.Reason.Trim());
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { promise.Revision });
    }

    private static async Task<IResult> CreatePromiseTask(Guid id, PromiseTaskRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await CollectionQuery(db).SingleOrDefaultAsync(x => x.Id == id);
        if (p?.Collection?.Promise is not { } promise) return Results.NotFound();
        if (p.Collection.Revision != r.CollectionRevision) return CollectionConflict();
        if (promise.Revision != r.Revision) return PromiseConflict();
        if (promise.TaskId is not null) return Results.Conflict(new { error = "Bu dönemin ödeme sözü takip işi zaten var. Tamamlanmış olsa da mevcut işi açın; ikinci iş oluşturulmadı." });
        if (CollectionPromises.Balance(p, TeamWork.Today(DateTimeOffset.UtcNow))?.Remaining is not > 0)
            return Results.Conflict(new { error = "Takip işi açılabilecek bekleyen ödeme sözü yok." });
        if (!await db.UserAccounts.AnyAsync(x => x.Id == promise.OwnerId && x.IsActive && x.Role != "BrandClient"))
            return Results.Conflict(new { error = "Önce ödeme sözünün sorumlusunu etkin bir çalışanla güncelleyin." });
        var old = await PromiseSnapshot(db, promise);
        var note = await db.BrandContactNotes.AsNoTracking().SingleAsync(x => x.Id == promise.ContactNoteId);
        var culture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var task = new WorkTask { Id = Guid.NewGuid(), BrandId = p.BrandId, AssigneeId = promise.OwnerId,
            Title = $"{p.Month}/{p.Year} ödeme sözü takibi", DueOn = r.DueOn, CreatedBy = User(user),
            Description = $"Kaynak hakediş: /commissions/{id}. Söz sürümü: {promise.Revision}. "
                + $"Bildirilen tutar: {promise.Amount.ToString("N4", culture)} {p.Collection.Currency}; ödeme sözü tarihi: {promise.PromisedOn:dd.MM.yyyy}. "
                + $"Görüşme ({note.ContactOn:dd.MM.yyyy}): {note.Text[..Math.Min(note.Text.Length, 2500)]}\n"
                + "Bu metin işin oluşturulduğu anı gösterir; güncel söz ve tam kaynak notu hakediş sayfasındadır. Ödeme sözü tahsilat değildir. İş kendiliğinden tamamlanmaz." };
        db.Add(task); promise.TaskId = task.Id; promise.Revision++;
        Audit(db, user, "WorkTaskCreated", "WorkTask", task.Id, null, task);
        Audit(db, user, "CollectionPromiseTaskCreated", "CollectionPromise", id, old, await PromiseSnapshot(db, promise), "Ödeme sözü için takip işi oluşturuldu.");
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Created($"/api/work-tasks?brandId={p.BrandId}", new { task.Id });
    }

    private static IResult PromiseConflict() => Results.Conflict(new { error = "Ödeme sözü değişmiş veya zaten kaldırılmış. Güncel bilgileri yükleyip tekrar kontrol edin." });
}
