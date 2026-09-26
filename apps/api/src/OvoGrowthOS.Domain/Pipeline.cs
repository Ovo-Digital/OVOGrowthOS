namespace OvoGrowthOS.Domain;

public enum LeadSource { Unspecified, Referral, InboundWebsite, OutboundContact, Event, Partner, Other }

public sealed class BrandStageHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public LeadStage Stage { get; set; }
    public bool EntryKnown { get; set; }
    public DateTimeOffset EnteredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExitedAt { get; set; }
    public string EnteredBy { get; set; } = "";
    public string ExitedBy { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed record StageWait(LeadStage Stage, int Open, int Known, int Unknown, decimal? AverageDays, int LongestDays);

public static class Pipeline
{
    public const int MaxReasonLength = 1000;
    public const int MaxSourceNoteLength = 200;

    public static string SourceLabel(LeadSource source) => source switch
    {
        LeadSource.Referral => "Referans / tavsiye",
        LeadSource.InboundWebsite => "Web sitesinden gelen talep",
        LeadSource.OutboundContact => "Dışarıdan yapılan ilk temas",
        LeadSource.Event => "Etkinlik / fuar",
        LeadSource.Partner => "İş ortağı yönlendirmesi",
        LeadSource.Other => "Diğer",
        _ => "Belirtilmedi"
    };

    public static int? StageDays(BrandStageHistory row, DateTimeOffset now)
    {
        if (!row.EntryKnown) return null;
        var days = (int)Math.Floor(((row.ExitedAt ?? now) - row.EnteredAt).TotalDays);
        return days < 0 ? 0 : days;
    }

    public static string? LossError(bool alreadyLost, bool hasDeal, string? reason)
    {
        if (alreadyLost) return "Bu marka için kayıp zaten kayıtlı.";
        if (hasDeal) return "Anlaşması olan marka kayıp olarak işaretlenemez.";
        if (string.IsNullOrWhiteSpace(reason)) return "Kayıp nedenini yazın.";
        if (reason.Trim().Length > MaxReasonLength) return $"Kayıp nedeni en fazla {MaxReasonLength} karakter olabilir.";
        return null;
    }

    public static string? CancelLossError(bool alreadyLost) =>
        alreadyLost ? null : "Kayıp kaydı bulunamadı.";

    public static decimal? ConversionRate(int reached, int converted) =>
        reached <= 0 ? null : decimal.Round(converted * 100m / reached, 1);

    public static StageWait Wait(IReadOnlyCollection<(LeadStage Stage, int? Days)> current, LeadStage stage)
    {
        var inStage = current.Where(x => x.Stage == stage).Select(x => x.Days).ToList();
        var known = inStage.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return new StageWait(stage, inStage.Count, known.Count, inStage.Count - known.Count,
            known.Count == 0 ? null : decimal.Round((decimal)known.Average(), 1),
            known.Count == 0 ? 0 : known.Max());
    }
}
