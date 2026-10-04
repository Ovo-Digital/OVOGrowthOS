using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Features;

public static partial class WorkflowEndpoints
{
    private static async Task<IResult> ListCommissions(AppDbContext db, int page = 1, int pageSize = 20,
        string? search = null, CommissionStatus? status = null, string sort = "recent",
        int? year = null, int? month = null, ReportScope scope = ReportScope.All, Guid? brandId = null, string? currency = null, string collection = "all")
    {
        var (error, rows, ordered, resolvedCurrency, today) = await CommissionRows(db, search, status, sort, year, month, scope, brandId, currency, collection);
        if (error is not null) return error;
        var currencies = (await db.Deals.Select(x => x.Currency).Distinct().ToListAsync()).Append(resolvedCurrency).Distinct().Order().ToList();
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        return Results.Ok(new
        {
            page, pageSize, total = rows.Count, currency = resolvedCurrency, currencies, scope, summary = PortfolioReporting.Summarize(rows),
            paid = PortfolioReporting.Paid(rows), outstanding = PortfolioReporting.Outstanding(rows),
            overdue = rows.Sum(x => Collections.Balance(x, today) is { OverdueDays: > 0 } b ? b.Outstanding : 0),
            items = ordered.ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
            {
                x.Id, x.BrandId, x.Year, x.Month, brand = x.Brand!.Name, x.CommissionableRevenue,
                dealType = x.Deal!.DealType, x.Deal.MonthlyRetainer, x.Deal.MinimumMonthlyFee, x.OvoFee,
                effectiveRate = FinancialCalculator.Ratio(x.OvoFee, x.CommissionableRevenue),
                commissionStatus = PortfolioReporting.PaymentStage(x.Status), periodStatus = x.Status,
                balance = Collections.Balance(x, today), x.Collection?.DueOn, invoiceReference = x.Collection?.InvoiceReference ?? ""
            })
        });
    }

    private static async Task<(IResult? Error, List<MonthlyPerformance> Rows, IOrderedEnumerable<MonthlyPerformance> Ordered, string Currency, DateOnly Today)> CommissionRows(
        AppDbContext db, string? search, CommissionStatus? status, string sort, int? year, int? month,
        ReportScope scope, Guid? brandId, string? currency, string collection)
    {
        if (year.HasValue != month.HasValue || year is < 2020 or > 2100 || month is < 1 or > 12 ||
            !Enum.IsDefined(scope) || status.HasValue && !Enum.IsDefined(status.Value))
            return (Results.BadRequest(new { error = "Geçerli bir dönem ve durum seçin." }), [], default!, "", default);
        if (collection is not ("all" or "outstanding" or "partial" or "overdue" or "settled" or "review"))
            return (Results.BadRequest(new { error = "Geçerli bir tahsilat görünümü seçin." }), [], default!, "", default);
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var settings = await db.GeneralSettings.AsNoTracking().SingleAsync();
        currency = (currency ?? settings.DefaultCurrency).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            return (Results.BadRequest(new { error = "Geçerli bir para birimi seçin." }), [], default!, "", default);
        var query = db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal)
            .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
            .Where(x => (!year.HasValue || x.Year == year && x.Month == month) &&
                (!brandId.HasValue || x.BrandId == brandId) && x.Deal!.Currency.ToUpper() == currency);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.Brand!.Name, $"%{search.Trim()}%"));
        var rows = (await query.ToListAsync()).Where(x => PortfolioReporting.Matches(x.Status, scope) &&
            (!status.HasValue || PortfolioReporting.PaymentStage(x.Status) == status)).Where(x =>
            {
                var b = Collections.Balance(x, today);
                return collection switch { "outstanding" => b.Outstanding > 0, "partial" => b.State == "PartiallyPaid",
                    "overdue" => b.OverdueDays > 0, "settled" => b.State == "Settled", "review" => b.NeedsReview, _ => true };
            }).ToList();
        var ordered = sort switch
        {
            "oldest" => rows.OrderBy(x => x.Year).ThenBy(x => x.Month),
            "name" => rows.OrderBy(x => x.Brand!.Name),
            "nameDesc" => rows.OrderByDescending(x => x.Brand!.Name),
            _ => rows.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
        };
        return (null, rows, ordered, currency, today);
    }

    private static async Task<IResult> ExportCommissions(AppDbContext db, string? search = null, CommissionStatus? status = null, string sort = "recent",
        int? year = null, int? month = null, ReportScope scope = ReportScope.All, Guid? brandId = null, string? currency = null, string collection = "all")
    {
        var (error, rows, ordered, resolvedCurrency, today) = await CommissionRows(db, search, status, sort, year, month, scope, brandId, currency, collection);
        if (error is not null) return error;
        string StatusLabel(CommissionStatus value) => value switch
        {
            CommissionStatus.Draft => "Taslak", CommissionStatus.Approved => "Onaylandı",
            CommissionStatus.Invoiced => "Faturalandı", _ => "Ödendi"
        };
        string PeriodLabel(MonthlyPerformanceStatus value) => value switch
        {
            MonthlyPerformanceStatus.Draft => "Taslak", MonthlyPerformanceStatus.UnderReview => "İncelemede",
            MonthlyPerformanceStatus.Approved => "Onaylandı", MonthlyPerformanceStatus.Locked => "Kilitlendi",
            MonthlyPerformanceStatus.Invoiced => "Faturalandı", _ => "Ödendi"
        };
        string DealLabel(DealType value) => value switch
        {
            DealType.FlatRevenueShare => "Sabit ciro payı", DealType.TieredRevenueShare => "Kademeli ciro payı",
            DealType.RetainerPlusRevenueShare => "Aylık hizmet bedeli + ciro payı",
            DealType.MinimumFeePlusRevenueShare => "Asgari ücret + ciro payı",
            DealType.IncrementalRevenueShare => "Büyüme farkı üzerinden pay",
            DealType.RetainerPlusIncrementalRevenueShare => "Aylık hizmet bedeli + büyüme farkı payı",
            DealType.ContributionProfitShare => "Katkı kârı paylaşımı", _ => "Sabit aylık hizmet bedeli"
        };
        string StateLabel(string value) => value switch
        {
            "NeedsReview" => "İnceleme gerekli", "NotClosed" => "Henüz kapanmadı", "Settled" => "Alacak kapandı",
            "PartiallyPaid" => "Kısmen ödendi", "Unpaid" => "Ödeme bekleniyor", _ => "Kontrol edin"
        };
        var headers = new[] { "Ay", "Marka", "Hesaplamaya esas ciro", "Anlaşma modeli", "Aylık ücret", "Asgari ücret",
            "Son hakediş", "Gerçekleşen oran", "Hakediş durumu", "Dönem kapanışı", "Ödenen", "Kalan alacak",
            "Tahsilat durumu", "Vade", "Gecikme (gün)", "Fatura referansı" };
        var data = ordered.ThenBy(x => x.Id).Select(x =>
        {
            var b = Collections.Balance(x, today);
            return new object?[]
            {
                $"{x.Month}/{x.Year}", x.Brand!.Name, x.CommissionableRevenue, DealLabel(x.Deal!.DealType),
                x.Deal.MonthlyRetainer, x.Deal.MinimumMonthlyFee, x.OvoFee,
                FinancialCalculator.Ratio(x.OvoFee, x.CommissionableRevenue), StatusLabel(PortfolioReporting.PaymentStage(x.Status)),
                PeriodLabel(x.Status), b.Paid, b.Outstanding, StateLabel(b.State), x.Collection?.DueOn, b.OverdueDays,
                x.Collection?.InvoiceReference ?? ""
            };
        }).ToList();
        var summary = PortfolioReporting.Summarize(rows);
        data.Add([]);
        data.Add(["Kayıt sayısı", rows.Count]);
        data.Add(["Toplam hakediş (kapsamdaki)", summary.OvoFee]);
        data.Add(["Ödenen", PortfolioReporting.Paid(rows)]);
        data.Add(["Kalan alacak", PortfolioReporting.Outstanding(rows)]);
        data.Add(["Bugün vadesi geçmiş kalan", rows.Sum(x => Collections.Balance(x, today) is { OverdueDays: > 0 } b ? b.Outstanding : 0)]);
        data.Add(["Para birimi", resolvedCurrency]);
        data.Add(["Uyarı", "Tutarlar KDV hariçtir. Bu dosya ekranınızdaki filtrelerle aynı kayıtları içerir."]);
        var periodName = year.HasValue ? $"-{year:0000}-{month:00}" : "";
        var bytes = ExcelExport.Build("Hakedişler", $"Hakediş listesi{periodName} ({resolvedCurrency})", headers, data);
        return Results.File(bytes, ExcelExport.MimeType, $"hakedisler{periodName}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    private static async Task<IResult> PortfolioReport(AppDbContext db, int? year = null, int? month = null,
        ReportScope scope = ReportScope.Closed, Guid? brandId = null, string? currency = null,
        string? convertTo = null, string? manualRates = null)
    {
        if (year.HasValue != month.HasValue || year is < 2020 or > 2100 || month is < 1 or > 12 || !Enum.IsDefined(scope))
            return Results.BadRequest(new { error = "Geçerli bir yıl, ay ve rapor kapsamı seçin." });
        if (brandId.HasValue && !await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
        var settings = await db.GeneralSettings.AsNoTracking().SingleAsync();
        currency = (currency ?? settings.DefaultCurrency).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            return Results.BadRequest(new { error = "Üç harfli bir para birimi seçin." });

        var deals = await db.Deals.AsNoTracking().Include(x => x.Brand)
            .Where(x => !brandId.HasValue || x.BrandId == brandId).ToListAsync();
        var currencies = deals.Select(x => x.Currency.ToUpperInvariant()).Append(currency).Distinct().Order().ToArray();
        deals = deals.Where(x => string.Equals(x.Currency, currency, StringComparison.OrdinalIgnoreCase)).ToList();
        var allHistory = await db.MonthlyPerformances.AsNoTracking()
            .Include(x => x.Brand)!.ThenInclude(x => x!.Economics)
            .Include(x => x.Deal)!.ThenInclude(x => x!.Conditions)
            .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
            .Where(x => !brandId.HasValue || x.BrandId == brandId).ToListAsync();
        var history = allHistory.Where(x => x.Deal is not null && string.Equals(x.Deal.Currency, currency, StringComparison.OrdinalIgnoreCase)).ToList();
        var periods = history.Select(x => new ReportPeriod(x.Year, x.Month)).Distinct()
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToList();
        var period = year.HasValue ? new ReportPeriod(year.Value, month!.Value) : periods.FirstOrDefault();
        var periodRows = period is null ? [] : history.Where(x => x.Year == period.Year && x.Month == period.Month).ToList();
        var selected = periodRows.Where(x => PortfolioReporting.Matches(x.Status, scope)).ToList();
        var totals = PortfolioReporting.Summarize(selected);
        var expected = period is null ? [] : deals.Where(x => PortfolioReporting.ExpectedInPeriod(x, period)).Select(x => x.BrandId).Distinct().ToList();
        var recorded = periodRows.Select(x => x.BrandId).ToHashSet();
        var missing = deals.Where(x => expected.Contains(x.BrandId) && !recorded.Contains(x.BrandId))
            .DistinctBy(x => x.BrandId).Select(x => new { x.BrandId, name = x.Brand!.Name }).OrderBy(x => x.name).ToList();
        var largestFee = PortfolioRiskCalculator.LargestShare(selected.Select(x => x.OvoFee));
        var activeDeals = deals.Where(x => x.Status == DealStatus.Active).ToList();
        var trendHistory = history.Where(x => PortfolioReporting.Matches(x.Status, scope)).ToList();
        var trendPeriods = period is null ? [] : Enumerable.Range(0, 12)
            .Select(i => new DateOnly(period.Year, period.Month, 1).AddMonths(i - 11)).ToList();
        var scores = await db.Evaluations.Where(x => (!brandId.HasValue || x.BrandId == brandId) &&
            (x.Status == EvaluationStatus.Approved || x.Status == EvaluationStatus.Analyzed)).Select(x => x.PartnershipScore).ToListAsync();

        var perCurrency = new List<(string Currency, decimal NetRevenue, decimal OvoFee, int RecordCount)>();
        if (period is not null)
            perCurrency.AddRange(allHistory
                .Where(x => x.Year == period.Year && x.Month == period.Month && x.Deal is not null && PortfolioReporting.Matches(x.Status, scope))
                .GroupBy(x => x.Deal!.Currency.ToUpperInvariant())
                .Select(g => (Currency: g.Key, NetRevenue: g.Sum(x => x.NetRevenue), OvoFee: g.Sum(x => x.OvoFee), RecordCount: g.Count()))
                .OrderBy(x => x.Currency));
        object? conversion = null;
        if (convertTo is not null)
        {
            convertTo = convertTo.Trim().ToUpperInvariant();
            if (convertTo.Length != 3 || !convertTo.All(char.IsAsciiLetter))
                return Results.BadRequest(new { error = "Genel toplam para birimi üç harfli olmalı." });
            var rates = new Dictionary<string, decimal>();
            foreach (var entry in (manualRates ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                var code = parts[0].ToUpperInvariant();
                if (parts.Length != 2 || code.Length != 3 || !code.All(char.IsAsciiLetter) ||
                    !decimal.TryParse(parts[1].Replace(',', '.'), System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var rate) || rate <= 0)
                    return Results.BadRequest(new { error = "Kurları şu biçimde girin: USD=38.5 (birden çok para birimini virgülle ayırın)." });
                rates[code] = rate;
            }
            var present = perCurrency.Select(x => x.Currency).ToHashSet();
            var missingCurrencies = present.Where(c => c != convertTo && !rates.ContainsKey(c)).Order().ToList();
            decimal? netTotal = null, feeTotal = null;
            if (missingCurrencies.Count == 0)
            {
                netTotal = perCurrency.Sum(x => x.NetRevenue * (x.Currency == convertTo ? 1m : rates[x.Currency]));
                feeTotal = perCurrency.Sum(x => x.OvoFee * (x.Currency == convertTo ? 1m : rates[x.Currency]));
            }
            conversion = new
            {
                target = convertTo,
                rates = rates.Where(kv => present.Contains(kv.Key)).OrderBy(kv => kv.Key)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                missing = missingCurrencies, netRevenue = netTotal, ovoFee = feeTotal
            };
        }

        return Results.Ok(new
        {
            period, periods, scope, currency, currencies, totals,
            currencyTotals = perCurrency.Select(x => new { currency = x.Currency, netRevenue = x.NetRevenue, ovoFee = x.OvoFee, recordCount = x.RecordCount }),
            conversion,
            activeBrands = await db.Brands.CountAsync(x => x.Status == BrandStatus.Active && (!brandId.HasValue || x.Id == brandId)),
            portfolioNetRevenue = totals.NetRevenue, ovoMonthlyRevenue = totals.OvoFee,
            ovoGrossProfit = totals.OvoGrossProfit, ovoGrossMargin = totals.OvoMargin, portfolioMer = totals.Mer,
            outstandingCommission = PortfolioReporting.Outstanding(history),
            paidCommission = PortfolioReporting.Paid(periodRows),
            periodOutstandingCommission = PortfolioReporting.Outstanding(periodRows),
            allPeriodsPaidCommission = PortfolioReporting.Paid(history),
            cashReceivedInSelectedMonth = period is null ? 0 : Collections.PaidInCalendarMonth(history, period.Year, period.Month),
            legacyUndatedPaid = history.Sum(x => Collections.Balance(x, DateOnly.MinValue).LegacyPaid),
            overdueCommission = history.Sum(x => Collections.Balance(x, TeamWork.Today(DateTimeOffset.UtcNow)) is { OverdueDays: > 0 } b ? b.Outstanding : 0),
            collectionReviewCount = history.Count(x => Collections.Balance(x, DateOnly.MinValue).NeedsReview),
            totalActiveSetupInvestment = activeDeals.Sum(x => x.SetupInvestment),
            stages = new
            {
                closed = PortfolioReporting.Summarize(periodRows.Where(x => PortfolioReporting.IsClosed(x.Status))),
                approved = PortfolioReporting.Summarize(periodRows.Where(x => x.Status == MonthlyPerformanceStatus.Approved)),
                preparation = PortfolioReporting.Summarize(periodRows.Where(x => PortfolioReporting.Matches(x.Status, ReportScope.Preparation)))
            },
            coverage = new
            {
                recordedBrands = recorded.Count, selectedBrands = selected.Select(x => x.BrandId).Distinct().Count(),
                expectedBrands = expected.Count, missingBrands = missing,
                unknownStartDateBrands = activeDeals.Where(x => x.StartDate is null).Select(x => x.BrandId).Distinct().Count(),
                unclosedRecords = periodRows.Count(x => !PortfolioReporting.IsClosed(x.Status))
            },
            averagePartnershipScore = scores.Count == 0 ? (decimal?)null : scores.Average(),
            largestClientRevenueShare = PortfolioRiskCalculator.LargestShare(selected.Select(x => x.NetRevenue)),
            largestClientOvoFeeShare = largestFee,
            top3RevenueConcentration = PortfolioRiskCalculator.TopThreeShare(selected.Select(x => x.NetRevenue)),
            top3OvoRevenueConcentration = PortfolioRiskCalculator.TopThreeShare(selected.Select(x => x.OvoFee)),
            concentrationRisk = totals.RecordCount == 0 || totals.OvoFee == 0 ? "Unknown" : largestFee > settings.ConcentrationRiskThreshold ? "High" : "Normal",
            trends = trendPeriods.Select(date =>
            {
                var summary = PortfolioReporting.Summarize(trendHistory.Where(x => x.Year == date.Year && x.Month == date.Month));
                return new { date.Year, date.Month, summary.RecordCount,
                    netRevenue = summary.RecordCount == 0 ? (decimal?)null : summary.NetRevenue,
                    ovoRevenue = summary.RecordCount == 0 ? (decimal?)null : summary.OvoFee,
                    ovoGrossProfit = summary.RecordCount == 0 ? (decimal?)null : summary.OvoGrossProfit,
                    ovoMargin = summary.OvoMargin, mer = summary.Mer };
            }),
            dealModelDistribution = activeDeals.GroupBy(x => x.DealType).Select(x => new { dealType = x.Key, count = x.Count() }),
            brands = selected.OrderBy(x => x.Brand!.Name).Select(x => new
            {
                x.Id, x.BrandId, name = x.Brand!.Name, x.Status, x.NetRevenue, x.OvoFee, x.OvoGrossProfit, x.OvoInternalCost,
                mer = x.TotalAdSpend == 0 ? (decimal?)null : FinancialCalculator.Ratio(x.NetRevenue, x.TotalAdSpend),
                contributionMargin = x.NetRevenue == 0 ? (decimal?)null : FinancialCalculator.Ratio(x.BrandContributionProfit, x.NetRevenue),
                health = BrandHealth(x, trendHistory)
            })
        });
    }

    private static async Task<IResult> PortfolioStressReport(AppDbContext db, decimal shockRate, int? year = null, int? month = null,
        ReportScope scope = ReportScope.Closed, Guid? brandId = null, string? currency = null)
    {
        if (shockRate is < PortfolioStress.MinShock or > PortfolioStress.MaxShock)
            return Results.BadRequest(new { error = "Kayıp oranı -%90 ile 0 arasında olmalıdır; -20 yazımı %20 kayıp demektir." });
        if (year.HasValue != month.HasValue || year is < 2020 or > 2100 || month is < 1 or > 12 || !Enum.IsDefined(scope))
            return Results.BadRequest(new { error = "Geçerli bir yıl, ay ve rapor kapsamı seçin." });
        if (brandId.HasValue && !await db.Brands.AnyAsync(x => x.Id == brandId)) return Results.NotFound();
        var settings = await db.GeneralSettings.AsNoTracking().SingleAsync();
        currency = (currency ?? settings.DefaultCurrency).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            return Results.BadRequest(new { error = "Üç harfli bir para birimi seçin." });

        var history = (await db.MonthlyPerformances.AsNoTracking().Include(x => x.Brand).Include(x => x.Deal)
                .Where(x => !brandId.HasValue || x.BrandId == brandId).ToListAsync())
            .Where(x => string.Equals(x.Deal?.Currency ?? x.Brand?.Currency ?? "", currency, StringComparison.OrdinalIgnoreCase)).ToList();
        var periods = history.Select(x => new ReportPeriod(x.Year, x.Month)).Distinct()
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToList();
        var period = year.HasValue ? new ReportPeriod(year.Value, month!.Value) : periods.FirstOrDefault();
        var rows = new List<StressRow>();
        if (period is not null) rows = history
            .Where(x => x.Year == period.Year && x.Month == period.Month && PortfolioReporting.Matches(x.Status, scope))
            .Select(x => new StressRow(x.BrandId, x.Brand?.Name ?? "", x.NetRevenue, x.CommissionableRevenue, x.ContributionBeforeOvo,
                x.OvoFee, x.OvoGrossProfit, x.Deal)).ToList();
        return Results.Ok(new
        {
            period, scope, currency, shockRate,
            stress = PortfolioStress.Calculate(rows, shockRate)
        });
    }
}
