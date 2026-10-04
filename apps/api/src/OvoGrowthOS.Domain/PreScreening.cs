namespace OvoGrowthOS.Domain;

public sealed record PreScreenIssue(string Code, string Label);
public sealed record PreScreenInput(Guid BrandId, string BrandName, string ContactName, string ContactEmail,
    string Website, string Industry, LeadSource SourceChannel, string NextStep, Guid? OwnerId,
    bool StageEntryKnown, int? StageDays, DateOnly? LastContactOn);
public sealed record PreScreenItem(Guid BrandId, string BrandName, string State, int IssueCount,
    IReadOnlyList<PreScreenIssue> Issues, int? StageDays);
public sealed record PreScreenReport(int Total, int Ready, int Attention,
    IReadOnlyList<PreScreenItem> Items, IReadOnlyList<string> Notes);

// Quick, deterministic pre-screening of open leads. It only reports which pieces of
// information are missing or overdue; it never records a decision or changes a stage.
public static class PreScreening
{
    public const int StaleDays = 30;

    public static PreScreenItem Evaluate(PreScreenInput input)
    {
        var issues = new List<PreScreenIssue>();
        if (input.ContactName.Trim().Length == 0 && input.ContactEmail.Trim().Length == 0)
            issues.Add(new PreScreenIssue("contact", "Yetkili adı veya e-postası yazılmamış"));
        if (input.Website.Trim().Length == 0)
            issues.Add(new PreScreenIssue("website", "İnternet sitesi yazılmamış"));
        if (input.Industry.Trim().Length == 0)
            issues.Add(new PreScreenIssue("industry", "Sektör bilgisi girilmemiş"));
        if (input.SourceChannel == LeadSource.Unspecified)
            issues.Add(new PreScreenIssue("source", "Kaynak kanalı belirtilmemiş"));
        if (input.NextStep.Trim().Length == 0)
            issues.Add(new PreScreenIssue("nextStep", "Sıradaki adım yazılmamış"));
        if (input.OwnerId is null)
            issues.Add(new PreScreenIssue("owner", "Sorumlu atanmamış"));
        if (input.LastContactOn is null)
            issues.Add(new PreScreenIssue("noContact", "Henüz görüşme kaydı yok"));
        if (input.StageEntryKnown && input.StageDays is > StaleDays)
            issues.Add(new PreScreenIssue("stale", $"Aynı aşamada {input.StageDays} günden uzun süredir bekliyor"));
        return new PreScreenItem(input.BrandId, input.BrandName,
            issues.Count == 0 ? "ready" : "attention", issues.Count, issues,
            input.StageEntryKnown ? input.StageDays : null);
    }

    public static PreScreenReport Build(IReadOnlyCollection<PreScreenInput> inputs)
    {
        var items = inputs.Select(Evaluate)
            .OrderBy(x => x.State == "attention" ? 0 : 1)
            .ThenByDescending(x => x.IssueCount)
            .ThenBy(x => x.BrandName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new PreScreenReport(items.Count,
            items.Count(x => x.State == "ready"), items.Count(x => x.State == "attention"),
            items,
            [
                "Ön eleme yalnız eksik bilgi ve bekleme süresini söyler; marka uygunluğu, değerlendirme kararı veya aşama değişikliği vermez.",
                $"'Hazır' etiketi bilgilerin tam olduğunu gösterir; anlaşma veya yatırım tavsiyesi değildir.",
                $"Bekleme uyarısı {StaleDays} günden sonra çıkar; ölçüm başlangıcından önce girilen aşamalar sayılmaz."
            ]);
    }
}
