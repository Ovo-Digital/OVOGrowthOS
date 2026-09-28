using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed class LeadTimeoutQueue(AppDbContext db)
{
    public const int TimeoutDays = 14;

    public async Task<int> RunDue(DateTimeOffset now, CancellationToken ct = default)
    {
        var cutoff = now.AddDays(-TimeoutDays);
        var rows = await db.BrandStageHistories
            .Where(x => x.ExitedAt == null && x.EntryKnown && x.EnteredAt <= cutoff && x.TimeoutTaskId == null)
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;
        var brandIds = rows.Select(x => x.BrandId).Distinct().ToList();
        var brands = await db.Brands.AsNoTracking()
            .Where(x => brandIds.Contains(x.Id) && (x.Status == BrandStatus.Lead || x.Status == BrandStatus.Evaluation || x.Status == BrandStatus.Negotiation))
            .ToDictionaryAsync(x => x.Id, ct);
        var followUps = await db.BrandFollowUps.AsNoTracking().Where(x => brandIds.Contains(x.BrandId)).ToListAsync(ct);
        var owners = followUps.GroupBy(x => x.BrandId).ToDictionary(g => g.Key, g => g.First().OwnerId);
        var staff = (await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && x.Role != "BrandClient").Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var today = TeamWork.Today(now);
        var created = 0;
        foreach (var row in rows)
        {
            if (!brands.TryGetValue(row.BrandId, out var brand)) continue;
            if (!owners.TryGetValue(row.BrandId, out var ownerId) || ownerId is not { } assignee || !staff.Contains(assignee)) continue;
            var open = await db.WorkTasks.AsNoTracking()
                .Where(x => x.BrandId == row.BrandId && x.Kind == WorkKind.LeadTimeout && x.CompletedAt == null)
                .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            Guid linkId;
            if (open is null)
            {
                var task = new WorkTask
                {
                    BrandId = row.BrandId,
                    AssigneeId = assignee,
                    Title = $"Aşama zaman aşımı: {brand.Name}",
                    Description = $"Satış aşaması {TimeoutDays} gündür değişmiyor. Aşama: {Pipeline.StageLabel(row.Stage)}. "
                        + "Markayla son teması ve sonraki adımı gözden geçirin; aşamayı güncellerseniz takip yeniden başlar.",
                    Priority = WorkPriority.Normal,
                    Kind = WorkKind.LeadTimeout,
                    DueOn = today.AddDays(3),
                    CreatedBy = "Sistem"
                };
                db.WorkTasks.Add(task);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException) { db.ChangeTracker.Clear(); continue; }
                created++;
                linkId = task.Id;
            }
            else linkId = open.Id;
            if (!db.BrandStageHistories.Local.Contains(row)) db.BrandStageHistories.Attach(row);
            row.TimeoutTaskId = linkId;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
        }
        return created;
    }
}
