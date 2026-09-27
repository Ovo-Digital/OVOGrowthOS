using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class StoreOrderStagingTests
{
    private static StoreOrderStaging Row(int number, decimal total, int status = StoreOrderStatus.Complete, decimal paid = 0m, decimal refunded = 0m) =>
        new() { SourceOrderId = $"g{number}", OrderNumber = number, OrderTotal = total, PaidAmount = paid, RefundedAmount = refunded, OrderStatus = status, Currency = "TRY" };

    [Theory]
    [InlineData("2026-08", 2026, 8)]
    [InlineData("2026-12", 2026, 12)]
    [InlineData("2020-01", 2020, 1)]
    public void Valid_periods_are_parsed(string value, int year, int month)
    {
        Assert.True(StoreOrderPeriod.TryParse(value, out var period));
        Assert.Equal(year, period.Year);
        Assert.Equal(month, period.Month);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-8")]
    [InlineData("202608")]
    [InlineData("2026-13")]
    [InlineData("2019-08")]
    [InlineData("abcd-ef")]
    public void Invalid_periods_are_rejected(string? value)
    {
        Assert.False(StoreOrderPeriod.TryParse(value, out _));
    }

    [Fact]
    public void Period_boundaries_are_turkey_time_converted_to_utc()
    {
        var period = StoreOrderPeriod.Parse("2026-08");
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 21, 0, 0, TimeSpan.Zero), period.UtcStart);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 21, 0, 0, TimeSpan.Zero), period.UtcEnd);
        Assert.True(period.Contains(period.UtcStart));
        Assert.False(period.Contains(period.UtcEnd));
        Assert.False(period.Contains(new DateTimeOffset(2026, 7, 31, 20, 59, 59, TimeSpan.Zero)));
        Assert.True(period.Contains(new DateTimeOffset(2026, 8, 31, 20, 59, 59, TimeSpan.Zero)));
    }

    [Fact]
    public void February_and_year_end_boundaries_close_correctly()
    {
        var february = StoreOrderPeriod.Parse("2026-02");
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 21, 0, 0, TimeSpan.Zero), february.UtcEnd);
        var december = StoreOrderPeriod.Parse("2026-12");
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 21, 0, 0, TimeSpan.Zero), december.UtcEnd);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)), december.UtcEnd.ToOffset(TimeSpan.FromHours(3)));
    }

    [Fact]
    public void Summary_sums_decimal_amounts_and_separates_cancelled_orders()
    {
        var rows = new[]
        {
            Row(1, 952.20m, StoreOrderStatus.Complete, paid: 952.20m),
            Row(2, 476.10m, StoreOrderStatus.Cancelled),
            Row(3, 557.10m, StoreOrderStatus.Processing, refunded: 100.00m)
        };
        var summary = StoreOrderSummary.Summarize(rows, "TRY");
        Assert.Equal(3, summary.OrderCount);
        Assert.Equal(1985.40m, summary.GrossTotal);
        Assert.Equal(1, summary.CancelledCount);
        Assert.Equal(476.10m, summary.CancelledTotal);
        Assert.Equal(1509.30m, summary.ActiveTotal);
        Assert.Equal(952.20m, summary.PaidTotal);
        Assert.Equal(100.00m, summary.RefundedTotal);
        Assert.Equal("TRY", summary.Currency);
    }

    [Fact]
    public void Summary_of_no_rows_is_zero_and_panel_comparison_is_absent()
    {
        var summary = StoreOrderSummary.Summarize([], "");
        Assert.Equal(0, summary.OrderCount);
        Assert.Equal(0m, summary.GrossTotal);
        Assert.Equal(0m, summary.ActiveTotal);
        var comparison = summary.ComparePanel(null);
        Assert.False(comparison.PanelExists);
        Assert.Null(comparison.Difference);
    }

    [Fact]
    public void Panel_comparison_reports_signed_decimal_difference()
    {
        var summary = StoreOrderSummary.Summarize(new[] { Row(1, 1428.30m, StoreOrderStatus.Complete) }, "TRY");
        var above = summary.ComparePanel(1400.00m);
        Assert.True(above.PanelExists);
        Assert.Equal(28.30m, above.Difference);
        var below = summary.ComparePanel(1500.00m);
        Assert.Equal(-71.70m, below.Difference);
        var exact = summary.ComparePanel(1428.30m);
        Assert.Equal(0m, exact.Difference);
    }
}
