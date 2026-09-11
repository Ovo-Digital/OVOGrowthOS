using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record InvoiceRequest(string Reference, DateOnly? InvoiceOn, DateOnly? DueOn, string Reason, int Revision);
public sealed record PaymentRequest(Guid Id, decimal Amount, DateOnly PaidOn, string Reference, string Note, int Revision);
public sealed record VoidPaymentRequest(string Reason, int Revision);

public static partial class WorkflowEndpoints
{
    private static void MapCollections(WebApplication app)
    {
        var group = app.MapGroup("/api/performance/{id:guid}/collection").RequireAuthorization("ReadAccess");
        group.MapGet("/", async (Guid id, AppDbContext db) =>
        {
            var p = await CollectionQuery(db).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            return p is null ? Results.NotFound() : Results.Ok(CollectionView(p));
        });
        group.MapPut("/invoice", SaveInvoice).AddEndpointFilter<ValidationFilter<InvoiceRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/payments", AddPayment).AddEndpointFilter<ValidationFilter<PaymentRequest>>().RequireAuthorization("OperationsWrite");
        group.MapPost("/payments/{paymentId:guid}/void", VoidPayment).AddEndpointFilter<ValidationFilter<VoidPaymentRequest>>().RequireAuthorization("AdminOnly");
    }

    private static IQueryable<MonthlyPerformance> CollectionQuery(AppDbContext db) => db.MonthlyPerformances
        .Include(x => x.Deal).Include(x => x.Collection)!.ThenInclude(x => x!.Payments);

    private static object CollectionView(MonthlyPerformance p) => new
    {
        p.Id, p.Status, currency = p.Collection?.Currency ?? p.Deal!.Currency,
        balance = Collections.Balance(p, TeamWork.Today(DateTimeOffset.UtcNow)),
        initialized = p.Collection is not null, revision = p.Collection?.Revision ?? 0,
        invoiceReference = p.Collection?.InvoiceReference ?? "", p.Collection?.InvoiceOn, p.Collection?.DueOn,
        payments = (p.Collection?.Payments ?? []).OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.CreatedAt)
    };

    // All settlement writers lock the same period before reading the balance, including on different API instances.
    private static async Task LockCollectionPeriod(AppDbContext db, Guid id)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"MonthlyPerformances\" WHERE \"Id\" = {id} FOR UPDATE");
    }

    private static async Task<IResult> SaveInvoice(Guid id, InvoiceRequest request, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await CollectionQuery(db).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!PortfolioReporting.IsClosed(p.Status)) return Results.Conflict(new { error = "Fatura takibi için önce dönemi onaylayıp kilitleyin." });
        if (p.OvoFee < 0) return Results.Conflict(new { error = "Negatif hakediş inceleme gerektirir. İade ve mahsup bu akışta desteklenmiyor." });
        if ((p.Collection?.Revision ?? 0) != request.Revision) return CollectionConflict();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        if (request.InvoiceOn > today || request.DueOn < request.InvoiceOn)
            return Results.BadRequest(new { error = "Fatura tarihi gelecekte, vade ise fatura tarihinden önce olamaz." });
        if (p.Status == MonthlyPerformanceStatus.Locked && (string.IsNullOrWhiteSpace(request.Reference) || request.InvoiceOn is null || request.DueOn is null))
            return Results.BadRequest(new { error = "Fatura referansı, fatura tarihi ve vade girin." });
        if (p.Collection is not null && string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { error = "Fatura bilgisini değiştirme nedenini yazın." });
        var old = p.Collection is null ? null : JsonSerializer.Serialize(new { p.Collection.InvoiceReference, p.Collection.InvoiceOn, p.Collection.DueOn }, Json);
        p.Collection ??= new CollectionAccount { MonthlyPerformanceId = id, ReceivableAmount = p.OvoFee,
            Currency = p.Deal!.Currency, LegacyPaidAmount = p.Status == MonthlyPerformanceStatus.Paid ? p.OvoFee : 0 };
        p.Collection.InvoiceReference = request.Reference.Trim(); p.Collection.InvoiceOn = request.InvoiceOn;
        p.Collection.DueOn = request.DueOn; p.Collection.Revision++;
        if (p.Status == MonthlyPerformanceStatus.Locked) { p.Status = MonthlyPerformanceStatus.Invoiced; p.CommissionStatus = CommissionStatus.Invoiced; }
        Collections.UpdateSettlementStatus(p, today);
        Audit(db, user, "CollectionInvoiceSaved", "MonthlyPerformance", id, old,
            new { p.Collection.InvoiceReference, p.Collection.InvoiceOn, p.Collection.DueOn, p.Collection.LegacyPaidAmount, request.Reason });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(CollectionView(p));
    }

    private static async Task<IResult> AddPayment(Guid id, PaymentRequest request, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await CollectionQuery(db).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if ((p.Collection?.Revision ?? 0) != request.Revision) return CollectionConflict();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        if (Collections.PaymentError(p, request.Amount, request.PaidOn, today) is { } error) return Results.Conflict(new { error });
        var reference = request.Reference.Trim().ToUpperInvariant();
        if (await db.CollectionPayments.AnyAsync(x => x.Id == request.Id || x.VoidedAt == null && x.Reference == reference))
            return Results.Conflict(new { error = "Bu ödeme kimliği veya referansı zaten kayıtlı. Listeyi kontrol edin; ikinci kez eklenmedi." });
        var payment = new CollectionPayment { Id = request.Id, MonthlyPerformanceId = id, Amount = request.Amount,
            PaidOn = request.PaidOn, Reference = reference, Note = request.Note.Trim(), CreatedBy = User(user) };
        p.Collection!.Payments.Add(payment);
        db.CollectionPayments.Add(payment); p.Collection.Revision++;
        Collections.UpdateSettlementStatus(p, today);
        Audit(db, user, "CollectionPaymentAdded", "MonthlyPerformance", id, null, payment);
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(CollectionView(p));
    }

    private static async Task<IResult> VoidPayment(Guid id, Guid paymentId, VoidPaymentRequest request, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await CollectionQuery(db).SingleOrDefaultAsync(x => x.Id == id);
        if (p?.Collection is null) return Results.NotFound();
        if (p.Collection.Revision != request.Revision) return CollectionConflict();
        var payment = p.Collection.Payments.SingleOrDefault(x => x.Id == paymentId);
        if (payment is null) return Results.NotFound();
        if (payment.VoidedAt is not null) return Results.Conflict(new { error = "Bu ödeme kaydı zaten iptal edilmiş." });
        payment.VoidedAt = DateTimeOffset.UtcNow; payment.VoidedBy = User(user); payment.VoidReason = request.Reason.Trim();
        p.Collection.Revision++; Collections.UpdateSettlementStatus(p, TeamWork.Today(DateTimeOffset.UtcNow));
        Audit(db, user, "CollectionPaymentVoided", "MonthlyPerformance", id, null, payment);
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(CollectionView(p));
    }

    private static IResult CollectionConflict() => Results.Conflict(new { error = "Tahsilat kaydı değişti. Sayfayı yenileyip güncel bakiye üzerinden yeniden deneyin." });
}
