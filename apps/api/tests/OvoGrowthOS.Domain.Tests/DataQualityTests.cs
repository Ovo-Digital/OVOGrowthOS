using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class DataQualityTests
{
    private static Deal Contract(DateOnly start, DateOnly? end = null) => new()
    {
        Id = Guid.NewGuid(), BrandId = Guid.NewGuid(), EvaluationId = Guid.NewGuid(), Name = "Anlaşma",
        Status = DealStatus.Active, StartDate = start, EndDate = end
    };

    private static Deal Contract() => Contract(new DateOnly(2026, 1, 1));

    private static Deal ContractUnknown() => new()
    {
        Id = Guid.NewGuid(), BrandId = Guid.NewGuid(), EvaluationId = Guid.NewGuid(), Name = "Anlaşma",
        Status = DealStatus.Active, StartDate = null
    };

    private static MonthlyPerformance Row(decimal gross, decimal vat = 0, decimal refunds = 0, decimal ads = 0, decimal costs = 0, int orders = 0)
        => new()
        {
            Id = Guid.NewGuid(), BrandId = Guid.NewGuid(), DealId = Guid.NewGuid(), Year = 2026, Month = 9,
            GrossSales = gross, Vat = vat, Refunds = refunds, TotalAdSpend = ads, Cogs = costs, Orders = orders
        };

    private static QualityInput Input(int year, int month, Deal? deal, MonthlyPerformance? current, MonthlyPerformance? previous = null,
        decimal vatRate = .20m) => new(year, month, Guid.NewGuid(), "Lale", deal, current, previous, "manual", "", "", null, null, false, vatRate);

    private static QualityAlert Find(BrandQuality quality, string code) => quality.Alerts.Single(x => x.Code == code);

    [Fact]
    public void Missing_record_is_reported_as_missing_and_never_as_zero()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), current: null));
        Assert.Equal("missing", quality.Readiness);
        Assert.Null(quality.PerformanceId);
        Assert.All(quality.Sources, s => Assert.Equal("missing", s.State));
        Assert.All(quality.Sources, s => Assert.Contains("sıfır sayılmadı", s.Note));
        Assert.Equal(0m, quality.Sources.Sum(s => s.Amount));
        Assert.Equal("missing_record", Find(quality, "missing_record").Code);
    }

    [Fact]
    public void Unknown_contract_start_is_not_treated_as_a_missing_period()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, ContractUnknown(), current: null));
        Assert.Equal("unknownStart", quality.Expectation);
        Assert.Equal("notApplicable", quality.Readiness);
        Assert.DoesNotContain(quality.Alerts, x => x.Code == "missing_record");
        Assert.Equal("unknown_start", Find(quality, "unknown_start").Code);
    }

    [Fact]
    public void Entered_zero_stays_zero_and_an_empty_month_is_flagged_instead_of_being_called_ready()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(gross: 0)));
        Assert.Equal("attention", quality.Readiness);
        Assert.All(quality.Sources, s => Assert.Equal("zero", s.State));
        Assert.All(quality.Sources, s => Assert.Contains("0 girildi", s.Note));
        Assert.Equal("all_zero", Find(quality, "all_zero").Code);
    }

    [Fact]
    public void A_clean_record_without_a_previous_month_has_no_findings()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(10_000m, vat: 2_000m, refunds: 100m, ads: 500m, costs: 3_000m, orders: 12)));
        Assert.Equal("ready", quality.Readiness);
        Assert.Empty(quality.Alerts);
        Assert.Equal("entered", quality.Sources.Single(x => x.Key == "sales").State);
    }

    [Fact]
    public void Return_share_vat_and_missing_source_checks_are_explained_with_a_next_step()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(10_000m, vat: 1_000m, refunds: 4_000m, ads: 0, costs: 0, orders: 0)));
        Assert.Equal("attention", quality.Readiness);
        Assert.Equal("warning", Find(quality, "returns_share").Severity);
        Assert.Equal("warning", Find(quality, "vat_share").Severity);
        Assert.Equal("info", Find(quality, "missing_ads").Severity);
        Assert.Equal("info", Find(quality, "missing_costs").Severity);
        Assert.Equal("info", Find(quality, "missing_orders").Severity);
        Assert.All(quality.Alerts, a => Assert.False(string.IsNullOrWhiteSpace(a.NextStep)));
    }

    [Fact]
    public void Returns_bigger_than_sales_explain_the_double_deduction_risk()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(1_000m, refunds: 1_200m, orders: 3)));
        Assert.Equal("returns_exceed_sales", Find(quality, "returns_exceed_sales").Code);
        Assert.Contains("iki kez", Find(quality, "returns_exceed_sales").NextStep);
        Assert.DoesNotContain(quality.Alerts, x => x.Code == "returns_share");
    }

    [Fact]
    public void Repeat_return_amount_across_two_months_is_flagged()
    {
        var previous = Row(50_000m, refunds: 800m, orders: 40);
        var current = Row(60_000m, refunds: 800m, orders: 45);
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), current, previous));
        Assert.Equal("repeat_returns", Find(quality, "repeat_returns").Code);
        Assert.DoesNotContain(quality.Alerts, x => x.Code == "same_as_previous");
    }

    [Fact]
    public void A_large_month_over_month_move_is_only_a_review_hint_and_small_or_tiny_moves_are_ignored()
    {
        var big = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(30_000m, orders: 1), Row(10_000m, orders: 1)));
        var change = Find(big, "change_sales");
        Assert.Equal("info", change.Severity);
        Assert.Contains("önceki aya göre", change.Finding);
        Assert.Equal("attention", big.Readiness);

        var small = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(12_000m), Row(10_000m)));
        Assert.DoesNotContain(small.Alerts, x => x.Code.StartsWith("change_"));

        var tiny = DataQuality.Evaluate(Input(2026, 9, Contract(), Row(1_400m), Row(100m)));
        Assert.DoesNotContain(tiny.Alerts, x => x.Code.StartsWith("change_"));
    }

    [Fact]
    public void Identical_values_in_two_months_are_flagged_as_a_possible_copy()
    {
        var previous = Row(10_000m, vat: 2_000m, refunds: 100m, ads: 500m, costs: 3_000m, orders: 12);
        var current = Row(10_000m, vat: 2_000m, refunds: 100m, ads: 500m, costs: 3_000m, orders: 12);
        var quality = DataQuality.Evaluate(Input(2026, 9, Contract(), current, previous));
        Assert.Equal("same_as_previous", Find(quality, "same_as_previous").Code);
    }

    [Fact]
    public void An_existing_record_is_evaluated_even_when_the_contract_scope_is_unknown()
    {
        var quality = DataQuality.Evaluate(Input(2026, 9, ContractUnknown(), Row(10_000m, vat: 2_000m, refunds: 100m, ads: 500m, costs: 3_000m, orders: 12)));
        Assert.Equal("unknownStart", quality.Expectation);
        Assert.Equal("ready", quality.Readiness);
        Assert.NotNull(quality.PerformanceId);
    }

    [Fact]
    public void Approval_is_complete_only_when_a_different_second_person_approved()
    {
        var record = Row(10_000m, vat: 2_000m, refunds: 100m, ads: 500m, costs: 3_000m, orders: 12);
        record.PreparedBy = "hazirlayan@ovo.test";
        var pending = DataQuality.Evaluate(Input(2026, 9, Contract(), record));
        Assert.NotNull(pending.Approval);
        Assert.False(pending.Approval!.Complete);
        record.ReviewedBy = "hazirlayan@ovo.test";
        Assert.False(DataQuality.Evaluate(Input(2026, 9, Contract(), record)).Approval!.Complete);
        record.ReviewedBy = "onaylayan@ovo.test";
        Assert.True(DataQuality.Evaluate(Input(2026, 9, Contract(), record)).Approval!.Complete);
    }

    [Fact]
    public void Summary_counts_each_readiness_state_once()
    {
        BrandQuality Item(string readiness, bool withTask = false) => new(
            Guid.NewGuid(), readiness, Guid.NewGuid(), "expected", Guid.NewGuid(), MonthlyPerformanceStatus.Draft,
            "manual", "", "", null, null, [], [], readiness, withTask ? Guid.NewGuid() : null, false);
        var summary = DataQuality.Summarize([Item("ready"), Item("attention"), Item("attention"), Item("missing"), Item("notApplicable"), Item("missing", true)]);
        Assert.Equal(6, summary.Total);
        Assert.Equal(1, summary.Ready);
        Assert.Equal(2, summary.Attention);
        Assert.Equal(2, summary.Missing);
        Assert.Equal(1, summary.NotApplicable);
        Assert.Equal(1, summary.WithOpenTask);
    }

    [Fact]
    public void Previous_period_wraps_from_january_to_december()
    {
        Assert.Equal(new QualityPeriod(2026, 12), DataQuality.Previous(new QualityPeriod(2027, 1)));
        Assert.Equal(new QualityPeriod(2026, 8), DataQuality.Previous(new QualityPeriod(2026, 9)));
        Assert.Equal("Eylül 2026", DataQuality.Label(2026, 9));
    }
}
