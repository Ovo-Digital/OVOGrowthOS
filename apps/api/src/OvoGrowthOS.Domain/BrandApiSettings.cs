using System.Text.Json.Serialization;

namespace OvoGrowthOS.Domain;

public sealed class BrandApiSettings
{
    public Guid BrandId { get; set; }
    public string StoreUrl { get; set; } = "";
    public string ApiUser { get; set; } = "";
    [JsonIgnore] public string ProtectedPassword { get; set; } = "";
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTestAt { get; set; }
}
