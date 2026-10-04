namespace OvoGrowthOS.Domain;

// What a brand contact reports for a period. It is an input signal for the OVO staff only: nothing here
// is ever written into a monthly performance, an invoice or a settlement by itself.
public sealed class PortalPeriodSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal? GrossSales { get; set; }
    public decimal? Refunds { get; set; }
    public decimal? MetaSpend { get; set; }
    public decimal? GoogleSpend { get; set; }
    public string Note { get; set; } = "";
    public Guid SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Revision { get; set; } = 1;
}

public static class PortalSubmissions
{
    public const int MaxNoteLength = 1000;
    // Upper guard shared with the performance form; larger amounts must not enter the prefill at all.
    public const decimal MaxAmount = 1_000_000_000_000m;

    public static bool ValidPeriod(int year, int month) => year is >= 2020 and <= 2100 && month is >= 1 and <= 12;
    public static bool ValidAmount(decimal? value) => value is null or (>= 0 and <= MaxAmount);
    public static bool HasContent(PortalPeriodSubmission s) =>
        s.GrossSales.HasValue || s.Refunds.HasValue || s.MetaSpend.HasValue || s.GoogleSpend.HasValue || s.Note.Length > 0;
}
