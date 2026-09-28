using System.Text.Json.Serialization;

namespace OvoGrowthOS.Domain;

public enum AdPlatform { Meta = 0, Google = 1 }

public sealed class BrandAdSettings
{
    public AdPlatform Platform { get; set; }
    public Guid BrandId { get; set; }
    public string AccountId { get; set; } = "";
    public string ClientId { get; set; } = "";
    [JsonIgnore] public string ProtectedSecret { get; set; } = "";
    [JsonIgnore] public string ProtectedClientSecret { get; set; } = "";
    [JsonIgnore] public string ProtectedDeveloperToken { get; set; } = "";
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTestAt { get; set; }
}

public sealed record AdSpendResult(decimal Amount, string Currency, string Source);

public static class AdSettings
{
    public const int MaxValueLength = 400;
    public const int MaxSecretLength = 2000;

    public static string PlatformLabel(AdPlatform platform) => platform switch
    {
        AdPlatform.Meta => "Meta (Facebook / Instagram)",
        _ => "Google Ads"
    };

    public static string? Validate(AdPlatform platform, string? accountId, string? clientId)
    {
        var account = accountId?.Trim() ?? "";
        var client = clientId?.Trim() ?? "";
        if (account.Length is 0 or > 64 || !account.All(c => char.IsLetterOrDigit(c) || c == '-'))
            return "Hesap numarası 64 karakteri geçmeyen, yalnız harf, rakam ve tire içerebilen bir değer olmalıdır.";
        if (platform == AdPlatform.Google)
        {
            if (client.Length is 0 or > 320)
                return "Google için istemci kimliği (client id) gereklidir ve en fazla 320 karakter olabilir.";
        }
        return null;
    }
}
