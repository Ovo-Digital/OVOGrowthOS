using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Integration;

public sealed record AdConnection(AdPlatform Platform, string AccountId, string ClientId, string Secret, string ClientSecret, string DeveloperToken);

public interface IAdSpendClient
{
    Task<bool> TestAsync(AdConnection connection, CancellationToken ct);
    Task<AdSpendResult?> FetchAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct);
    Task<IReadOnlyList<AdCampaignResult>?> FetchCampaignsAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct);
}

public sealed class AdSpendClient : IAdSpendClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string MetaGraphVersion = "v25.0";
    private const string GoogleAdsVersion = "v25";

    public async Task<bool> TestAsync(AdConnection connection, CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(20));
            return connection.Platform == AdPlatform.Meta
                ? await TestMetaAsync(connection, linked.Token)
                : await TestGoogleAsync(connection, linked.Token);
        }
        catch
        {
            // Never expose provider replies, credentials or network errors.
            return false;
        }
    }

    public async Task<AdSpendResult?> FetchAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(30));
            return connection.Platform == AdPlatform.Meta
                ? await FetchMetaAsync(connection, period, linked.Token)
                : await FetchGoogleAsync(connection, period, linked.Token);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<AdCampaignResult>?> FetchCampaignsAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(30));
            return connection.Platform == AdPlatform.Meta
                ? await FetchMetaCampaignsAsync(connection, period, linked.Token)
                : await FetchGoogleCampaignsAsync(connection, period, linked.Token);
        }
        catch
        {
            return null;
        }
    }

    private static string MetaAccount(AdConnection connection)
    {
        var account = connection.AccountId.Trim();
        return account.StartsWith("act_", StringComparison.OrdinalIgnoreCase) ? account : $"act_{account}";
    }

    private static async Task<bool> TestMetaAsync(AdConnection connection, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.facebook.com/{MetaGraphVersion}/{MetaAccount(connection)}?fields=name,account_currency");
        request.Headers.Authorization = new("Bearer", connection.Secret);
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        return doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("id", out _)
            && doc.RootElement.TryGetProperty("account_currency", out _);
    }

    private static async Task<AdSpendResult?> FetchMetaAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
    {
        var since = new DateOnly(period.Year, period.Month, 1);
        var until = since.AddMonths(1).AddDays(-1);
        var range = Uri.EscapeDataString($"{{\"since\":\"{since:yyyy-MM-dd}\",\"until\":\"{until:yyyy-MM-dd}\"}}");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.facebook.com/{MetaGraphVersion}/{MetaAccount(connection)}/insights?fields=spend,account_currency&level=account&time_range={range}");
        request.Headers.Authorization = new("Bearer", connection.Secret);
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var parsed = ParseMetaInsights(doc.RootElement);
        if (parsed is null) return null;
        var (amount, currency) = parsed.Value;
        if (currency.Length == 0)
        {
            using var accountRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://graph.facebook.com/{MetaGraphVersion}/{MetaAccount(connection)}?fields=account_currency");
            accountRequest.Headers.Authorization = new("Bearer", connection.Secret);
            using var accountResponse = await Http.SendAsync(accountRequest, ct);
            if (!accountResponse.IsSuccessStatusCode) return null;
            using var accountDoc = JsonDocument.Parse(await accountResponse.Content.ReadAsStreamAsync(ct));
            if (accountDoc.RootElement.ValueKind != JsonValueKind.Object || !accountDoc.RootElement.TryGetProperty("account_currency", out var currencyValue))
                return null;
            currency = currencyValue.GetString() ?? "";
            if (currency.Length != 3) return null;
        }
        return new AdSpendResult(amount, currency.ToUpperInvariant(), "Meta reklam raporu");
    }

    private static async Task<IReadOnlyList<AdCampaignResult>?> FetchMetaCampaignsAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
    {
        var since = new DateOnly(period.Year, period.Month, 1);
        var until = since.AddMonths(1).AddDays(-1);
        var range = Uri.EscapeDataString($"{{\"since\":\"{since:yyyy-MM-dd}\",\"until\":\"{until:yyyy-MM-dd}\"}}");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.facebook.com/{MetaGraphVersion}/{MetaAccount(connection)}/insights?fields=campaign_name,spend,account_currency&level=campaign&time_range={range}&limit=1000");
        request.Headers.Authorization = new("Bearer", connection.Secret);
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var rows = ParseMetaCampaigns(doc.RootElement);
        if (rows is null) return null;
        var currency = rows.Select(x => x.Currency).FirstOrDefault(x => x.Length == 3) ?? "";
        if (currency.Length == 0)
        {
            using var accountRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://graph.facebook.com/{MetaGraphVersion}/{MetaAccount(connection)}?fields=account_currency");
            accountRequest.Headers.Authorization = new("Bearer", connection.Secret);
            using var accountResponse = await Http.SendAsync(accountRequest, ct);
            if (!accountResponse.IsSuccessStatusCode) return null;
            using var accountDoc = JsonDocument.Parse(await accountResponse.Content.ReadAsStreamAsync(ct));
            if (accountDoc.RootElement.ValueKind != JsonValueKind.Object || !accountDoc.RootElement.TryGetProperty("account_currency", out var currencyValue))
                return null;
            currency = currencyValue.GetString() ?? "";
        }
        return AdCampaigns.Normalize(rows, currency);
    }

    internal static IReadOnlyList<AdCampaignResult>? ParseMetaCampaigns(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;
        var rows = new List<AdCampaignResult>();
        foreach (var row in data.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) return null;
            var name = row.TryGetProperty("campaign_name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
                ? nameValue.GetString() ?? "" : "";
            var amount = 0m;
            if (row.TryGetProperty("spend", out var spendValue))
            {
                var text = spendValue.ValueKind == JsonValueKind.String ? spendValue.GetString() ?? "" : spendValue.GetRawText();
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return null;
                amount = value;
            }
            var currency = row.TryGetProperty("account_currency", out var currencyValue) && currencyValue.ValueKind == JsonValueKind.String
                ? currencyValue.GetString() ?? "" : "";
            rows.Add(new AdCampaignResult(name, amount, currency));
        }
        return rows;
    }

    internal static (decimal Amount, string Currency)? ParseMetaInsights(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;
        var amount = 0m;
        var currency = "";
        foreach (var row in data.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) return null;
            if (row.TryGetProperty("spend", out var spend))
            {
                var text = spend.ValueKind == JsonValueKind.String ? spend.GetString() ?? "" : spend.GetRawText();
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return null;
                amount += value;
            }
            if (currency.Length == 0 && row.TryGetProperty("account_currency", out var currencyValue) && currencyValue.ValueKind == JsonValueKind.String)
                currency = currencyValue.GetString() ?? "";
        }
        return (amount, currency);
    }

    private static async Task<bool> TestGoogleAsync(AdConnection connection, CancellationToken ct)
    {
        var token = await GoogleTokenAsync(connection, ct);
        if (token is null) return false;
        using var request = GoogleSearchRequest(connection, token, "SELECT customer.id, customer.currency_code FROM customer");
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0;
    }

    private static async Task<AdSpendResult?> FetchGoogleAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
    {
        var token = await GoogleTokenAsync(connection, ct);
        if (token is null) return null;
        var first = new DateOnly(period.Year, period.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var query = $"SELECT metrics.cost_micros, customer.currency_code FROM customer WHERE segments.date BETWEEN '{first:yyyy-MM-dd}' AND '{last:yyyy-MM-dd}'";
        using var request = GoogleSearchRequest(connection, token, query);
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var parsed = ParseGoogleStream(doc.RootElement);
        if (parsed is null) return null;
        var (amount, currency) = parsed.Value;
        if (currency.Length != 3) return null;
        return new AdSpendResult(amount, currency.ToUpperInvariant(), "Google Ads raporu");
    }

    private static async Task<IReadOnlyList<AdCampaignResult>?> FetchGoogleCampaignsAsync(AdConnection connection, StoreOrderPeriod period, CancellationToken ct)
    {
        var token = await GoogleTokenAsync(connection, ct);
        if (token is null) return null;
        var first = new DateOnly(period.Year, period.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var query = $"SELECT campaign.name, metrics.cost_micros, customer.currency_code FROM campaign WHERE segments.date BETWEEN '{first:yyyy-MM-dd}' AND '{last:yyyy-MM-dd}'";
        using var request = GoogleSearchRequest(connection, token, query);
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var rows = ParseGoogleCampaigns(doc.RootElement);
        return rows is null ? null : AdCampaigns.Normalize(rows, rows.Select(x => x.Currency).FirstOrDefault(x => x.Length == 3) ?? "");
    }

    internal static IReadOnlyList<AdCampaignResult>? ParseGoogleCampaigns(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return null;
        var rows = new List<AdCampaignResult>();
        foreach (var segment in root.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Object || !segment.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var row in results.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) return null;
                var name = row.TryGetProperty("campaign", out var campaign) && campaign.ValueKind == JsonValueKind.Object
                    && campaign.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
                    ? nameValue.GetString() ?? "" : "";
                var amount = 0m;
                if (row.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Object
                    && metrics.TryGetProperty("costMicros", out var cost))
                {
                    var text = cost.ValueKind == JsonValueKind.String ? cost.GetString() ?? "" : cost.GetRawText();
                    if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var micros)) return null;
                    amount = micros / 1_000_000m;
                }
                var currency = row.TryGetProperty("customer", out var customer) && customer.ValueKind == JsonValueKind.Object
                    && customer.TryGetProperty("currencyCode", out var code) && code.ValueKind == JsonValueKind.String
                    ? code.GetString() ?? "" : "";
                rows.Add(new AdCampaignResult(name, amount, currency));
            }
        }
        return rows;
    }

    internal static (decimal Amount, string Currency)? ParseGoogleStream(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return null;
        var amount = 0m;
        var currency = "";
        foreach (var segment in root.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Object || !segment.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var row in results.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) return null;
                if (row.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Object
                    && metrics.TryGetProperty("costMicros", out var cost))
                {
                    var text = cost.ValueKind == JsonValueKind.String ? cost.GetString() ?? "" : cost.GetRawText();
                    if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var micros)) return null;
                    amount += micros / 1_000_000m;
                }
                if (currency.Length == 0 && row.TryGetProperty("customer", out var customer) && customer.ValueKind == JsonValueKind.Object
                    && customer.TryGetProperty("currencyCode", out var code) && code.ValueKind == JsonValueKind.String)
                    currency = code.GetString() ?? "";
            }
        }
        return (amount, currency);
    }

    private static async Task<string?> GoogleTokenAsync(AdConnection connection, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = connection.ClientId,
                ["client_secret"] = connection.ClientSecret,
                ["refresh_token"] = connection.Secret,
                ["grant_type"] = "refresh_token"
            })
        };
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("access_token", out var token)) return null;
        var value = token.GetString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static HttpRequestMessage GoogleSearchRequest(AdConnection connection, string token, string query)
    {
        var customer = new string(connection.AccountId.Where(char.IsDigit).ToArray());
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://googleads.googleapis.com/{GoogleAdsVersion}/customers/{customer}/googleAds:searchStream")
        {
            Content = JsonContent.Create(new { query })
        };
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.Add("developer-token", connection.DeveloperToken);
        return request;
    }
}
