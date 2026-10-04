using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapPreScreening(WebApplication app)
    {
        app.MapGet("/api/leads/pre-screening", ReadPreScreening).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ReadPreScreening(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TeamWork.Today(now);
        var pipelineBrands = await db.Brands.AsNoTracking()
            .Where(x => PipelineStatuses.Contains(x.Status))
            .Select(x => new { x.Id, x.Name, x.ContactName, x.ContactEmail, x.Website, x.Industry })
            .ToListAsync();
        var follows = (await db.BrandFollowUps.AsNoTracking().ToListAsync())
            .Where(x => pipelineBrands.Any(b => b.Id == x.BrandId))
            .GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First());
        var lostIds = follows.Values.Where(x => x.LostOn is not null).Select(x => x.BrandId).ToHashSet();
        var ownerIds = follows.Values.Where(x => x.OwnerId is not null).Select(x => x.OwnerId!.Value).ToHashSet();
        var openRows = (await db.BrandStageHistories.AsNoTracking().Where(x => x.ExitedAt == null).ToListAsync())
            .GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First());
        var lastContacts = (await db.BrandContactNotes.AsNoTracking()
                .GroupBy(x => x.BrandId).Select(g => new { BrandId = g.Key, LastOn = g.Max(x => x.ContactOn) })
                .ToListAsync()).ToDictionary(x => x.BrandId, x => x.LastOn);

        var inputs = new List<PreScreenInput>();
        foreach (var brand in pipelineBrands.Where(x => !lostIds.Contains(x.Id)))
        {
            follows.TryGetValue(brand.Id, out var follow);
            openRows.TryGetValue(brand.Id, out var openRow);
            inputs.Add(new PreScreenInput(brand.Id, brand.Name, brand.ContactName, brand.ContactEmail,
                brand.Website, brand.Industry, follow?.SourceChannel ?? LeadSource.Unspecified,
                follow?.NextStep ?? "", follow?.OwnerId,
                openRow?.EntryKnown ?? false, openRow is null ? null : Pipeline.StageDays(openRow, now),
                lastContacts.GetValueOrDefault(brand.Id)));
        }
        return Results.Ok(PreScreening.Build(inputs));
    }
}
