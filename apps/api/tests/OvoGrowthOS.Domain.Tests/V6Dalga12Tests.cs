using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class V6Dalga12Tests
{
    private static readonly DateOnly Today = new(2026, 8, 20);

    private static BrandHealthInput Health(string readiness = "ready", int alerts = 0, TargetComparison? target = null,
        PromiseBalance? promise = null, DealStatus? deal = DealStatus.Active) =>
        new(readiness, alerts, target, promise, deal, Today);

    private static TargetComparison MissingTarget() =>
        new(null, MonthlyPerformanceStatus.Approved, DateTimeOffset.UtcNow, false,
            "Bu ay için gerçekleşen sonuç kaydı yok; sıfır kabul edilmedi.", []);

    private static TargetComparison MetTarget() =>
        new(Guid.NewGuid(), MonthlyPerformanceStatus.Approved, DateTimeOffset.UtcNow, false, null,
            [new TargetMetricResult(TargetMetric.NetRevenue, 100, 100, 0, 0m, null, false)]);

    [Fact]
    public void Health_healthy_inputs_score_full_marks_and_four_labeled_factors()
    {
        var score = BrandHealth.Evaluate(Health(target: MetTarget()));
        Assert.Equal(100, score.Score);
        Assert.Equal("Güçlü", score.Band);
        Assert.Equal(4, score.Factors.Count);
        Assert.All(score.Factors, f => Assert.False(string.IsNullOrWhiteSpace(f.Label)));
        Assert.Contains(score.Factors, f => f.Code == "deal" && f.Effect == 0);
        Assert.Contains(score.Factors, f => f.Code == "target" && f.Effect == 0);
    }

    [Fact]
    public void Health_band_boundaries_are_exact_at_85_70_and_50()
    {
        Assert.Equal("Güçlü", BrandHealth.Evaluate(Health(deal: null)).Band);
        Assert.Equal(85, BrandHealth.Evaluate(Health(deal: null)).Score);

        var izlenmeli = BrandHealth.Evaluate(Health(readiness: "attention", alerts: 1, deal: DealStatus.Expired));
        Assert.Equal(70, izlenmeli.Score);
        Assert.Equal("İzlenmeli", izlenmeli.Band);

        var riskli = BrandHealth.Evaluate(Health(readiness: "none", target: MissingTarget(), deal: DealStatus.Terminated));
        Assert.Equal(50, riskli.Score);
        Assert.Equal("Riskli", riskli.Band);

        var kritik = BrandHealth.Evaluate(Health(readiness: "none", target: MissingTarget(),
            promise: new PromiseBalance(100, "Overdue", Today.AddDays(-40)), deal: DealStatus.Terminated));
        Assert.Equal(30, kritik.Score);
        Assert.Equal("Kritik", kritik.Band);
    }

    [Fact]
    public void Health_data_quality_factor_states_and_counts_alerts_in_detail()
    {
        Assert.Equal(0, BrandHealth.Evaluate(Health()).Factors.Single(f => f.Code == "dataQuality").Effect);
        Assert.Equal(-10, BrandHealth.Evaluate(Health("attention", 1)).Factors.Single(f => f.Code == "dataQuality").Effect);
        Assert.Equal(-20, BrandHealth.Evaluate(Health("missing")).Factors.Single(f => f.Code == "dataQuality").Effect);
        Assert.Equal(-20, BrandHealth.Evaluate(Health("none")).Factors.Single(f => f.Code == "dataQuality").Effect);
        var detail = BrandHealth.Evaluate(Health("attention", 3)).Factors.Single(f => f.Code == "dataQuality").Detail;
        Assert.Contains("3 uyarı bulundu", detail);
    }

    [Fact]
    public void Health_target_factor_covers_missing_met_shortfall_and_ad_overspend()
    {
        Assert.Equal(-5, BrandHealth.Evaluate(Health(target: MissingTarget())).Factors.Single(f => f.Code == "target").Effect);

        var met = BrandHealth.Evaluate(Health(target: new TargetComparison(Guid.NewGuid(), MonthlyPerformanceStatus.Approved,
            DateTimeOffset.UtcNow, false, null, [new TargetMetricResult(TargetMetric.NetRevenue, 100, 120, 20, 0.2m, null, false)])));
        Assert.Equal(0, met.Factors.Single(f => f.Code == "target").Effect);

        var shortfall = BrandHealth.Evaluate(Health(target: new TargetComparison(Guid.NewGuid(), MonthlyPerformanceStatus.Approved,
            DateTimeOffset.UtcNow, false, null, [new TargetMetricResult(TargetMetric.NetRevenue, 100, 80, -20, -0.2m, null, true)])));
        Assert.Equal(-10, shortfall.Factors.Single(f => f.Code == "target").Effect);

        var margin = BrandHealth.Evaluate(Health(target: new TargetComparison(Guid.NewGuid(), MonthlyPerformanceStatus.Approved,
            DateTimeOffset.UtcNow, false, null, [new TargetMetricResult(TargetMetric.ContributionMargin, 0.5m, 0.47m, null, null, -3m, true)])));
        Assert.Equal(-5, margin.Factors.Single(f => f.Code == "target").Effect);

        var adOver = BrandHealth.Evaluate(Health(target: new TargetComparison(Guid.NewGuid(), MonthlyPerformanceStatus.Approved,
            DateTimeOffset.UtcNow, false, null, [new TargetMetricResult(TargetMetric.AdSpend, 100, 110, 10, 0.1m, null, true)])));
        Assert.Equal(-5, adOver.Factors.Single(f => f.Code == "target").Effect);
    }

    [Theory]
    [InlineData(40, -20)]
    [InlineData(8, -12)]
    [InlineData(1, -5)]
    public void Health_promise_overdue_penalizes_by_age(int overdueDays, int effect)
    {
        var score = BrandHealth.Evaluate(Health(promise: new PromiseBalance(50, "Overdue", Today.AddDays(-overdueDays))));
        Assert.Equal(effect, score.Factors.Single(f => f.Code == "promise").Effect);
        Assert.Contains($"{overdueDays} gün gecikti", score.Factors.Single(f => f.Code == "promise").Detail);
    }

    [Fact]
    public void Health_promise_without_overdue_and_deal_variants_have_exact_effects()
    {
        Assert.Equal(0, BrandHealth.Evaluate(Health(promise: new PromiseBalance(50, "Waiting", Today.AddDays(1)))).Factors.Single(f => f.Code == "promise").Effect);
        Assert.Equal(0, BrandHealth.Evaluate(Health(promise: new PromiseBalance(0, "Covered", Today.AddDays(-5)))).Factors.Single(f => f.Code == "promise").Effect);
        Assert.Equal(0, BrandHealth.Evaluate(Health(deal: DealStatus.Active)).Factors.Single(f => f.Code == "deal").Effect);
        Assert.Equal(-5, BrandHealth.Evaluate(Health(deal: DealStatus.Accepted)).Factors.Single(f => f.Code == "deal").Effect);
        Assert.Equal(-25, BrandHealth.Evaluate(Health(deal: DealStatus.Terminated)).Factors.Single(f => f.Code == "deal").Effect);
        Assert.Equal(-15, BrandHealth.Evaluate(Health(deal: null)).Factors.Single(f => f.Code == "deal").Effect);
    }

    [Fact]
    public void Sector_empty_and_zero_rows_never_report_invented_ratios()
    {
        var empty = SectorComparison.Build([]);
        Assert.Equal(0, empty.BrandCount);
        Assert.Null(empty.AverageGrossMargin);
        Assert.Null(empty.AverageReturnShare);
        Assert.Null(empty.AverageTargetAchievement);

        var zero = SectorComparison.Build([new SectorSample(0, 0, 0, 0, 0)]);
        Assert.Equal(1, zero.BrandCount);
        Assert.Null(zero.AverageGrossMargin);
        Assert.Null(zero.AverageTargetAchievement);
    }

    [Fact]
    public void Sector_averages_are_per_brand_ratios_rounded_to_four_decimals()
    {
        var metric = SectorComparison.Build([
            new SectorSample(1000, 400, 50, 900, 1000),
            new SectorSample(2000, 600, 100, 800, 1000)
        ]);
        Assert.Equal(2, metric.BrandCount);
        Assert.Equal(0.35m, metric.AverageGrossMargin);
        Assert.Equal(0.05m, metric.AverageReturnShare);
        Assert.Equal(0.85m, metric.AverageTargetAchievement);
    }

    [Fact]
    public void Sector_target_achievement_skips_samples_without_goal()
    {
        var metric = SectorComparison.Build([
            new SectorSample(1000, 400, 50, 900, null),
            new SectorSample(2000, 600, 100, 800, 1000)
        ]);
        Assert.Equal(2, metric.BrandCount);
        Assert.Equal(0.8m, metric.AverageTargetAchievement);
    }

    [Theory]
    [InlineData(MonthlyPerformanceStatus.Draft)]
    [InlineData(MonthlyPerformanceStatus.UnderReview)]
    public void Period_approval_is_closed_until_the_period_is_final(MonthlyPerformanceStatus status)
    {
        var error = PeriodApprovals.DecisionError(true, null, status);
        Assert.NotNull(error);
        Assert.Contains("kesinleşmedi", error);
    }

    [Theory]
    [InlineData(MonthlyPerformanceStatus.Approved)]
    [InlineData(MonthlyPerformanceStatus.Locked)]
    [InlineData(MonthlyPerformanceStatus.Invoiced)]
    [InlineData(MonthlyPerformanceStatus.Paid)]
    public void Period_approval_is_allowed_on_final_periods_without_changing_them(MonthlyPerformanceStatus status)
    {
        Assert.Null(PeriodApprovals.DecisionError(true, null, status));
        Assert.Null(PeriodApprovals.DecisionError(true, "  Onaylıyorum  ", status));
        Assert.Null(PeriodApprovals.DecisionError(false, "Faturalandıktan sonra fark ettim.", status));
    }

    [Fact]
    public void Period_rejection_needs_a_reason_and_reason_length_is_bounded()
    {
        var noReason = PeriodApprovals.DecisionError(false, "   ", MonthlyPerformanceStatus.Locked);
        Assert.NotNull(noReason);
        Assert.Contains("gerekçe", noReason);

        var tooLong = PeriodApprovals.DecisionError(false, new string('x', 1001), MonthlyPerformanceStatus.Locked);
        Assert.NotNull(tooLong);
        Assert.Contains("1000", tooLong);

        Assert.Null(PeriodApprovals.DecisionError(false, new string('x', 1000), MonthlyPerformanceStatus.Locked));
    }

    [Fact]
    public void Reliability_counts_only_kept_and_broken_promises()
    {
        MonthlyPerformance Promised(Guid brand, int month, decimal amount, DateOnly promisedOn, decimal paidAfter, bool cancelled = false)
        {
            var before = Guid.NewGuid();
            var period = new MonthlyPerformance
            {
                BrandId = brand, Status = MonthlyPerformanceStatus.Invoiced, Year = 2026, Month = month,
                Deal = new Deal { Name = "Güven anlaşması", Currency = "TRY" },
                Collection = new CollectionAccount
                {
                    ReceivableAmount = 100, Currency = "TRY",
                    Payments = [new() { Id = before, Amount = 100 - amount, PaidOn = Today.AddDays(-20) }],
                    Promise = new CollectionPromise
                    {
                        Amount = amount, PromisedOn = promisedOn, IsCancelled = cancelled,
                        PaymentIdsAtRecording = [before],
                    }
                }
            };
            if (paidAfter > 0)
                period.Collection.Payments.Add(new() { Amount = paidAfter, PaidOn = promisedOn });
            return period;
        }

        var good = Guid.NewGuid(); var bad = Guid.NewGuid();
        var rows = new[]
        {
            (Promised(good, 6, 60, Today.AddDays(-30), 60), "Düzenli Marka"),
            (Promised(good, 7, 60, Today.AddDays(-10), 0), "Düzenli Marka"),
            (Promised(good, 8, 60, Today.AddDays(5), 0), "Düzenli Marka"),
            (Promised(bad, 7, 80, Today.AddDays(-12), 0), "Geciken Marka"),
            (Promised(bad, 8, 80, Today.AddDays(-9), 0, cancelled: true), "Geciken Marka"),
        };
        var rank = PromiseReliability.Rank(rows, Today, "TRY");
        Assert.Equal(2, rank.Count);
        // Worst first: broken-only brand sorts before the half-kept one.
        Assert.Equal(bad, rank[0].BrandId);
        Assert.Equal(0, rank[0].Score);
        Assert.Equal(1, rank[0].Broken);
        Assert.Equal(12, rank[0].AvgOverdueDays);
        Assert.Equal(good, rank[1].BrandId);
        Assert.Equal(0.5m, rank[1].Score);
        Assert.Equal(1, rank[1].Broken);
        Assert.Equal(1, rank[1].Waiting);
        Assert.Equal(10, rank[1].AvgOverdueDays);
        Assert.Empty(PromiseReliability.Rank(rows, Today, "USD"));
    }
    [Fact]
    public void Promise_reminder_follows_the_due_cascade()
    {
        Assert.Null(CollectionPromises.Reminder(null, Today));
        Assert.Equal(CollectionPromises.ReminderDueIn3, CollectionPromises.Reminder(new PromiseBalance(10, "Waiting", Today.AddDays(3)), Today));
        Assert.Equal(CollectionPromises.ReminderDueTomorrow, CollectionPromises.Reminder(new PromiseBalance(10, "Waiting", Today.AddDays(1)), Today));
        Assert.Equal(CollectionPromises.ReminderDueToday, CollectionPromises.Reminder(new PromiseBalance(10, "Waiting", Today), Today));
        Assert.Null(CollectionPromises.Reminder(new PromiseBalance(10, "Waiting", Today.AddDays(2)), Today));
        Assert.Equal(CollectionPromises.ReminderOverdue, CollectionPromises.Reminder(new PromiseBalance(10, "Overdue", Today.AddDays(-2)), Today));
        Assert.Equal(CollectionPromises.ReminderOverdue, CollectionPromises.Reminder(new PromiseBalance(10, "Overdue", Today.AddDays(-6)), Today));
        Assert.Equal(CollectionPromises.ReminderOverdue7, CollectionPromises.Reminder(new PromiseBalance(10, "Overdue", Today.AddDays(-7)), Today));
        Assert.Equal(CollectionPromises.ReminderOverdue7, CollectionPromises.Reminder(new PromiseBalance(10, "Overdue", Today.AddDays(-30)), Today));
        Assert.Null(CollectionPromises.Reminder(new PromiseBalance(10, "Covered", Today.AddDays(1)), Today));
        Assert.Null(CollectionPromises.Reminder(new PromiseBalance(0, "NeedsReview", Today.AddDays(1)), Today));
    }

    [Fact]
    public void Ad_settings_validation_requires_valid_account_and_google_client_id()
    {
        Assert.Null(AdSettings.Validate(AdPlatform.Meta, "123456789", ""));
        Assert.Null(AdSettings.Validate(AdPlatform.Google, "123456789", "client-123.apps.googleusercontent.com"));
        Assert.NotNull(AdSettings.Validate(AdPlatform.Meta, "", ""));
        Assert.NotNull(AdSettings.Validate(AdPlatform.Meta, "abc def", ""));
        Assert.NotNull(AdSettings.Validate(AdPlatform.Meta, new string('1', 65), ""));
        Assert.NotNull(AdSettings.Validate(AdPlatform.Google, "123456789", ""));
        Assert.Contains("Meta", AdSettings.PlatformLabel(AdPlatform.Meta));
        Assert.Equal("Google Ads", AdSettings.PlatformLabel(AdPlatform.Google));
    }
}
