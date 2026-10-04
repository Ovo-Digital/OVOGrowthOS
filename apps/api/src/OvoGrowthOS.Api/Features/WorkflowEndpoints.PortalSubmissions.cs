using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record PortalPeriodSubmissionRequest(int Year, int Month, decimal? GrossSales, decimal? Refunds,
    decimal? MetaSpend, decimal? GoogleSpend, string Note);
public sealed record PortalSubmissionApplyRequest(string Reason);

public static partial class WorkflowEndpoints
{
    private static void MapPortalSubmissions(RouteGroupBuilder portal, RouteGroupBuilder management)
    {
        portal.MapGet("/submissions", async (AppDbContext db, ClaimsPrincipal user) =>
            await ReadPortalSubmissions(db, await PortalBrand(db, user)));
        portal.MapPut("/submissions", SubmitPortalPeriod);
        management.MapGet("/submissions", async (Guid brandId, AppDbContext db) => await ReadPortalSubmissions(db, brandId));
        management.MapPost("/submissions/{id:guid}/apply", ApplyPortalPeriodSubmission);
    }

    private static async Task<IResult> ReadPortalSubmissions(AppDbContext db, Guid brandId)
    {
        var rows = await db.PortalPeriodSubmissions.AsNoTracking().Where(x => x.BrandId == brandId)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToListAsync();
        var names = await db.UserAccounts.AsNoTracking().Where(x => rows.Select(r => r.SubmittedBy).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        return Results.Ok(rows.Select(x => new
        {
            x.Id, x.Year, x.Month, x.GrossSales, x.Refunds, x.MetaSpend, x.GoogleSpend, x.Note, x.Revision, x.SubmittedAt,
            submittedBy = names.GetValueOrDefault(x.SubmittedBy, "")
        }));
    }

    private static async Task<IResult> SubmitPortalPeriod(PortalPeriodSubmissionRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        var note = (r.Note ?? "").Trim();
        if (!PortalSubmissions.ValidPeriod(r.Year, r.Month))
            return Results.BadRequest(new { error = "Yılı 2020–2100, ayı 1–12 arasında seçin." });
        if (!PortalSubmissions.ValidAmount(r.GrossSales) || !PortalSubmissions.ValidAmount(r.Refunds)
            || !PortalSubmissions.ValidAmount(r.MetaSpend) || !PortalSubmissions.ValidAmount(r.GoogleSpend))
            return Results.BadRequest(new { error = "Tutarlar sıfır veya üzerinde olmalı ve en fazla 1.000.000.000.000 sınırını aşmamalıdır." });
        if (note.Length > PortalSubmissions.MaxNoteLength)
            return Results.BadRequest(new { error = $"Not en fazla {PortalSubmissions.MaxNoteLength} karakter olabilir." });

        var brandId = await PortalBrand(db, user);
        var actor = Guid.Parse(user.FindFirstValue("uid")!);
        var existing = await db.PortalPeriodSubmissions.SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == r.Year && x.Month == r.Month);
        var created = existing is null;
        var row = existing ?? new PortalPeriodSubmission { BrandId = brandId, Year = r.Year, Month = r.Month };
        if (created) db.Add(row);
        object? before = existing is null ? null : new { existing.GrossSales, existing.Refunds, existing.MetaSpend, existing.GoogleSpend, existing.Note, existing.Revision };
        row.GrossSales = r.GrossSales; row.Refunds = r.Refunds; row.MetaSpend = r.MetaSpend; row.GoogleSpend = r.GoogleSpend;
        row.Note = note; row.SubmittedBy = actor; row.SubmittedAt = DateTimeOffset.UtcNow;
        if (!PortalSubmissions.HasContent(row))
            return Results.BadRequest(new { error = "En az bir tutar girin veya kısa bir not yazın." });
        if (!created) row.Revision++;

        Audit(db, user, "PortalPeriodSubmitted", "Brand", brandId, before,
            new { row.Id, row.Year, row.Month, row.GrossSales, row.Refunds, row.MetaSpend, row.GoogleSpend, row.Note, row.Revision });
        await db.SaveChangesAsync();
        return created
            ? Results.Created($"/api/portal/submissions", new { row.Id, row.Revision, row.SubmittedAt })
            : Results.Ok(new { row.Id, row.Revision, row.SubmittedAt });
    }

    // Records the staff approval; the numbers are returned so the open form can be prefilled by hand, never saved here.
    private static async Task<IResult> ApplyPortalPeriodSubmission(Guid brandId, Guid id, PortalSubmissionApplyRequest r, AppDbContext db, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 500)
            return Results.BadRequest(new { error = "1–500 karakterlik gerekçe girin." });
        var s = await db.PortalPeriodSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.BrandId == brandId);
        if (s is null) return Results.NotFound();
        var period = await db.MonthlyPerformances.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId && x.Year == s.Year && x.Month == s.Month);
        if (period is not null && period.Status is MonthlyPerformanceStatus.Locked or MonthlyPerformanceStatus.Invoiced or MonthlyPerformanceStatus.Paid)
            return Results.Conflict(new { error = "Bu dönem kilitli; müşteri bildirimi ön doldurmada kullanılamaz." });

        Audit(db, user, "PortalPeriodSubmissionApplied", "Brand", brandId, null,
            new { s.Id, s.Year, s.Month, s.GrossSales, s.Refunds, s.MetaSpend, s.GoogleSpend, s.Revision }, r.Reason.Trim());
        await db.SaveChangesAsync();
        return Results.Ok(new { s.Year, s.Month, s.GrossSales, s.Refunds, s.MetaSpend, s.GoogleSpend, s.Revision });
    }
}
