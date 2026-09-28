using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record PeriodDecisionRequest(bool Approved, string? Reason);

public static partial class WorkflowEndpoints
{
    private static void MapPeriodApprovals(WebApplication app)
    {
        var portal = app.MapGroup("/api/portal").RequireAuthorization("PortalAccess");
        portal.MapGet("/periods", ListPortalPeriods);
        portal.MapPost("/periods/{year:int}/{month:int}/decision", DecidePortalPeriod)
            .RequireRateLimiting("user-action");
    }

    private static async Task<IResult> ListPortalPeriods(AppDbContext db, ClaimsPrincipal actor, CancellationToken ct)
    {
        var brandId = await PortalBrand(db, actor);
        var performances = await db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.BrandId == brandId
                && (x.Status == MonthlyPerformanceStatus.Approved || x.Status == MonthlyPerformanceStatus.Locked
                    || x.Status == MonthlyPerformanceStatus.Invoiced || x.Status == MonthlyPerformanceStatus.Paid))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Take(24)
            .Select(x => new { x.Year, x.Month, x.Status })
            .ToListAsync(ct);
        var approvals = await db.PeriodApprovals.AsNoTracking().Where(x => x.BrandId == brandId).ToListAsync(ct);
        var byPeriod = approvals.ToDictionary(x => (x.Year, x.Month));
        return Results.Ok(new
        {
            items = performances.Select(x =>
            {
                byPeriod.TryGetValue((x.Year, x.Month), out var approval);
                return new
                {
                    x.Year, x.Month, label = DataQuality.Label(x.Year, x.Month),
                    statusText = PeriodStatusText(x.Status),
                    approval = approval is null ? null : new { approval.Approved, approval.Reason, approval.UserEmail, approval.CreatedAt }
                };
            })
        });
    }

    private static async Task<IResult> DecidePortalPeriod(int year, int month, PeriodDecisionRequest r, AppDbContext db, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12) return Results.BadRequest(new { error = "Geçerli bir yıl ve ay seçin." });
        var brandId = await PortalBrand(db, actor);
        var performance = await db.MonthlyPerformances.AsNoTracking()
            .SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == year && x.Month == month, ct);
        if (performance is null) return Results.NotFound(new { error = "Dönem bulunamadı." });
        var error = PeriodApprovals.DecisionError(r.Approved, r.Reason, performance.Status);
        if (error is not null) return Results.Conflict(new { error });
        var approval = await db.PeriodApprovals.SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == year && x.Month == month, ct);
        object? old = approval is null ? null : new { approval.Approved, approval.Reason, approval.CreatedAt };
        var isNew = approval is null;
        approval ??= new PeriodApproval { BrandId = brandId, Year = year, Month = month };
        approval.Approved = r.Approved;
        approval.Reason = r.Approved ? "" : (r.Reason ?? "").Trim();
        approval.UserId = Guid.Parse(actor.FindFirstValue("uid")!);
        approval.UserEmail = actor.FindFirstValue(ClaimTypes.Email) ?? "";
        approval.CreatedAt = DateTimeOffset.UtcNow;
        if (isNew) db.PeriodApprovals.Add(approval);
        Audit(db, actor, "PeriodApprovalDecided", "PeriodApproval", approval.Id, old,
            new { approval.Approved, approval.Reason, year, month, approval.UserEmail });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "Bu dönem için onay kaydı başka bir işlemle değişmiş. Sayfayı yenileyip tekrar deneyin." }); }
        return Results.Ok(new
        {
            year, month, approved = approval.Approved, approval.Reason, approval.CreatedAt,
            message = approval.Approved ? "Dönem onaylandı." : "Dönem reddedildi; gerekçeniz kaydedildi."
        });
    }

    private static string PeriodStatusText(MonthlyPerformanceStatus status) => status switch
    {
        MonthlyPerformanceStatus.Approved => "Ekip onayladı",
        MonthlyPerformanceStatus.Locked => "Kilitli",
        MonthlyPerformanceStatus.Invoiced => "Faturalandı",
        _ => "Ödendi"
    };
}
