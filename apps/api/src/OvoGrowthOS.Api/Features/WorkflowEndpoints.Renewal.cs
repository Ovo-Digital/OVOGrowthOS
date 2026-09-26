using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static void MapRenewalSummary(WebApplication app)
    {
        app.MapGet("/api/deals/{id:guid}/renewal-summary", RenewalSummary).RequireAuthorization("OperationsWrite");
    }

    private static async Task<IResult> RenewalSummary(Guid id, AppDbContext db)
    {
        var deal = await db.Deals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (deal is null) return Results.NotFound();
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var periods = await db.MonthlyPerformances.AsNoTracking()
            .Include(x => x.Collection)
            .ThenInclude(x => x!.Payments)
            .Where(x => x.BrandId == deal.BrandId)
            .ToListAsync();
        var perfIds = periods.Select(x => x.Id).ToList();
        var targets = await db.MonthlyTargets.AsNoTracking()
            .Where(x => x.BrandId == deal.BrandId && x.Currency == deal.Currency)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).Take(24).ToListAsync();
        var perfByMonth = periods.ToDictionary(x => (x.Year, x.Month));
        var targetByMonth = targets.ToDictionary(x => (x.Year, x.Month));
        var months = perfByMonth.Keys.Union(targetByMonth.Keys)
            .OrderByDescending(x => x.Item1).ThenByDescending(x => x.Item2).Take(6)
            .Select(k =>
            {
                var p = perfByMonth.GetValueOrDefault(k);
                var t = targetByMonth.GetValueOrDefault(k);
                var closed = p is not null && PortfolioReporting.IsClosed(p.Status);
                var balance = p is null ? (CollectionBalance?)null : Collections.Balance(p, today);
                return new
                {
                    year = k.Item1, month = k.Item2, status = p?.Status,
                    netRevenue = p?.NetRevenue, target = t?.NetRevenueGoal,
                    receivable = closed ? balance?.Receivable : null, paid = closed ? balance?.Paid : null,
                    outstanding = closed ? balance?.Outstanding : null, overdueDays = balance?.OverdueDays ?? 0
                };
            }).ToList();
        var costEntries = await db.ServiceCostEntries.AsNoTracking()
            .Where(x => perfIds.Contains(x.MonthlyPerformanceId) && x.VoidedAt == null).ToListAsync();
        var confirmedPeriods = await db.ServiceCostAccounts.AsNoTracking()
            .CountAsync(x => perfIds.Contains(x.MonthlyPerformanceId) && x.ConfirmedAt != null);
        var taskIds = await db.WorkTasks.AsNoTracking().Where(x => x.BrandId == deal.BrandId).Select(x => x.Id).ToListAsync();
        var plannedHours = await db.TaskHourPlans.AsNoTracking().Where(x => taskIds.Contains(x.TaskId)).SumAsync(x => x.Hours);
        var actualHours = await db.TaskTimeEntries.AsNoTracking()
            .Where(x => taskIds.Contains(x.TaskId) && x.VoidedAt == null).SumAsync(x => x.Hours);
        var renewalTask = await db.WorkTasks.AsNoTracking()
            .Where(x => x.DealId == id && x.Kind == WorkKind.ContractRenewal)
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
        var successor = await db.Deals.AsNoTracking()
            .Where(x => x.RenewalOfDealId == id).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
        var scopeItems = await db.DealScopeItems.AsNoTracking()
            .CountAsync(x => x.DealId == id && x.RemovedAt == null);
        var pendingRequests = await db.DealScopeRequests.AsNoTracking()
            .CountAsync(x => x.DealId == id && x.Status == ScopeRequestStatus.Pending);
        var rejectedRequests = await db.DealScopeRequests.AsNoTracking()
            .CountAsync(x => x.DealId == id && x.Status == ScopeRequestStatus.Rejected);
        var renewalAssignee = renewalTask is null
            ? null
            : await db.UserAccounts.AsNoTracking().Where(x => x.Id == renewalTask.AssigneeId)
                .Select(x => x.Name).FirstOrDefaultAsync();
        return Results.Ok(new
        {
            dealId = id, deal = new
            {
                deal.Id, deal.Name, deal.Status, deal.DealType, brandId = deal.BrandId, deal.ContractMonths,
                deal.StartDate, deal.EndDate, deal.Currency, deal.MonthlyRetainer, deal.MinimumMonthlyFee,
                deal.RevenueShareRate, deal.EstimatedMonthlyInternalCost
            },
            renewal = new
            {
                renewalOfDealId = deal.RenewalOfDealId,
                successorId = successor?.Id, successorName = successor?.Name, successorStatus = successor?.Status,
                taskId = renewalTask?.Id, taskTitle = renewalTask?.Title, taskDueOn = renewalTask?.DueOn,
                taskAssignee = renewalAssignee,
                taskCompleted = renewalTask?.CompletedAt is not null
            },
            scope = new { activeItems = scopeItems, pendingRequests, rejectedRequests },
            months,
            collections = new
            {
                receivable = months.Sum(x => x.receivable ?? 0),
                paid = months.Sum(x => x.paid ?? 0),
                outstanding = months.Sum(x => x.outstanding ?? 0),
                overdue = months.Max(x => x.overdueDays)
            },
            costs = new
            {
                recorded = costEntries.Sum(x => x.Amount),
                hours = costEntries.Sum(x => x.Hours ?? 0),
                direct = costEntries.Where(x => x.Kind == ServiceCostKind.DirectExpense).Sum(x => x.Amount),
                team = costEntries.Where(x => x.Kind == ServiceCostKind.TeamWork).Sum(x => x.Amount),
                confirmedPeriods
            },
            effort = new { taskCount = taskIds.Count, plannedHours, actualHours, difference = plannedHours - actualHours },
            notes = new[]
            {
                "Bu özet yalnız okuma içindir; otomatik ücret artışı yapmaz ve hiçbir anlaşma koşulunu değiştirmez.",
                "Yenileme kararı ayrı bir adımda verilir; özet tek başına yenileme veya sonlandırma oluşturmaz.",
                "Tahsilat tutarları kapalı dönemlere göre hesaplanır; açık dönemlerde alacak sayılmaz.",
                "Planlanan ve gerçekleşen saat yan yana gösterilir; biri diğerinden türetilmez.",
                "Bu ekran saat ücreti veya çalışan başına maliyet döndürmez."
            }
        });
    }
}
