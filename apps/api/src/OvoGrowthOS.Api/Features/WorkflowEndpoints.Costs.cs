using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record ServiceCostRequest(Guid Id, ServiceCostKind Kind, decimal Amount, decimal? Hours, decimal? HourlyCost, DateOnly IncurredOn, string Reference, string Description, int Revision);
public sealed record CostConfirmationRequest(bool Complete, string Reason, int Revision);
public sealed record InvestmentEntryRequest(Guid Id, InvestmentEntryKind Kind, decimal Amount, DateOnly OccurredOn, string Reference, string Description, int Revision);

public static partial class WorkflowEndpoints
{
    private static void MapOperatingCosts(WebApplication app)
    {
        // Sensitive actual costs have no navigation on the public period/deal DTOs or amounts in the shared audit feed.
        var costs = app.MapGroup("/api/performance/{id:guid}/costs").RequireAuthorization("OperationsWrite");
        costs.MapGet("/", async (Guid id, AppDbContext db) =>
        {
            var p = await db.MonthlyPerformances.AsNoTracking().Include(x => x.Deal).SingleOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            return Results.Ok(CostView(p, await db.ServiceCostAccounts.AsNoTracking().Include(x => x.Entries).Include(x => x.Reviews).SingleOrDefaultAsync(x => x.MonthlyPerformanceId == id)));
        });
        costs.MapPost("/entries", AddServiceCost).AddEndpointFilter<ValidationFilter<ServiceCostRequest>>();
        costs.MapPost("/entries/{entryId:guid}/void", VoidServiceCost).AddEndpointFilter<ValidationFilter<VoidPaymentRequest>>().RequireAuthorization("AdminOnly");
        costs.MapPut("/confirmation", ConfirmCosts).AddEndpointFilter<ValidationFilter<CostConfirmationRequest>>();
        var investment = app.MapGroup("/api/deals/{id:guid}/investment").RequireAuthorization("OperationsWrite");
        investment.MapGet("/", async (Guid id, AppDbContext db) =>
        {
            var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            return deal is null ? Results.NotFound() : Results.Ok(InvestmentView(deal, await db.InvestmentAccounts.AsNoTracking().Include(x => x.Entries).SingleOrDefaultAsync(x => x.DealId == id)));
        });
        investment.MapPost("/entries", AddInvestmentEntry).AddEndpointFilter<ValidationFilter<InvestmentEntryRequest>>();
        investment.MapPost("/entries/{entryId:guid}/void", VoidInvestmentEntry).AddEndpointFilter<ValidationFilter<VoidPaymentRequest>>().RequireAuthorization("AdminOnly");
    }

    private static object CostView(MonthlyPerformance p, ServiceCostAccount? account) => new
    {
        p.Id, p.Year, p.Month, p.Status, p.OvoFee, p.OvoGrossProfit, currency = p.Deal!.Currency,
        revision = account?.Revision ?? 0, account?.ConfirmedAt, account?.ConfirmedBy, account?.LastReviewReason,
        summary = OperatingCosts.Summary(p, account), entries = (account?.Entries ?? []).OrderByDescending(x => x.IncurredOn).ThenByDescending(x => x.CreatedAt),
        reviews = (account?.Reviews ?? []).OrderByDescending(x => x.CreatedAt)
    };
    private static object InvestmentView(Deal d, InvestmentAccount? account) => new
    {
        d.Id, d.Currency, d.Status, revision = account?.Revision ?? 0, summary = OperatingCosts.InvestmentSummary(d, account),
        entries = (account?.Entries ?? []).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAt)
    };
    private static IResult CostConflict() => Results.Conflict(new { error = "Maliyet kaydı değişti. Sayfayı yenileyip tekrar deneyin." });

    private static async Task<IResult> AddServiceCost(Guid id, ServiceCostRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await db.MonthlyPerformances.Include(x => x.Deal).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!PortfolioReporting.IsClosed(p.Status)) return Results.Conflict(new { error = "Gerçekleşen maliyet karşılaştırması için önce aylık sonucu kapatın." });
        if (r.IncurredOn.Year != p.Year || r.IncurredOn.Month != p.Month || r.IncurredOn > TeamWork.Today(DateTimeOffset.UtcNow))
            return Results.BadRequest(new { error = "Gider tarihi seçili dönem içinde ve bugün veya geçmişte olmalıdır." });
        var account = await db.ServiceCostAccounts.Include(x => x.Entries).SingleOrDefaultAsync(x => x.MonthlyPerformanceId == id);
        if ((account?.Revision ?? 0) != r.Revision) return CostConflict();
        if (account?.ConfirmedAt is not null) return Results.Conflict(new { error = "Maliyet kontrolü tamamlanmış. Düzeltmek için yönetici önce gerekçeyle kontrolü yeniden açmalıdır." });
        var reference = r.Reference.Trim().ToUpperInvariant();
        await LockExpenseReferences(db);
        if (await db.ServiceCostEntries.AnyAsync(x => x.Id == r.Id || x.VoidedAt == null && x.Reference == reference) ||
            await db.InvestmentEntries.AnyAsync(x => x.VoidedAt == null && x.Kind == InvestmentEntryKind.Investment && x.Reference == reference))
            return Results.Conflict(new { error = "Bu gider referansı zaten kayıtlı. Aynı gideri hizmet maliyeti ve yatırım olarak iki kez eklemeyin." });
        if (account is null) { account = new() { MonthlyPerformanceId = id }; db.ServiceCostAccounts.Add(account); }
        var entry = new ServiceCostEntry { Id = r.Id, MonthlyPerformanceId = id, Kind = r.Kind,
            Amount = r.Kind == ServiceCostKind.TeamWork ? OperatingCosts.TeamAmount(r.Hours!.Value, r.HourlyCost!.Value) : r.Amount,
            Hours = r.Hours, HourlyCost = r.HourlyCost, IncurredOn = r.IncurredOn, Reference = reference, Description = r.Description.Trim(), CreatedBy = User(user) };
        account.Entries.Add(entry); db.ServiceCostEntries.Add(entry); account.Revision++;
        Audit(db, user, "ServiceCostAdded", "MonthlyPerformance", id, null, new { entry.Id });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.Ok(CostView(p, account));
    }

    private static async Task<IResult> ConfirmCosts(Guid id, CostConfirmationRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (!r.Complete && !user.IsInRole("Admin")) return Results.Forbid();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var p = await db.MonthlyPerformances.Include(x => x.Deal).SingleOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!PortfolioReporting.IsClosed(p.Status)) return Results.Conflict(new { error = "Önce aylık sonucu kapatın." });
        var a = await db.ServiceCostAccounts.Include(x => x.Entries).SingleOrDefaultAsync(x => x.MonthlyPerformanceId == id);
        if ((a?.Revision ?? 0) != r.Revision) return CostConflict();
        if ((a?.ConfirmedAt is not null) == r.Complete) return Results.Conflict(new { error = "Maliyet kontrolü zaten bu durumda." });
        if (a is null) { a = new() { MonthlyPerformanceId = id }; db.ServiceCostAccounts.Add(a); }
        a.ConfirmedAt = r.Complete ? DateTimeOffset.UtcNow : null; a.ConfirmedBy = r.Complete ? User(user) : "";
        a.LastReviewReason = r.Reason.Trim(); a.Revision++;
        // Reason is private; do not copy it into the analyst-readable audit feed.
        db.ServiceCostReviews.Add(new ServiceCostReview { MonthlyPerformanceId = id, Complete = r.Complete, Reason = r.Reason.Trim(), CreatedBy = User(user) });
        Audit(db, user, r.Complete ? "ServiceCostsConfirmed" : "ServiceCostsReopened", "MonthlyPerformance", id, null, new { complete = r.Complete });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.Ok(CostView(p, a));
    }

    private static async Task<IResult> VoidServiceCost(Guid id, Guid entryId, VoidPaymentRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockCollectionPeriod(db, id);
        var a = await db.ServiceCostAccounts.Include(x => x.Entries).SingleOrDefaultAsync(x => x.MonthlyPerformanceId == id);
        if (a is null) return Results.NotFound(); if (a.Revision != r.Revision) return CostConflict();
        if (a.ConfirmedAt is not null) return Results.Conflict(new { error = "Önce maliyet kontrolünü gerekçeyle yeniden açın." });
        var entry = a.Entries.SingleOrDefault(x => x.Id == entryId); if (entry is null) return Results.NotFound();
        if (entry.VoidedAt is not null) return Results.Conflict(new { error = "Bu gider zaten iptal edilmiş." });
        entry.VoidedAt = DateTimeOffset.UtcNow; entry.VoidedBy = User(user); entry.VoidReason = r.Reason.Trim(); a.Revision++;
        Audit(db, user, "ServiceCostVoided", "MonthlyPerformance", id, null, new { entry.Id });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.NoContent();
    }

    private static async Task LockInvestmentDeal(AppDbContext db, Guid id)
    {
        if (db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PartnershipDeals\" WHERE \"Id\" = {id} FOR UPDATE");
    }
    private static async Task LockExpenseReferences(AppDbContext db)
    {
        // Two ledgers share expense references; serialize their inserts so a cross-ledger duplicate cannot race.
        if (db.Database.IsRelational()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"ServiceCostEntries\", growth.\"InvestmentEntries\" IN SHARE ROW EXCLUSIVE MODE");
    }
    private static async Task<IResult> AddInvestmentEntry(Guid id, InvestmentEntryRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockInvestmentDeal(db, id);
        var d = await db.Deals.SingleOrDefaultAsync(x => x.Id == id); if (d is null) return Results.NotFound();
        if (d.Status is not (DealStatus.Accepted or DealStatus.Active or DealStatus.Expired or DealStatus.Terminated)) return Results.Conflict(new { error = "Yatırım takibi için kabul edilmiş bir anlaşma gerekir." });
        if (r.OccurredOn > TeamWork.Today(DateTimeOffset.UtcNow)) return Results.BadRequest(new { error = "Gerçekleşmiş yatırım veya geri kazanım gelecekte olamaz." });
        var a = await db.InvestmentAccounts.Include(x => x.Entries).SingleOrDefaultAsync(x => x.DealId == id);
        if ((a?.Revision ?? 0) != r.Revision) return CostConflict();
        if (r.Kind == InvestmentEntryKind.Recovery && r.Amount > OperatingCosts.InvestmentSummary(d, a).Remaining)
            return Results.Conflict(new { error = "Geri kazanım, kayıtlı yatırımın kalan tutarını aşamaz. Önce gerçek yatırım harcamasını kaydedin." });
        var reference = r.Reference.Trim().ToUpperInvariant();
        await LockExpenseReferences(db);
        if (await db.InvestmentEntries.AnyAsync(x => x.Id == r.Id || x.VoidedAt == null && x.Reference == reference) ||
            r.Kind == InvestmentEntryKind.Investment && await db.ServiceCostEntries.AnyAsync(x => x.VoidedAt == null && x.Reference == reference))
            return Results.Conflict(new { error = "Bu referans zaten kayıtlı. Aynı harcamayı ikinci kez eklemeyin." });
        if (a is null) { a = new() { DealId = id }; db.InvestmentAccounts.Add(a); }
        var entry = new InvestmentEntry { Id = r.Id, DealId = id, Kind = r.Kind, Amount = r.Amount, OccurredOn = r.OccurredOn,
            Reference = reference, Description = r.Description.Trim(), CreatedBy = User(user) };
        a.Entries.Add(entry); db.InvestmentEntries.Add(entry); a.Revision++;
        Audit(db, user, "InvestmentEntryAdded", "Deal", id, null, new { entry.Id });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.Ok(InvestmentView(d, a));
    }
    private static async Task<IResult> VoidInvestmentEntry(Guid id, Guid entryId, VoidPaymentRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockInvestmentDeal(db, id);
        var a = await db.InvestmentAccounts.Include(x => x.Entries).SingleOrDefaultAsync(x => x.DealId == id);
        if (a is null) return Results.NotFound(); if (a.Revision != r.Revision) return CostConflict();
        var entry = a.Entries.SingleOrDefault(x => x.Id == entryId); if (entry is null) return Results.NotFound();
        if (entry.VoidedAt is not null) return Results.Conflict(new { error = "Bu kayıt zaten iptal edilmiş." });
        var remaining = a.Entries.Where(x => x.VoidedAt == null && x.Id != entryId).Sum(x => x.Kind == InvestmentEntryKind.Investment ? x.Amount : -x.Amount);
        if (remaining < 0) return Results.Conflict(new { error = "Bu harcama iptal edilirse geri kazanım yatırımı aşar. Önce yanlış geri kazanım kaydını düzeltin." });
        entry.VoidedAt = DateTimeOffset.UtcNow; entry.VoidedBy = User(user); entry.VoidReason = r.Reason.Trim(); a.Revision++;
        Audit(db, user, "InvestmentEntryVoided", "Deal", id, null, new { entry.Id });
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.NoContent();
    }
}
