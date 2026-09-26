using System.Text.RegularExpressions;

namespace OvoGrowthOS.Domain;

public sealed class BrandMailPolicy
{
    public Guid BrandId { get; set; }
    public bool ReportEmailEnabled { get; set; }
    public bool ScheduledReportEnabled { get; set; }
    public int ScheduledSendDay { get; set; } = 5;
    public int ScheduledSendHour { get; set; } = 9;
    public string SubjectTemplate { get; set; } = "{marka} · {donem} raporunuz hazır";
    public string BodyTemplate { get; set; } = "{marka} için {donem} raporu paylaşıldı.\n\nRaporu hesabınızla giriş yaparak inceleyin: {baglanti}";
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ReportMailTemplate
{
    private static readonly Regex Token = new(@"\{(marka|donem|baglanti)\}", RegexOptions.CultureInvariant);
    public static bool Valid(string? subject, string? body) =>
        ValidText(subject, 180, false) && ValidText(body, 2000, true)
        && !subject!.Contains("{baglanti}") && body!.Contains("{baglanti}");

    private static bool ValidText(string? value, int max, bool multiline)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Contains('<') || value.Contains('>')
            || value.Contains("://") || value.Contains("www.", StringComparison.OrdinalIgnoreCase)
            || value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n'))) return false;
        var rest = Token.Replace(value, "");
        return !rest.Contains('{') && !rest.Contains('}');
    }

    public static string Render(string template, string brand, string period, string link) => Token.Replace(template, m => m.Groups[1].Value switch
    {
        "marka" => string.Concat(brand.Where(c => !char.IsControl(c))),
        "donem" => period,
        _ => link
    });
}
