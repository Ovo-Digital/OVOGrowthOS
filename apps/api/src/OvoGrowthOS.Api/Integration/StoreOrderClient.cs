using System.Globalization;
using System.Text.Json;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Integration;

public sealed record StoreOrderDraft(string SourceOrderId, string SourceStoreId, int OrderNumber, DateTimeOffset PlacedOnUtc,
    string Currency, decimal OrderTotal, decimal PaidAmount, decimal RefundedAmount, int OrderStatus, int PaymentStatus);

public sealed record StoreOrderFetchResult(bool Accepted, IReadOnlyList<StoreOrderDraft> Orders, bool Truncated);

public interface IStoreOrderClient
{
    Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, CancellationToken ct);
}

public sealed class StoreOrderClient : IStoreOrderClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const int PageSize = 100;
    private const int MaxPages = 50;
    private const string Select = "OrderGuid,OrderNumber,StoreId,CreatedOnUtc,OrderStatusId,PaymentStatusId,OrderTotal,PaidAmount,RefundedAmount,CustomerCurrencyCode,Deleted";

    private static readonly Dictionary<string, int> PaymentStatusByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pending"] = 10, ["Authorized"] = 20, ["PartiallyPaid"] = 25, ["Paid"] = 30, ["PartiallyRefunded"] = 35,
        ["PendingRefunded"] = 39, ["Refunded"] = 40, ["Voided"] = 50, ["PaymentCancelled"] = 60
    };

    public async Task<StoreOrderFetchResult> FetchAsync(string token, string storeUrl, StoreOrderPeriod period, CancellationToken ct)
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

    private static string BuildUrl(string storeUrl, StoreOrderPeriod period, int skip)
    {
        var start = period.UtcStart.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var end = period.UtcEnd.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var filter = Uri.EscapeDataString($"CreatedOnUtc ge {start} and CreatedOnUtc lt {end}");
        return $"{storeUrl.TrimEnd('/')}/odata/Order?$top={PageSize}&$skip={skip}&$count=true&$select={Select}&$filter={filter}";
    }

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
        if (value.ValueKind == JsonValueKind.String) return PaymentStatusByName.TryGetValue(value.GetString() ?? "", out result);
        return false;
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
