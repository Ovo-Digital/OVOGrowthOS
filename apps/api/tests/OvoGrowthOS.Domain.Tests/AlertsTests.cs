using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class AlertsTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);
    private const string Period = "Ekim 2026";

    private static AlertReport Build(
        IReadOnlyCollection<MissingCloseInput>? missing = null,
        IReadOnlyCollection<OverdueReceivableInput>? receivables = null,
        IReadOnlyCollection<OverduePromiseInput>? promises = null,
        IReadOnlyCollection<MerBreachInput>? merBreaches = null,
        int timedOutLeads = 0, int longestLeadDays = 0, int overdueTasks = 0) =>
        AlertEngine.Build(Today, Period, missing ?? [], receivables ?? [], promises ?? [], merBreaches ?? [],
            timedOutLeads, longestLeadDays, overdueTasks);

    [Fact]
    public void Empty_inputs_produce_no_alerts()
    {
        var report = Build();
        Assert.Empty(report.Items);
        Assert.Equal(0, report.Summary.Total);
        Assert.Equal(Period, report.PeriodLabel);
        Assert.Equal(Today, report.Today);
    }

    [Fact]
    public void Missing_close_is_explained_in_turkish_and_never_invents_a_value()
    {
        var report = Build(missing: [new MissingCloseInput(Guid.NewGuid(), "Aura Spor")]);
        var item = Assert.Single(report.Items);
        Assert.Equal("missing_close", item.Code);
        Assert.Equal("warning", item.Severity);
        Assert.Equal("Aura Spor", item.BrandName);
        Assert.Contains("Aura Spor", item.Detail);
        Assert.Contains("Ekim 2026", item.Detail);
        Assert.Contains("girilmemiş", item.Detail);
        Assert.Equal("/data-quality", item.Link);
        Assert.Equal(1, report.Summary.Warning);
    }

    [Fact]
    public void Receivable_overdue_turns_critical_after_thirty_days()
    {
        var brand = Guid.NewGuid();
        var report = Build(receivables:
        [
            new OverdueReceivableInput(brand, "Aura Spor", 12, 4000m, "TRY"),
            new OverdueReceivableInput(brand, "Aura Spor", 45, 9000m, "TRY")
        ]);
        Assert.Equal(2, report.Items.Count);
        Assert.Equal("critical", report.Items[0].Severity);
        Assert.Contains("45 gün", report.Items[0].Detail);
        Assert.Contains("9.000,00 TRY", report.Items[0].Detail);
        Assert.Equal("warning", report.Items[1].Severity);
        Assert.Equal(1, report.Summary.Critical);
        Assert.Equal(1, report.Summary.Warning);
    }

    [Fact]
    public void Overdue_promise_reports_remaining_amount_and_currency()
    {
        var report = Build(promises: [new OverduePromiseInput(Guid.NewGuid(), "Aura Spor", 9, 1500.5m, "USD")]);
        var item = Assert.Single(report.Items);
        Assert.Equal("overdue_promise", item.Code);
        Assert.Equal("warning", item.Severity);
        Assert.Contains("9 gün", item.Detail);
        Assert.Contains("1.500,50 USD", item.Detail);
    }

    [Fact]
    public void Mer_below_breakeven_is_critical_only_when_deeply_below()
    {
        var brand = Guid.NewGuid(); var performance = Guid.NewGuid();
        var report = Build(merBreaches:
        [
            new MerBreachInput(brand, "Aura Spor", performance, "09/2026", 1.5m, 2m),
            new MerBreachInput(Guid.NewGuid(), "Bora Yapı", Guid.NewGuid(), "09/2026", 1.85m, 2m)
        ]);
        Assert.Equal(2, report.Items.Count);
        var critical = report.Items.Single(x => x.Severity == "critical");
        var warning = report.Items.Single(x => x.Severity == "warning");
        Assert.Contains("1,50x", critical.Detail);
        Assert.Contains("2,00x", critical.Detail);
        Assert.Equal($"/performance/{performance}", critical.Link);
        Assert.Contains("1,85x", warning.Detail);
        Assert.Equal(1, report.Summary.Critical);
        Assert.Equal(1, report.Summary.Warning);
    }

    [Fact]
    public void Stage_timeout_and_overdue_tasks_also_reach_the_board()
    {
        var report = Build(timedOutLeads: 3, longestLeadDays: 52, overdueTasks: 4);
        Assert.Contains(report.Items, x => x.Code == "stage_timeout" && x.Detail.Contains("3 marka") && x.Detail.Contains("52 gün"));
        Assert.Contains(report.Items, x => x.Code == "overdue_task" && x.Severity == "info" && x.Detail.Contains("4 görev"));
        Assert.Equal(2, report.Summary.Total);
    }
}
