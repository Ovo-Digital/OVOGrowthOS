namespace OvoGrowthOS.Domain;

public sealed record StressRow(Guid BrandId, string BrandName, decimal NetRevenue, decimal CommissionableRevenue,
    decimal ContributionBeforeOvo, decimal OvoFee, decimal OvoGrossProfit, Deal? Deal);

public sealed record PortfolioStressResult(
    decimal ShockRate, int RecordCount, Guid BrandId, string BrandName, bool BrandHasAgreement,
    decimal BaseRevenue, decimal SimRevenue, decimal RevenueDelta,
    decimal BaseFee, decimal SimFee, decimal FeeDelta,
    decimal BaseProfit, decimal SimProfit, decimal ProfitDelta,
    decimal BrandBaseRevenue, decimal BrandSimRevenue, decimal BrandBaseFee, decimal BrandSimFee,
    IReadOnlyList<string> Notes);

// "What if the biggest brand shrinks?" stress view for the portfolio report. Read-only:
// it recomputes the shocked brand through the commission engine and never touches records.
public static class PortfolioStress
{
    public const decimal MinShock = -0.9m;
    public const decimal MaxShock = 0m;

    public static PortfolioStressResult Calculate(IReadOnlyList<StressRow> rows, decimal shockRate)
    {
        if (shockRate is < MinShock or > MaxShock)
            throw new ArgumentOutOfRangeException(nameof(shockRate), "Kayıp oranı -%90 ile 0 arasında olmalıdır.");

        var notes = new List<string>
        {
            "Bu ekran yalnız simülasyondur; kaydetmez, hiçbir dönem kaydını, hakedişi veya anlaşmayı değiştirmez.",
            "Şok yalnız en yüksek ciroya sahip markaya uygulanır; diğer markalar aynı kalır.",
            "Hesaplanabilir ciro ve katkı tutarı da aynı oranda değiştiği varsayılmıştır; sabit ücret ve iç maliyet aynı kalır."
        };
        var baseRevenue = R(rows.Sum(x => x.NetRevenue));
        var baseFee = R(rows.Sum(x => x.OvoFee));
        var baseProfit = R(rows.Sum(x => x.OvoGrossProfit));
        if (rows.Count == 0)
        {
            notes.Add("Seçili dönemde kayıt yok; stres testi hesaplanamadı.");
            return new(shockRate, 0, Guid.Empty, "", false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, notes);
        }

        var largest = rows.OrderByDescending(x => x.NetRevenue).ThenBy(x => x.BrandId).First();
        var brandBaseRevenue = R(largest.NetRevenue);
        if (brandBaseRevenue <= 0)
        {
            notes.Add("Bu dönemde ciro bilgisi yok; şok uygulanamadı.");
            return new(shockRate, rows.Count, largest.BrandId, largest.BrandName, largest.Deal is not null,
                baseRevenue, baseRevenue, 0, baseFee, baseFee, 0, baseProfit, baseProfit, 0,
                brandBaseRevenue, brandBaseRevenue, R(largest.OvoFee), R(largest.OvoFee), notes);
        }

        var brandSimRevenue = R(brandBaseRevenue * (1 + shockRate));
        var revenueDelta = R(brandSimRevenue - brandBaseRevenue);
        decimal feeDelta = 0;
        if (largest.Deal is null)
            notes.Add($"{largest.BrandName} markası için anlaşma kaydı yok; hakediş farkı hesaplanamadı, yalnız ciro ve kâr etkisi yazıldı.");
        else if (largest.CommissionableRevenue <= 0)
            notes.Add("Bu dönemde hesaplanabilir ciro boş; hakediş farkı hesaplanamadı, yalnız ciro ve kâr etkisi yazıldı.");
        else
        {
            var baseFeeBrand = DealCommissionCalculator.Calculate(largest.Deal, largest.CommissionableRevenue, largest.ContributionBeforeOvo).FinalFee;
            var simFeeBrand = DealCommissionCalculator.Calculate(largest.Deal,
                R(largest.CommissionableRevenue * (1 + shockRate)), R(largest.ContributionBeforeOvo * (1 + shockRate))).FinalFee;
            feeDelta = R(simFeeBrand - baseFeeBrand);
            if (feeDelta == 0)
                notes.Add("Hakediş farkı yok; bu anlaşma modelinde ciro değişimi hakedişi etkilemiyor.");
            else
                notes.Add("Hakediş farkı, markanın anlaşma modeli üzerinden yeniden hesaplandı.");
        }

        var simProfit = R(baseProfit + feeDelta);
        return new(shockRate, rows.Count, largest.BrandId, largest.BrandName, largest.Deal is not null,
            baseRevenue, R(baseRevenue + revenueDelta), revenueDelta,
            baseFee, R(baseFee + feeDelta), feeDelta,
            baseProfit, simProfit, R(simProfit - baseProfit),
            brandBaseRevenue, brandSimRevenue, R(largest.OvoFee), R(largest.OvoFee + feeDelta), notes);
    }

    private static decimal R(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
