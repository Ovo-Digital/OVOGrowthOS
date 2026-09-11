using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class BrandReportingTests
{
    [Fact]
    public void Missing_previous_and_zero_denominator_never_invent_growth()
    {
        Assert.Null(BrandReporting.Change(100, null)); Assert.Null(BrandReporting.Change(100, 0)); Assert.Null(BrandReporting.Change(100, -100));
        Assert.Equal(-.2m, BrandReporting.Change(800, 1000)); Assert.Equal(0, BrandReporting.Change(1000, 1000));
        Assert.Contains(BrandReporting.Explain(null, null, false), x => x.WhatHappened.Contains("sonuç yok"));
    }
    [Fact]
    public void Falling_revenue_rising_returns_and_weaker_ad_efficiency_have_source_based_actions()
    {
        var previous = new MonthlyPerformance { NetRevenue = 1_000_000, GrossSales = 1_200_000, Refunds = 60_000, TotalAdSpend = 200_000, BrandContributionProfit = 100_000 };
        var current = new MonthlyPerformance { NetRevenue = 800_000, GrossSales = 1_000_000, Refunds = 100_000, TotalAdSpend = 200_000, BrandContributionProfit = -10_000 };
        var insights = BrandReporting.Explain(BrandReporting.Metrics(current), BrandReporting.Metrics(previous), true);
        Assert.Contains(insights, x => x.WhatHappened.Contains("%20,00 azaldı"));
        Assert.Contains(insights, x => x.WhatHappened.Contains("5,00x") && x.WhatHappened.Contains("4,00x"));
        Assert.Contains(insights, x => x.WhatHappened.Contains("%5,00") && x.WhatHappened.Contains("%10,00"));
        Assert.Contains(insights, x => x.WhatHappened.Contains("farklı anlaşmalara")); Assert.Contains(insights, x => x.WhatHappened.Contains("sıfır veya negatif"));
        Assert.All(insights, x => { Assert.NotEmpty(x.WhyItMatters); Assert.NotEmpty(x.NextStep); });
    }
    [Fact]
    public void Zero_ad_and_gross_sales_do_not_become_perfect_ratios()
    {
        var metrics = BrandReporting.Metrics(new MonthlyPerformance { NetRevenue = 100 });
        Assert.Null(metrics.Mer); Assert.Null(metrics.RefundRate);
        var insights = BrandReporting.Explain(metrics, null, false);
        Assert.Contains(insights, x => x.WhatHappened.Contains("oranı hesaplanamıyor"));
    }
}
