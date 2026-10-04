using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapHourDeviation(WebApplication app)
    {
        app.MapGet("/api/reports/hour-deviation", ReadHourDeviation).RequireAuthorization("ReadAccess");
    }

    private static async Task<IResult> ReadHourDeviation(AppDbContext db, DateOnly? weekFrom = null, DateOnly? weekTo = null, Guid? brandId = null)
    {
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        weekTo ??= monday;
        weekFrom ??= weekTo.Value.AddDays(-7 * 11);
        if (weekFrom > weekTo)
            return Results.BadRequest(new { error = "Başlangıç haftası bitiş haftasından sonra olamaz." });
        if (weekTo.Value.AddDays(-364) > weekFrom)
            return Results.BadRequest(new { error = "En fazla 52 haftalık bir aralık seçin." });
        if (brandId.HasValue && !await db.Brands.AsNoTracking().AnyAsync(x => x.Id == brandId)) return Results.NotFound();

        var plans = await db.TaskHourPlans.AsNoTracking()
            .Where(x => x.WeekStart >= weekFrom && x.WeekStart <= weekTo)
            .Select(x => new { x.TaskId, x.Hours }).ToListAsync();
        var entries = await db.TaskTimeEntries.AsNoTracking()
            .Where(x => x.WeekStart >= weekFrom && x.WeekStart <= weekTo)
            .Select(x => new { x.TaskId, x.Hours, x.VoidedAt }).ToListAsync();
        var ids = plans.Select(x => x.TaskId).Concat(entries.Select(x => x.TaskId)).Distinct().ToList();
        var tasks = (await db.WorkTasks.AsNoTracking().Include(x => x.Brand)
                .Where(x => ids.Contains(x.Id) && (!brandId.HasValue || x.BrandId == brandId)).ToListAsync())
            .ToDictionary(x => x.Id);
        var planLookup = plans.ToLookup(x => x.TaskId);
        var entryLookup = entries.ToLookup(x => x.TaskId);
        var rows = tasks.Values.Select(task => new HourDeviationInput(task.BrandId, task.Brand?.Name ?? "", task.Id, task.Title,
            planLookup[task.Id].Sum(x => x.Hours),
            entryLookup[task.Id].Where(x => x.VoidedAt == null).Sum(x => x.Hours),
            entryLookup[task.Id].Where(x => x.VoidedAt != null).Sum(x => x.Hours))).ToList();

        var totals = HourDeviation.Total(rows);
        return Results.Ok(new
        {
            weekFrom, weekTo, brandId,
            totals = new { totals.TaskCount, totals.PlannedHours, totals.ActualHours, totals.VoidedHours, totals.DifferenceHours, totals.CompletionRatio, status = HourDeviation.StatusLabel(totals) },
            items = HourDeviation.ByBrand(rows).Select(x => new
            {
                x.BrandId, brand = x.BrandName, x.TaskCount, x.PlannedHours, x.ActualHours, x.VoidedHours,
                x.DifferenceHours, x.CompletionRatio, status = HourDeviation.StatusLabel(x)
            }),
            tasks = rows.OrderByDescending(x => Math.Abs(x.ActualHours - x.PlannedHours)).ThenBy(x => x.TaskTitle, StringComparer.Ordinal)
                .Take(100).Select(x => new
                {
                    x.BrandId, brand = x.BrandName, task = x.TaskTitle, x.PlannedHours, x.ActualHours,
                    differenceHours = decimal.Round(x.ActualHours - x.PlannedHours, 2, MidpointRounding.AwayFromZero)
                }).ToList()
        });
    }
}
