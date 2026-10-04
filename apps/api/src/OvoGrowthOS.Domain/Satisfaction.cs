namespace OvoGrowthOS.Domain;

public sealed class SatisfactionRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Score { get; set; }
    public string Comment { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SatisfactionRatings
{
    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int MaxCommentLength = 1000;

    public static string ScoreLabel(int score) => score switch
    {
        1 => "Çok kötü", 2 => "Kötü", 3 => "Orta", 4 => "İyi", 5 => "Çok iyi",
        _ => "Geçersiz puan"
    };

    public static string? ValidationError(int score, string? comment, int year, int month)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12)
            return "Geçerli bir yıl ve ay seçin.";
        if (score is < MinScore or > MaxScore)
            return $"Puan {MinScore} ile {MaxScore} arasında bir sayı olmalıdır.";
        if ((comment?.Trim().Length ?? 0) > MaxCommentLength)
            return $"Yorum en fazla {MaxCommentLength} karakter olabilir.";
        return null;
    }

    public static decimal Average(IEnumerable<int> scores)
    {
        var list = scores.Where(x => x >= MinScore && x <= MaxScore).ToList();
        return list.Count == 0 ? 0 : Math.Round((decimal)list.Average(), 1, MidpointRounding.AwayFromZero);
    }
}
