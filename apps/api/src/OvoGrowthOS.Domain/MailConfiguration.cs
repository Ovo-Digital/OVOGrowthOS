using System.Text.Json.Serialization;

namespace OvoGrowthOS.Domain;

public sealed class MailConfiguration
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 465;
    public bool Secure { get; set; } = true;
    public string User { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "OVO Digital";
    [JsonIgnore] public string ProtectedPassword { get; set; } = "";
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastTestAt { get; set; }
}
