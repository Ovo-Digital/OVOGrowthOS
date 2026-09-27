using System.Net.Http.Json;
using System.Text.Json;

namespace OvoGrowthOS.Api.Integration;

public sealed record StoreTokenResult(bool Accepted, string Token);

public interface IStoreTokenClient
{
    Task<StoreTokenResult> CreateTokenAsync(string storeUrl, string apiUser, string passwordBase64, CancellationToken ct);
}

public sealed class StoreTokenClient : IStoreTokenClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<StoreTokenResult> CreateTokenAsync(string storeUrl, string apiUser, string passwordBase64, CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(15));
            var url = $"{storeUrl.TrimEnd('/')}/Api/Token/Create";
            using var response = await Http.PostAsJsonAsync(url, new { Email = apiUser, Password = passwordBase64 }, linked.Token);
            if (!response.IsSuccessStatusCode) return new StoreTokenResult(false, "");
            var body = (await response.Content.ReadAsStringAsync(linked.Token)).Trim();
            var token = Normalize(body);
            return token is null ? new StoreTokenResult(false, "") : new StoreTokenResult(true, token);
        }
        catch
        {
            // Never expose store replies, credentials or network errors.
            return new StoreTokenResult(false, "");
        }
    }

    private static string? Normalize(string body)
    {
        if (body.Length < 20) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var value = doc.RootElement.ValueKind == JsonValueKind.String ? doc.RootElement.GetString() ?? "" : "";
            return IsJwt(value) ? value : null;
        }
        catch (JsonException)
        {
            return IsJwt(body) ? body : null;
        }
    }

    private static bool IsJwt(string value) => value.Split('.').Length == 3 && value.Length >= 20 && !value.Contains(' ') && !value.Contains('<') && !value.Contains('\n');
}
