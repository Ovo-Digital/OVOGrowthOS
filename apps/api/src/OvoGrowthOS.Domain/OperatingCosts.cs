namespace OvoGrowthOS.Domain;

public enum ServiceCostKind { DirectExpense, TeamWork }
public enum InvestmentEntryKind { Investment, Recovery }

public sealed class ServiceCostAccount
{
    public Guid MonthlyPerformanceId { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string ConfirmedBy { get; set; } = "";
    public string LastReviewReason { get; set; } = "";
    public List<ServiceCostEntry> Entries { get; set; } = [];
    public List<ServiceCostReview> Reviews { get; set; } = [];
}
public sealed class ServiceCostReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MonthlyPerformanceId { get; set; }
    public bool Complete { get; set; }
    public string Reason { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ServiceCostEntry
{
    public Guid Id { get; set; }
    public Guid MonthlyPerformanceId { get; set; }
    public ServiceCostKind Kind { get; set; }
    public decimal Amount { get; set; }
    public decimal? Hours { get; set; }
    public decimal? HourlyCost { get; set; }
    public DateOnly IncurredOn { get; set; }
    public string Reference { get; set; } = "";
    public string Description { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VoidedAt { get; set; }
    public string VoidedBy { get; set; } = "";
    public string VoidReason { get; set; } = "";
}
public sealed class InvestmentAccount
{
    public Guid DealId { get; set; }
    public int Revision { get; set; }
    public List<InvestmentEntry> Entries { get; set; } = [];
}
public sealed class InvestmentEntry
{
    public Guid Id { get; set; }
    public Guid DealId { get; set; }
    public InvestmentEntryKind Kind { get; set; }
    public decimal Amount { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string Reference { get; set; } = "";
    public string Description { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VoidedAt { get; set; }
    public string VoidedBy { get; set; } = "";
    public string VoidReason { get; set; } = "";
}

public sealed record ServiceCostSummary(decimal PlannedCost, decimal RecordedCost, decimal DirectExpenses, decimal TeamCost,
    decimal Hours, decimal CostDifference, decimal? ContributionAfterRecordedCosts, decimal? MarginAfterRecordedCosts, bool Complete);
public sealed record InvestmentSummary(decimal PlannedInvestment, decimal RecordedInvestment, decimal RecordedRecovery, decimal Remaining, bool HasRecords);

public static class OperatingCosts
{
    public static decimal TeamAmount(decimal hours, decimal hourlyCost) => decimal.Round(hours * hourlyCost, 4, MidpointRounding.AwayFromZero);

    public static ServiceCostSummary Summary(MonthlyPerformance period, ServiceCostAccount? account)
    {
        var entries = account?.Entries.Where(x => x.VoidedAt is null).ToList() ?? [];
        var direct = entries.Where(x => x.Kind == ServiceCostKind.DirectExpense).Sum(x => x.Amount);
        var team = entries.Where(x => x.Kind == ServiceCostKind.TeamWork).Sum(x => x.Amount);
        var total = direct + team;
        var complete = account?.ConfirmedAt is not null;
        // Actual costs replace the planned cost for this separate view; never subtract both or overwrite the closed period.
        var contribution = complete ? period.OvoFee - total : (decimal?)null;
        return new(period.OvoInternalCost, total, direct, team, entries.Sum(x => x.Hours ?? 0), total - period.OvoInternalCost,
            contribution, complete && period.OvoFee > 0 ? contribution / period.OvoFee : null, complete);
    }

    public static InvestmentSummary InvestmentSummary(Deal deal, InvestmentAccount? account)
    {
        var entries = account?.Entries.Where(x => x.VoidedAt is null).ToList() ?? [];
        var spent = entries.Where(x => x.Kind == InvestmentEntryKind.Investment).Sum(x => x.Amount);
        var recovered = entries.Where(x => x.Kind == InvestmentEntryKind.Recovery).Sum(x => x.Amount);
        return new(deal.SetupInvestment, spent, recovered, spent - recovered, entries.Count > 0);
    }
}
