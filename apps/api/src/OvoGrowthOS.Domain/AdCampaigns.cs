namespace OvoGrowthOS.Domain;

// Campaign level spend read from the connected ad platform. Stored per brand and period for
// review only; it never feeds performance, commission or budget calculations on its own.
public sealed class AdCampaignSpend
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public AdPlatform Platform { get; set; }
    public string CampaignName { get; set; } = "";
    public decimal Spend { get; set; }
    public string Currency { get; set; } = "";
    public DateTimeOffset ReadAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record AdCampaignResult(string CampaignName, decimal Spend, string Currency);

public sealed record AdCampaignBreakdown(string Platform, string CampaignName, decimal Spend, string Currency, DateTimeOffset ReadAt);

public sealed record AdCampaignTotal(string Platform, string Currency, decimal Spend);

public static class AdCampaigns
{
    public const int MaxCampaignNameLength = 300;

    // Null rows are dropped, names are trimmed and capped, spend is never negative.
    // Rows that share a name are added together instead of overwriting each other.
    public static IReadOnlyList<AdCampaignResult> Normalize(IEnumerable<AdCampaignResult>? rows, string fallbackCurrency)
    {
        if (rows is null) return Array.Empty<AdCampaignResult>();
        var currency = fallbackCurrency.Trim().ToUpperInvariant();
        if (currency.Length != 3) currency = "TRY";
        var merged = new Dictionary<string, (decimal Spend, string Currency)>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var row in rows)
        {
            var name = (row.CampaignName ?? "").Trim();
            if (name.Length == 0) continue;
            if (name.Length > MaxCampaignNameLength) name = name[..MaxCampaignNameLength];
            var rowCurrency = (row.Currency ?? "").Trim().ToUpperInvariant();
            if (rowCurrency.Length != 3) rowCurrency = currency;
            var spend = row.Spend < 0 ? 0m : row.Spend;
            if (merged.TryGetValue(name, out var current))
                merged[name] = (current.Spend + spend, current.Currency == rowCurrency ? current.Currency : currency);
            else { merged[name] = (spend, rowCurrency); order.Add(name); }
        }
        return order.Select(name => new AdCampaignResult(name, merged[name].Spend, merged[name].Currency)).ToList();
    }

    public static IReadOnlyList<AdCampaignBreakdown> Group(IReadOnlyCollection<AdCampaignSpend> rows)
        => rows.OrderByDescending(x => x.Spend).ThenBy(x => x.CampaignName, StringComparer.CurrentCulture)
            .Select(x => new AdCampaignBreakdown(x.Platform.ToString(), x.CampaignName, x.Spend, x.Currency, x.ReadAt)).ToList();

    public static IReadOnlyList<AdCampaignTotal> Totals(IReadOnlyCollection<AdCampaignSpend> rows)
        => rows.GroupBy(x => new { x.Platform, x.Currency })
            .OrderByDescending(x => x.Sum(y => y.Spend))
            .Select(x => new AdCampaignTotal(x.Key.Platform.ToString(), x.Key.Currency, x.Sum(y => y.Spend)))
            .ToList();
}
