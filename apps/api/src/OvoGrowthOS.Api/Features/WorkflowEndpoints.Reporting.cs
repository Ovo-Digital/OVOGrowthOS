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
        if (year.HasValue != month.HasValue || year is < 2020 or > 2100 || month is < 1 or > 12 ||
            !Enum.IsDefined(scope) || status.HasValue && !Enum.IsDefined(status.Value))
            return Results.BadRequest(new { error = "Geçerli bir dönem ve durum seçin." });
        if (collection is not ("all" or "outstanding" or "partial" or "overdue" or "settled" or "review")) return Results.BadRequest(new { error = "Geçerli bir tahsilat görünümü seçin." });
        var today = TeamWork.Today(DateTimeOffset.UtcNow);
        var settings = await db.GeneralSettings.AsNoTracking().SingleAsync();
        currency = (currency ?? settings.DefaultCurrency).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter)) return Results.BadRequest(new { error = "Geçerli bir para birimi seçin." });
        var currencies = (await db.Deals.Select(x => x.Currency).Distinct().ToListAsync()).Append(currency).Distinct().Order().ToList();
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
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        return Results.Ok(new
        {
            page, pageSize, total = rows.Count, currency, currencies, scope, summary = PortfolioReporting.Summarize(rows),
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

    private static async Task<IResult> PortfolioReport(AppDbContext db, int? year = null, int? month = null,
        ReportScope scope = ReportScope.Closed, Guid? brandId = null, string? currency = null)
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
        var history = await db.MonthlyPerformances.AsNoTracking()
            .Include(x => x.Brand)!.ThenInclude(x => x!.Economics)
            .Include(x => x.Deal)!.ThenInclude(x => x!.Conditions)
            .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
            .Where(x => (!brandId.HasValue || x.BrandId == brandId) && x.Deal!.Currency.ToUpper() == currency).ToListAsync();
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

        return Results.Ok(new
        {
            period, periods, scope, currency, currencies, totals,
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
}
