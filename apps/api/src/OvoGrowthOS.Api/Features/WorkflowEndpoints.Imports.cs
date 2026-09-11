using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Validation;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public sealed record ImportRowPreview(int Line, string Brand, string Deal, string Period, string Currency,
    decimal? NetRevenue, decimal? OvoFee, decimal? AdSpend, decimal? BrandContribution, string[] Errors);
public sealed record ImportTotal(string Currency, decimal NetRevenue, decimal OvoFee, decimal AdSpend, decimal BrandContribution);
internal sealed record ImportApproval(string Actor, string Fingerprint, DateTimeOffset ExpiresAt);

public static partial class WorkflowEndpoints
{
    private static void MapPerformanceImports(WebApplication app)
    {
        var group = app.MapGroup("/api/performance-imports").RequireAuthorization("OperationsWrite");
        group.MapGet("/fields", () => PerformanceImportFile.Fields);
        group.MapPost("/inspect", (ImportFileRequest request) =>
        {
            try
            {
                var table = PerformanceImportFile.Read(request);
                var mapping = PerformanceImportFile.Fields.ToDictionary(x => x.Key, x => Array.FindIndex(table.Headers, h => string.Equals(h, x.Label, StringComparison.OrdinalIgnoreCase)));
                return Results.Ok(new { table.Headers, sample = table.Rows.Take(3), rowCount = table.Rows.Count, mapping });
            }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithMetadata(new RequestSizeLimitAttribute(1_500_000));
        group.MapPost("/preview", (ImportFileRequest request, AppDbContext db, ClaimsPrincipal user, IDataProtectionProvider protection) =>
            ImportPerformance(request, false, db, user, protection)).WithMetadata(new RequestSizeLimitAttribute(1_500_000));
        group.MapPost("/commit", (ImportFileRequest request, AppDbContext db, ClaimsPrincipal user, IDataProtectionProvider protection) =>
            ImportPerformance(request, true, db, user, protection)).WithMetadata(new RequestSizeLimitAttribute(1_500_000));
        group.MapGet("/history", async (AppDbContext db) => await db.AuditRecords.AsNoTracking()
            .Where(x => x.Action == "MonthlyPerformanceImported").OrderByDescending(x => x.CreatedAt).Take(30)
            .Select(x => new { x.Id, x.UserId, x.CreatedAt, details = x.NewValueJson }).ToListAsync());
    }

    private static async Task<IResult> ImportPerformance(ImportFileRequest request, bool commit, AppDbContext db, ClaimsPrincipal user, IDataProtectionProvider protection)
    {
        ImportFileTable table;
        try { table = PerformanceImportFile.Read(request); }
        catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        var fields = PerformanceImportFile.Fields; var mapping = request.Mapping;
        if (mapping is null || mapping.Count != fields.Length || fields.Any(x => !mapping.TryGetValue(x.Key, out var i) || i < 0 || i >= table.Headers.Length) || mapping.Values.Distinct().Count() != fields.Length)
            return Results.BadRequest(new { error = "Her alanı farklı bir dosya sütunuyla eşleştirin. Boş sayıları sıfır kabul etmiyoruz; kullanılmayan tutarlara dosyada 0 yazın." });
        var protector = protection.CreateProtector("OvoGrowthOS.MonthlyImport.v1"); ImportApproval? approval = null;
        if (commit)
        {
            try
            {
                if (request.PreviewToken is null || request.PreviewToken.Length > 8192) throw new CryptographicException();
                approval = JsonSerializer.Deserialize<ImportApproval>(protector.Unprotect(request.PreviewToken));
                if (approval is null || approval.Actor != User(user) || approval.ExpiresAt <= DateTimeOffset.UtcNow) throw new CryptographicException();
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            { return Results.Conflict(new { error = "Ön izleme onayı bulunamadı veya süresi doldu. Yeniden ön izleme yapın." }); }
        }
        // Existing period uniqueness and the single SaveChanges transaction protect the whole batch.
        // The share lock also prevents deal changes between final validation and the insertion.
        await using var transaction = commit && db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (transaction is not null) await db.Database.ExecuteSqlRawAsync("LOCK TABLE growth.\"PartnershipDeals\" IN SHARE MODE");
        var ids = table.Rows.Select(x => Guid.TryParse(x.Values[mapping["dealId"]], out var id) ? id : Guid.Empty).Distinct().ToArray();
        var deals = await db.Deals.AsNoTracking().Include(x => x.Brand).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var brandIds = table.Rows.Select(x => Guid.TryParse(x.Values[mapping["brandId"]], out var id) ? id : Guid.Empty).Distinct().ToArray();
        var existing = await db.MonthlyPerformances.AsNoTracking().Where(x => brandIds.Contains(x.BrandId)).Select(x => new { x.BrandId, x.Year, x.Month }).ToListAsync();
        List<ImportRowPreview> previews = []; List<MonthlyPerformance> prepared = []; List<object> fingerprints = [];
        var seen = new HashSet<(Guid, int, int)>(); var validator = new PerformanceRequestValidator();
        foreach (var row in table.Rows)
        {
            string Value(string key) => row.Values[mapping[key]].Trim();
            var errors = new List<string>();
            Guid Code(string key)
            {
                if (Guid.TryParse(Value(key), out var id) && id != Guid.Empty) return id;
                errors.Add($"{fields.Single(x => x.Key == key).Label}: listedeki kodu aynen kopyalayın."); return Guid.Empty;
            }
            var brandId = Code("brandId"); var dealId = Code("dealId");
            var periodText = Value("period");
            var validPeriod = DateOnly.TryParseExact(periodText, new[] { "yyyy-MM", "MM.yyyy", "dd.MM.yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var period);
            if (!validPeriod || period.Day != 1 || period.Year is < 2020 or > 2100) errors.Add("Dönem: 2026-08 veya 08.2026 yazın. Tam tarih kullanıyorsanız ayın ilk günü olmalıdır.");
            var currency = Value("currency").ToUpperInvariant();
            var numbers = new Dictionary<string, decimal>();
            foreach (var field in fields.Skip(4))
            {
                try
                {
                    var number = PerformanceImportFile.Number(Value(field.Key));
                    if (field.Key is "orders" or "sessions" or "newCustomers" or "returningCustomers" && (number != decimal.Truncate(number) || number > 1_000_000_000))
                        throw new InvalidDataException("0–1 milyar arasında tam sayı kullanın.");
                    numbers[field.Key] = number;
                }
                catch (InvalidDataException ex) { errors.Add($"{field.Label}: {ex.Message}"); }
            }
            deals.TryGetValue(dealId, out var deal);
            if (deal is null) errors.Add("Anlaşma bulunamadı. Ekrandaki etkin anlaşma listesinden kodu kopyalayın.");
            else
            {
                if (deal.BrandId != brandId) errors.Add("Marka kodu ile anlaşma kodu aynı markaya ait değil.");
                if (deal.Status != DealStatus.Active) errors.Add("Anlaşma etkin değil.");
                if (currency != deal.Currency.ToUpperInvariant()) errors.Add("Para birimi anlaşmayla aynı olmalıdır; kur dönüşümü yapılmaz.");
                if (validPeriod && (deal.StartDate.HasValue && period.AddMonths(1).AddDays(-1) < deal.StartDate.Value || deal.EndDate.HasValue && period > deal.EndDate.Value))
                    errors.Add("Dönem anlaşmanın başlangıç/bitiş aralığı dışında.");
            }
            if (validPeriod)
            {
                if (!seen.Add((brandId, period.Year, period.Month))) errors.Add("Aynı marka ve dönem dosyada birden fazla kez var.");
                if (existing.Any(x => x.BrandId == brandId && x.Year == period.Year && x.Month == period.Month)) errors.Add("Bu marka ve dönem zaten kayıtlı. Kilitli veya taslak hiçbir mevcut kayıt üzerine yazılmaz.");
            }
            MonthlyPerformance? p = null;
            if (errors.Count == 0)
            {
                decimal N(string key) => numbers[key]; int Count(string key) => (int)N(key);
                var input = new PerformanceRequest(brandId, dealId, period.Year, period.Month, N("grossSales"), N("vat"), N("refunds"), N("cancellations"), N("chargebacks"), N("customerPaidShipping"), N("giftCardTopups"),
                    Count("orders"), Count("sessions"), Count("newCustomers"), Count("returningCustomers"), N("cogs"), N("paymentFees"), N("fulfillmentCosts"), N("shippingSubsidy"), N("otherVariableCosts"), N("metaSpend"), N("googleSpend"), N("tikTokSpend"), N("influencerSpend"), N("otherAdSpend"));
                errors.AddRange((await validator.ValidateAsync(input)).Errors.Select(x => x.ErrorMessage).Distinct());
                if (errors.Count == 0)
                {
                    p = ToPerformance(input); p.PreparedBy = User(user); MonthlyPerformanceCalculator.CalculateForImport(p, deal!);
                    prepared.Add(p);
                    fingerprints.Add(new { input, deal = JsonSerializer.Serialize(deal, Json), p.NetRevenue, p.OvoFee, p.TotalAdSpend, p.BrandContributionProfit, p.OvoGrossProfit, p.CommissionBreakdownJson });
                }
            }
            previews.Add(new(row.Line, deal?.BrandId == brandId ? deal.Brand?.Name ?? "" : "Eşleşme yok", deal?.Name ?? "", periodText, currency,
                p?.NetRevenue, p?.OvoFee, p?.TotalAdSpend, p?.BrandContributionProfit, errors.ToArray()));
        }
        var canCommit = previews.All(x => x.Errors.Length == 0);
        // Never present a partial sum as a file total. Currencies remain separate.
        var totals = canCommit ? previews.GroupBy(x => x.Currency).Select(g => new ImportTotal(g.Key, g.Sum(x => x.NetRevenue!.Value), g.Sum(x => x.OvoFee!.Value), g.Sum(x => x.AdSpend!.Value), g.Sum(x => x.BrandContribution!.Value))).ToArray() : [];
        var fileHash = Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(request.ContentBase64)));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { fileHash, request.FileName, mapping = fields.Select(x => mapping[x.Key]), fingerprints }, Json))));
        if (commit)
        {
            if (!canCommit || approval!.Fingerprint != fingerprint) return Results.Conflict(new { error = "Dosya, eşleştirme veya kayıtlar ön izlemeden sonra değişmiş. Hiçbir satır kaydedilmedi; yeniden ön izleme yapın." });
            var batchId = Guid.NewGuid();
            db.MonthlyPerformances.AddRange(prepared);
            var summary = new { fileName = request.FileName, fileHash, rowCount = prepared.Count, totals, records = prepared.Select(p => new { p.Id, p.BrandId, p.DealId, p.Year, p.Month }).ToArray() };
            Audit(db, user, "MonthlyPerformanceImported", "PerformanceImport", batchId, null, summary);
            foreach (var p in prepared) Audit(db, user, "MonthlyPerformanceCreated", "MonthlyPerformance", p.Id, null, p, $"Dosyadan aktarım: {batchId}");
            await db.SaveChangesAsync(); if (transaction is not null) await transaction.CommitAsync();
            return Results.Ok(new { batchId, summary.rowCount, totals, records = summary.records });
        }
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var previewToken = canCommit ? protector.Protect(JsonSerializer.Serialize(new ImportApproval(User(user), fingerprint, expiresAt))) : null;
        return Results.Ok(new { rows = previews, totals, canCommit, previewToken, expiresAt });
    }
}
