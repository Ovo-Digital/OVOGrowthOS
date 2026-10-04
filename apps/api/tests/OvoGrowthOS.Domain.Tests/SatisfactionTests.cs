using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class SatisfactionTests
{
    [Fact]
    public void Scores_have_readable_turkish_labels_and_invalid_values_are_named_as_such()
    {
        Assert.Equal("Çok kötü", SatisfactionRatings.ScoreLabel(1));
        Assert.Equal("Kötü", SatisfactionRatings.ScoreLabel(2));
        Assert.Equal("Orta", SatisfactionRatings.ScoreLabel(3));
        Assert.Equal("İyi", SatisfactionRatings.ScoreLabel(4));
        Assert.Equal("Çok iyi", SatisfactionRatings.ScoreLabel(5));
        Assert.Equal("Geçersiz puan", SatisfactionRatings.ScoreLabel(0));
        Assert.Equal("Geçersiz puan", SatisfactionRatings.ScoreLabel(6));
    }

    [Fact]
    public void Valid_input_has_no_error_and_empty_comment_is_allowed()
    {
        Assert.Null(SatisfactionRatings.ValidationError(5, null, 2026, 9));
        Assert.Null(SatisfactionRatings.ValidationError(1, "   ", 2026, 12));
        Assert.Null(SatisfactionRatings.ValidationError(3, new string('x', SatisfactionRatings.MaxCommentLength), 2026, 1));
    }

    [Fact]
    public void Score_period_and_comment_problems_each_return_a_turkish_message()
    {
        Assert.Equal("Puan 1 ile 5 arasında bir sayı olmalıdır.", SatisfactionRatings.ValidationError(0, null, 2026, 9));
        Assert.Equal("Puan 1 ile 5 arasında bir sayı olmalıdır.", SatisfactionRatings.ValidationError(6, null, 2026, 9));
        Assert.Equal("Geçerli bir yıl ve ay seçin.", SatisfactionRatings.ValidationError(4, null, 2019, 9));
        Assert.Equal("Geçerli bir yıl ve ay seçin.", SatisfactionRatings.ValidationError(4, null, 2101, 9));
        Assert.Equal("Geçerli bir yıl ve ay seçin.", SatisfactionRatings.ValidationError(4, null, 2026, 0));
        Assert.Equal("Geçerli bir yıl ve ay seçin.", SatisfactionRatings.ValidationError(4, null, 2026, 13));
        Assert.Equal("Yorum en fazla 1000 karakter olabilir.",
            SatisfactionRatings.ValidationError(4, new string('x', 1001), 2026, 9));
    }

    [Fact]
    public void Average_is_rounded_to_one_decimal_and_ignores_out_of_range_scores()
    {
        Assert.Equal(0m, SatisfactionRatings.Average([]));
        Assert.Equal(4.3m, SatisfactionRatings.Average([5, 4, 4]));
        Assert.Equal(3.5m, SatisfactionRatings.Average([3, 4]));
        Assert.Equal(2.0m, SatisfactionRatings.Average([1, 3]));
        Assert.Equal(5.0m, SatisfactionRatings.Average([5, 99, -3]));
    }
}
