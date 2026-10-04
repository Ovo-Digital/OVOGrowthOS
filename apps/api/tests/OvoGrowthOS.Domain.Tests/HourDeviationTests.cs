using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class HourDeviationTests
{
    private static HourDeviationInput Row(string brand, string task, decimal planned, decimal actual, decimal voided = 0) =>
        new(Guid.NewGuid(), brand, Guid.NewGuid(), task, planned, actual, voided);

    [Fact]
    public void Total_sums_every_task_and_counts_distinct_tasks_once()
    {
        var rows = new List<HourDeviationInput> { Row("A", "T1", 10, 12), Row("A", "T2", 6, 4), Row("B", "T3", 4, 4) };
        var total = HourDeviation.Total(rows);
        Assert.Equal(3, total.TaskCount);
        Assert.Equal(20m, total.PlannedHours);
        Assert.Equal(20m, total.ActualHours);
        Assert.Equal(0m, total.DifferenceHours);
        Assert.Equal(1.0m, total.CompletionRatio);
        Assert.Equal("Plana eşit", HourDeviation.StatusLabel(total));
    }

    [Fact]
    public void Brands_are_grouped_and_ordered_by_the_size_of_the_gap()
    {
        var rows = new List<HourDeviationInput> { Row("Büyük sapma", "T1", 10, 4), Row("Az sapma", "T2", 5, 4), Row("Aynı", "T3", 3, 3) };
        var brands = HourDeviation.ByBrand(rows);
        Assert.Equal(3, brands.Count);
        Assert.Equal("Büyük sapma", brands[0].BrandName);
        Assert.Equal(-6m, brands[0].DifferenceHours);
        Assert.Equal(0.4m, brands[0].CompletionRatio);
        Assert.Equal("Planın altında", HourDeviation.StatusLabel(brands[0]));
        Assert.Equal("Az sapma", brands[1].BrandName);
        Assert.Equal("Aynı", brands[2].BrandName);
        Assert.Equal(0.8m, brands[1].CompletionRatio);
    }

    [Fact]
    public void Over_plan_and_under_plan_labels_point_in_the_right_direction()
    {
        Assert.Equal("Planın üzerinde", HourDeviation.StatusLabel(HourDeviation.Build(Guid.NewGuid(), "A", 1, 5, 8, 0)));
        Assert.Equal("Planın altında", HourDeviation.StatusLabel(HourDeviation.Build(Guid.NewGuid(), "A", 1, 5, 3, 0)));
    }

    [Fact]
    public void Zero_plan_never_divides_by_zero_and_says_what_really_exists()
    {
        var nothing = HourDeviation.Build(Guid.NewGuid(), "A", 0, 0, 0, 0);
        Assert.Null(nothing.CompletionRatio);
        Assert.Equal("Plan ve kayıt yok", HourDeviation.StatusLabel(nothing));

        var onlyActual = HourDeviation.Build(Guid.NewGuid(), "A", 1, 0, 3, 1);
        Assert.Null(onlyActual.CompletionRatio);
        Assert.Equal(3m, onlyActual.DifferenceHours);
        Assert.Equal(1m, onlyActual.VoidedHours);
        Assert.Equal("Yalnız gerçekleşen saat var", HourDeviation.StatusLabel(onlyActual));
    }

    [Fact]
    public void Repeated_task_ids_in_one_brand_are_counted_as_a_single_task()
    {
        var brand = Guid.NewGuid();
        var task = Guid.NewGuid();
        var rows = new List<HourDeviationInput>
        {
            new(brand, "A", task, "Haftalık görev", 4, 4, 0),
            new(brand, "A", task, "Haftalık görev", 4, 2, 0)
        };
        var group = Assert.Single(HourDeviation.ByBrand(rows));
        Assert.Equal(1, group.TaskCount);
        Assert.Equal(8m, group.PlannedHours);
        Assert.Equal(6m, group.ActualHours);
    }

    [Fact]
    public void Values_are_rounded_to_two_decimals_in_stable_shape()
    {
        var row = HourDeviation.Build(Guid.NewGuid(), "A", 1, 2.5m, 3.333m, 1.555m);
        Assert.Equal(2.50m, row.PlannedHours);
        Assert.Equal(3.33m, row.ActualHours);
        Assert.Equal(0.83m, row.DifferenceHours);
        Assert.Equal(1.56m, row.VoidedHours);
        Assert.Equal(1.3332m, row.CompletionRatio);
    }
}
