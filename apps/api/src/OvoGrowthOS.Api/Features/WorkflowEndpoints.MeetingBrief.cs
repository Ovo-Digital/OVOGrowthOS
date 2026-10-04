using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapMeetingBrief(WebApplication app)
    {
        app.MapGet("/api/brands/{id:guid}/meeting-brief", ReadMeetingBrief).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ReadMeetingBrief(Guid id, AppDbContext db)
    {
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (brand is null) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;

        var follow = await db.BrandFollowUps.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == id);
        var openRow = await db.BrandStageHistories.AsNoTracking()
            .Where(x => x.BrandId == id && x.ExitedAt == null)
            .OrderByDescending(x => x.EnteredAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        var lastContact = await db.BrandContactNotes.AsNoTracking()
            .Where(x => x.BrandId == id).OrderByDescending(x => x.ContactOn).ThenByDescending(x => x.CreatedAt)
            .Select(x => (DateOnly?)x.ContactOn).FirstOrDefaultAsync();
        var notes = await db.BrandContactNotes.AsNoTracking()
            .Where(x => x.BrandId == id).OrderByDescending(x => x.ContactOn).ThenByDescending(x => x.CreatedAt)
            .Take(3).ToListAsync();
        var evaluation = await db.Evaluations.AsNoTracking()
            .Where(x => x.BrandId == id).OrderByDescending(x => x.UpdatedAt)
            .Select(x => new { x.Id, x.Status, x.Decision, x.PartnershipScore, x.DataConfidenceScore, x.UpdatedAt })
            .FirstOrDefaultAsync();
        var deals = await db.Deals.AsNoTracking()
            .Where(x => x.BrandId == id && x.Status != DealStatus.Terminated && x.Status != DealStatus.Expired)
            .OrderByDescending(x => x.UpdatedAt).Take(3)
            .Select(x => new { x.Id, x.Name, x.Status, x.DealType, x.StartDate }).ToListAsync();
        var tasks = await db.WorkTasks.AsNoTracking().Include(x => x.Assignee)
            .Where(x => x.BrandId == id && x.CompletedAt == null)
            .OrderBy(x => x.DueOn).ThenBy(x => x.Id).Take(5).ToListAsync();
        var period = await db.MonthlyPerformances.AsNoTracking()
            .Where(x => x.BrandId == id).OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Select(x => new { x.Year, x.Month, x.Status, x.NetRevenue, x.TotalAdSpend, x.Mer })
            .FirstOrDefaultAsync();
        var owner = follow?.OwnerId is { } ownerId
            ? await db.UserAccounts.AsNoTracking().Where(x => x.Id == ownerId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;
        var today = TeamWork.Today(now);

        return Results.Ok(new
        {
            brandId = brand.Id,
            brand = new
            {
                brand.Name, brand.LegalName, brand.Website, brand.Country, brand.Currency,
                brand.Industry, brand.SubIndustry, brand.BusinessModel, brand.Platform, brand.Status,
                brand.ContactName, brand.ContactEmail, brand.ContactPhone
            },
            contact = new
            {
                ownerName = owner,
                stage = follow?.Stage ?? LeadStage.New,
                waitingReason = follow?.WaitingReason ?? "",
                nextContactOn = follow?.NextContactOn,
                nextStep = follow?.NextStep ?? "",
                sourceChannel = follow?.SourceChannel ?? LeadSource.Unspecified,
                sourceNote = follow?.SourceNote ?? "",
                stageEnteredAt = openRow?.EnteredAt,
                stageEntryKnown = openRow?.EntryKnown ?? false,
                stageDays = openRow is null ? null : Pipeline.StageDays(openRow, now),
                lostOn = follow?.LostOn,
                lostReason = follow?.LostReason ?? "",
                lastContactOn = lastContact
            },
            notes = notes.Select(x => new { x.ContactOn, x.Text, x.CreatedBy }),
            evaluation,
            deals,
            tasks = tasks.Select(x => new
            {
                x.Id, x.Title, x.DueOn, x.Priority, x.Kind,
                assigneeName = x.Assignee?.Name ?? "",
                overdue = TeamWork.IsOverdue(x, today)
            }),
            latestPeriod = period is null ? null : new
            {
                period.Year, period.Month, period.Status, period.NetRevenue, period.TotalAdSpend, period.Mer
            },
            generatedAt = now,
            note = "Bu özet salt okunurdur; görüşme notu, aşama, görev veya kaydı kendiliğinden değiştirmez."
        });
    }
}
