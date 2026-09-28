using System.Text.Json.Serialization;

namespace OvoGrowthOS.Domain;

public enum StorePlatform { GrandNode = 0, Shopify = 1 }

public sealed class BrandApiSettings
{
    public Guid BrandId { get; set; }
    public StorePlatform Platform { get; set; } = StorePlatform.GrandNode;
    public string StoreUrl { get; set; } = "";
    public string ApiUser { get; set; } = "";
    [JsonIgnore] public string ProtectedPassword { get; set; } = "";
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTestAt { get; set; }
}
