using System.Globalization;
using System.Text.Json;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Integration;

public sealed record StoreOrderDraft(string SourceOrderId, string SourceStoreId, int OrderNumber, DateTimeOffset PlacedOnUtc,
    string Currency, decimal OrderTotal, decimal PaidAmount, decimal RefundedAmount, int OrderStatus, int PaymentStatus);

public sealed record StoreOrderFetchResult(bool Accepted, IReadOnlyList<StoreOrderDraft> Orders, bool Truncated);

public interface IStoreOrderClient
{
    Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, StorePlatform platform, CancellationToken ct);
    Task<bool> TestAsync(StorePlatform platform, string token, string storeUrl, CancellationToken ct);
}

public sealed class StoreOrderClient : IStoreOrderClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const int PageSize = 100;
    private const int MaxPages = 50;
    private const string Select = "OrderGuid,OrderNumber,StoreId,CreatedOnUtc,OrderStatusId,PaymentStatusId,OrderTotal,PaidAmount,RefundedAmount,CustomerCurrencyCode,Deleted";

    // Shopify Admin REST API current stable version (docs: Shopify Admin API, "Current version").
    private const string ShopifyApiVersion = "2026-07";
    private const int ShopifyPageSize = 250;
    private const int ShopifyMaxPages = 50;
    private const string ShopifyFields = "id,order_number,name,created_at,cancelled,cancelled_at,currency,total_price,total_refunded,financial_status";

    private static readonly Dictionary<string, int> PaymentStatusByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pending"] = 10, ["Authorized"] = 20, ["PartiallyPaid"] = 25, ["Paid"] = 30, ["PartiallyRefunded"] = 35,
        ["PendingRefunded"] = 39, ["Refunded"] = 40, ["Voided"] = 50, ["PaymentCancelled"] = 60
    };

    private static readonly Dictionary<string, int> ShopifyPaymentByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pending"] = 10, ["authorized"] = 20, ["partially_paid"] = 25, ["paid"] = 30, ["partially_refunded"] = 35,
        ["refunded"] = 40, ["voided"] = 50, ["expired"] = 60
    };

    public Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, StorePlatform platform, CancellationToken ct) =>
        platform == StorePlatform.Shopify ? FetchShopifyAsync(token, storeUrl, period, ct) : FetchGrandNodeAsync(token, storeUrl, period, ct);

    public Task<bool> TestAsync(StorePlatform platform, string token, string storeUrl, CancellationToken ct) =>
        platform == StorePlatform.Shopify ? TestShopifyAsync(token, storeUrl, ct) : TestGrandNodeAsync(token, storeUrl, ct);

    private static async Task<StoreOrderFetchResult> FetchGrandNodeAsync(string token, string storeUrl, StoreOrderPeriod period, CancellationToken ct)
    {
        var orders = new List<StoreOrderDraft>();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(30));
            for (var page = 0; page < MaxPages; page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(storeUrl, period, page * PageSize));
                request.Headers.Authorization = new("Bearer", token);
                using var response = await Http.SendAsync(request, linked.Token);
                if (!response.IsSuccessStatusCode) return new StoreOrderFetchResult(false, [], false);
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(linked.Token));
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                    return new StoreOrderFetchResult(false, [], false);
                foreach (var item in value.EnumerateArray())
                {
                    if (!TryParseOrder(item, out var order, out var deleted)) return new StoreOrderFetchResult(false, [], false);
                    if (deleted) continue;
                    orders.Add(order);
                }
                if (value.GetArrayLength() < PageSize) return new StoreOrderFetchResult(true, orders, false);
            }
            return new StoreOrderFetchResult(true, orders, true);
        }
        catch
        {
            // Never expose store replies, credentials or network errors.
            return new StoreOrderFetchResult(false, [], false);
        }
    }

    private static async Task<StoreOrderFetchResult> FetchShopifyAsync(string token, string storeUrl, StoreOrderPeriod period, CancellationToken ct)
    {
        var orders = new List<StoreOrderDraft>();
        var host = StoreHost(storeUrl);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(30));
            var url = ShopifyListUrl(storeUrl, period);
            for (var page = 0; page < ShopifyMaxPages && url is not null; page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-Shopify-Access-Token", token);
                using var response = await Http.SendAsync(request, linked.Token);
                if (!response.IsSuccessStatusCode) return new StoreOrderFetchResult(false, [], false);
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(linked.Token));
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("orders", out var value) || value.ValueKind != JsonValueKind.Array)
                    return new StoreOrderFetchResult(false, [], false);
                foreach (var item in value.EnumerateArray())
                {
                    if (!TryParseShopifyOrder(item, host, out var order)) return new StoreOrderFetchResult(false, [], false);
                    orders.Add(order);
                }
                url = NextLink(response);
            }
            return new StoreOrderFetchResult(true, orders, url is not null);
        }
        catch
        {
            return new StoreOrderFetchResult(false, [], false);
        }
    }

    private static async Task<bool> TestShopifyAsync(string token, string storeUrl, CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{storeUrl.TrimEnd('/')}/admin/api/{ShopifyApiVersion}/orders/count.json?status=any");
            request.Headers.Add("X-Shopify-Access-Token", token);
            using var response = await Http.SendAsync(request, linked.Token);
            if (!response.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(linked.Token));
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("count", out _);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestGrandNodeAsync(string token, string storeUrl, CancellationToken ct)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{storeUrl.TrimEnd('/')}/odata/Order?$top=1&$count=true");
            request.Headers.Authorization = new("Bearer", token);
            using var response = await Http.SendAsync(request, linked.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildUrl(string storeUrl, StoreOrderPeriod period, int skip)
    {
        var start = period.UtcStart.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var end = period.UtcEnd.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var filter = Uri.EscapeDataString($"CreatedOnUtc ge {start} and CreatedOnUtc lt {end}");
        return $"{storeUrl.TrimEnd('/')}/odata/Order?$top={PageSize}&$skip={skip}&$count=true&$select={Select}&$filter={filter}";
    }

    private static string ShopifyListUrl(string storeUrl, StoreOrderPeriod period)
    {
        var start = period.UtcStart.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var end = period.UtcEnd.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return $"{storeUrl.TrimEnd('/')}/admin/api/{ShopifyApiVersion}/orders.json?status=any&limit={ShopifyPageSize}" +
            $"&created_at_min={start}&created_at_max={end}&fields={ShopifyFields}";
    }

    private static string StoreHost(string storeUrl) =>
        Uri.TryCreate(storeUrl, UriKind.Absolute, out var uri) ? uri.Host : "";

    private static string? NextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values)) return null;
        foreach (var header in values)
        {
            foreach (var part in header.Split(','))
            {
                var segments = part.Split(';');
                if (segments.Length < 2) continue;
                if (!segments.Skip(1).Any(x => x.Trim() == "rel=\"next\"")) continue;
                var url = segments[0].Trim().Trim('<', '>');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) return uri.ToString();
            }
        }
        return null;
    }

    internal static bool TryParseShopifyOrder(JsonElement item, string storeHost, out StoreOrderDraft draft)
    {
        draft = null!;
        if (item.ValueKind != JsonValueKind.Object) return false;
        if (!TryGetString(item, "id", out var id) || id.Length == 0 || id.Length > 64) return false;
        if (!TryGetInt(item, "order_number", out var number))
        {
            if (!TryGetString(item, "name", out var name) || name.Length < 2 || name[0] != '#' || !int.TryParse(name.AsSpan(1), out number)) return false;
        }
        if (!TryGetInstant(item, "created_at", out var placedOn)) return false;
        if (!TryGetString(item, "currency", out var currency)) return false;
        currency = currency.Trim().ToUpperInvariant();
        if (currency.Length != 3) return false;
        if (!TryGetDecimal(item, "total_price", out var total)) return false;
        TryGetDecimal(item, "total_refunded", out var refunded);
        if (total < 0 || refunded < 0) return false;
        if (!TryGetString(item, "financial_status", out var financial) || !ShopifyPaymentByName.TryGetValue(financial.Trim(), out var payment)) return false;
        TryGetBool(item, "cancelled", out var cancelledFlag);
        TryGetInstant(item, "cancelled_at", out _);
        var cancelled = cancelledFlag || HasInstant(item, "cancelled_at");
        var paid = payment is 30 or 35 or 40 ? total : 0m;
        var orderStatus = cancelled ? StoreOrderStatus.Cancelled
            : payment is 10 or 20 ? StoreOrderStatus.Pending : StoreOrderStatus.Complete;
        draft = new StoreOrderDraft(id, storeHost, number, placedOn, currency, total, paid, refunded, orderStatus, payment);
        return true;
    }

    private static bool HasInstant(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _);

    private static bool TryParseOrder(JsonElement item, out StoreOrderDraft draft, out bool deleted)
    {
        draft = null!; deleted = false;
        if (item.ValueKind != JsonValueKind.Object) return false;
        if (!TryGetString(item, "OrderGuid", out var guid) || guid.Length == 0) return false;
        if (!TryGetInt(item, "OrderNumber", out var number)) return false;
        TryGetString(item, "StoreId", out var storeId);
        if (!TryGetInstant(item, "CreatedOnUtc", out var placedOn)) return false;
        if (!TryGetInt(item, "OrderStatusId", out var orderStatus)) return false;
        if (!TryGetPaymentStatus(item, out var paymentStatus)) return false;
        if (!TryGetDecimal(item, "OrderTotal", out var orderTotal) ||
            !TryGetDecimal(item, "PaidAmount", out var paid) ||
            !TryGetDecimal(item, "RefundedAmount", out var refunded)) return false;
        TryGetString(item, "CustomerCurrencyCode", out var currency);
        deleted = TryGetBool(item, "Deleted", out var d) && d;
        draft = new StoreOrderDraft(guid, storeId, number, placedOn, currency.Trim().ToUpperInvariant(), orderTotal, paid, refunded, orderStatus, paymentStatus);
        return true;
    }

    private static bool TryGetString(JsonElement item, string name, out string result)
    {
        result = "";
        if (!item.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind == JsonValueKind.String) { result = value.GetString() ?? ""; return true; }
        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) { result = value.GetRawText(); return true; }
        return false;
    }

    private static bool TryGetInt(JsonElement item, string name, out int result)
    {
        result = 0;
        if (!item.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result)) return true;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out result);
    }

    private static bool TryGetPaymentStatus(JsonElement item, out int result)
    {
        result = 0;
        if (!item.TryGetProperty("PaymentStatusId", out var value)) return false;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result)) return true;
        return value.ValueKind == JsonValueKind.String && PaymentStatusByName.TryGetValue(value.GetString() ?? "", out result);
    }

    private static bool TryGetDecimal(JsonElement item, string name, out decimal result)
    {
        result = 0m;
        if (!item.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out result)) return true;
        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryGetInstant(JsonElement item, string name, out DateTimeOffset result)
    {
        result = default;
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return false;
        return DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out result);
    }

    private static bool TryGetBool(JsonElement item, string name, out bool result)
    {
        result = false;
        if (!item.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind == JsonValueKind.True) { result = true; return true; }
        if (value.ValueKind == JsonValueKind.False) return true;
        return false;
    }
}
