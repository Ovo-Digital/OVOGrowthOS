using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record ScopeItemRequest(Guid Id, string Title, string Description);
public sealed record ScopeRemoveRequest(string Reason);
public sealed record ScopeCreateRequest(Guid Id, string Title, string Description);
public sealed record ScopeDecisionRequest(string Decision, string Note);

public static partial class WorkflowEndpoints
{
    private static void MapDealScope(WebApplication app)
    {
        app.MapGet("/api/deals/{id:guid}/scope", ReadDealScope).RequireAuthorization("ReadAccess");
        app.MapPost("/api/deals/{id:guid}/scope/items", AddScopeItem).AddEndpointFilter<ValidationFilter<ScopeItemRequest>>().RequireAuthorization("OperationsWrite");
        app.MapPost("/api/deals/{id:guid}/scope/items/{itemId:guid}/remove", RemoveScopeItem).AddEndpointFilter<ValidationFilter<ScopeRemoveRequest>>().RequireAuthorization("OperationsWrite");
        app.MapPost("/api/deals/{id:guid}/scope/requests", AddScopeRequest).AddEndpointFilter<ValidationFilter<ScopeCreateRequest>>().RequireAuthorization("OperationsWrite");
        app.MapPost("/api/deals/{id:guid}/scope/requests/{requestId:guid}/decision", DecideScopeRequest).AddEndpointFilter<ValidationFilter<ScopeDecisionRequest>>().RequireAuthorization("OperationsWrite");
    }

    private static async Task<object?> DealScopeState(AppDbContext db, Guid id)
    {
        var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (deal is null) return null;
        var items = await db.DealScopeItems.AsNoTracking().Where(x => x.DealId == id).OrderBy(x => x.CreatedAt).ToListAsync();
        var requests = await db.DealScopeRequests.AsNoTracking().Where(x => x.DealId == id).OrderByDescending(x => x.RequestedAt).ToListAsync();
        return new
        {
            dealId = id, status = deal.Status, editable = DealScope.Editable(deal.Status),
            items = items.Where(x => x.RemovedAt is null).Select(x => new { x.Id, x.Title, x.Description, x.CreatedBy, x.CreatedAt }),
            removed = items.Where(x => x.RemovedAt is not null).Select(x => new { x.Id, x.Title, x.Description, x.RemovedAt, x.RemovedBy, x.RemoveReason }),
            requests = requests.Select(x => new
            {
                x.Id, x.Title, x.Description, x.Status, statusLabel = DealScope.StatusLabel(x.Status),
                x.RequestedBy, x.RequestedAt, x.DecidedBy, x.DecidedAt, x.DecisionNote, x.ScopeItemId
            }),
            counts = new
            {
                active = items.Count(x => x.RemovedAt is null),
                pending = requests.Count(x => x.Status == ScopeRequestStatus.Pending),
                approved = requests.Count(x => x.Status == ScopeRequestStatus.Approved),
                rejected = requests.Count(x => x.Status == ScopeRequestStatus.Rejected)
            }
        };
    }

    private static async Task<IResult> ReadDealScope(Guid id, AppDbContext db)
    {
        var state = await DealScopeState(db, id);
        return state is null ? Results.NotFound() : Results.Ok(state);
    }

    private static async Task<IResult> AddScopeItem(Guid id, ScopeItemRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (deal is null) return Results.NotFound();
        if (!DealScope.Editable(deal.Status)) return Results.Conflict(new { error = DealScope.EditError(deal.Status) });
        var title = r.Title.Trim();
        if (await db.DealScopeItems.AnyAsync(x => x.Id == r.Id)) return Results.Conflict(new { error = "Bu kapsam kalemi daha önce kaydedildi. Listeyi yenileyin." });
        if (await db.DealScopeItems.AnyAsync(x => x.DealId == id && x.Title == title && x.RemovedAt == null))
            return Results.Conflict(new { error = "Bu anlaşma kapsamında aynı başlıklı bir kalem zaten var." });
        var item = new DealScopeItem { Id = r.Id, DealId = id, Title = title, Description = r.Description.Trim(), CreatedBy = User(user) };
        db.DealScopeItems.Add(item);
        Audit(db, user, "DealScopeItemAdded", "Deal", id, null, new { item.Id, item.Title });
        await db.SaveChangesAsync();
        return Results.Created($"/api/deals/{id}/scope", item);
    }

    private static async Task<IResult> RemoveScopeItem(Guid id, Guid itemId, ScopeRemoveRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (deal is null) return Results.NotFound();
        if (!DealScope.Editable(deal.Status)) return Results.Conflict(new { error = DealScope.EditError(deal.Status) });
        await LockInvestmentDeal(db, id);
        var item = await db.DealScopeItems.SingleOrDefaultAsync(x => x.Id == itemId && x.DealId == id);
        if (item is null) return Results.NotFound();
        if (item.RemovedAt is not null) return Results.Conflict(new { error = "Bu kapsam kalemi zaten çıkarılmış." });
        item.RemovedAt = DateTimeOffset.UtcNow; item.RemovedBy = User(user); item.RemoveReason = r.Reason.Trim();
        Audit(db, user, "DealScopeItemRemoved", "Deal", id, null, new { item.Id, reason = item.RemoveReason });
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> AddScopeRequest(Guid id, ScopeCreateRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (deal is null) return Results.NotFound();
        if (!DealScope.Editable(deal.Status)) return Results.Conflict(new { error = DealScope.EditError(deal.Status) });
        var title = r.Title.Trim();
        if (await db.DealScopeRequests.AnyAsync(x => x.Id == r.Id)) return Results.Conflict(new { error = "Bu talep daha önce kaydedildi. Listeyi yenileyin." });
        if (await db.DealScopeRequests.AnyAsync(x => x.DealId == id && x.Title == title && x.Status == ScopeRequestStatus.Pending))
            return Results.Conflict(new { error = "Aynı başlıklı bir paket dışı talep zaten bekliyor." });
        var request = new DealScopeRequest { Id = r.Id, DealId = id, Title = title, Description = r.Description.Trim(), RequestedBy = User(user) };
        db.DealScopeRequests.Add(request);
        Audit(db, user, "DealScopeRequested", "Deal", id, null, new { request.Id, request.Title });
        await db.SaveChangesAsync();
        return Results.Created($"/api/deals/{id}/scope", request);
    }

    private static async Task<IResult> DecideScopeRequest(Guid id, Guid requestId, ScopeDecisionRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (deal is null) return Results.NotFound();
        if (!DealScope.Editable(deal.Status)) return Results.Conflict(new { error = DealScope.EditError(deal.Status) });
        var decision = r.Decision?.Trim().ToLowerInvariant();
        if (decision is not ("approved" or "rejected"))
            return Results.BadRequest(new { error = "Talebi onaylayın veya reddedin." });
        await LockInvestmentDeal(db, id);
        var request = await db.DealScopeRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.DealId == id);
        if (request is null) return Results.NotFound();
        var pending = DealScope.DecisionError(request.Status);
        if (pending is not null) return Results.Conflict(new { error = pending });
        var note = r.Note.Trim();
        var approved = decision == "approved";
        var item = DealScope.Approve(request, id, User(user), DateTimeOffset.UtcNow);
        if (approved)
        {
            if (item is null) return Results.Conflict(new { error = "Bu talep için kapsam kalemi oluşturulamadı. Listeyi yenileyin." });
            if (await db.DealScopeItems.AnyAsync(x => x.DealId == id && x.Title == item.Title && x.RemovedAt == null))
                return Results.Conflict(new { error = "Bu anlaşma kapsamında aynı başlıklı bir kalem zaten var. Önce mevcut kalemi çıkarın." });
            db.DealScopeItems.Add(item);
        }
        request.Status = approved ? ScopeRequestStatus.Approved : ScopeRequestStatus.Rejected;
        request.DecidedBy = User(user); request.DecidedAt = DateTimeOffset.UtcNow; request.DecisionNote = note;
        request.ScopeItemId = approved ? item!.Id : null;
        Audit(db, user, approved ? "DealScopeRequestApproved" : "DealScopeRequestRejected", "Deal", id, null,
            new { request.Id, request.Title, request.ScopeItemId, reason = note });
        await db.SaveChangesAsync();
        return Results.Ok(await DealScopeState(db, id));
    }
}
