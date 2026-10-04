namespace OvoGrowthOS.Domain;

public sealed record RenewalSimulationResult(
    string Currency, string DealType,
    decimal RevenueChangeRate, decimal ShareChangePoints, decimal RetainerChange,
    decimal BaseRevenue, decimal BaseContribution, decimal BaseOvoFee, decimal BaseEffectiveRate, decimal BaseBrandContribution,
    decimal SimRevenue, decimal SimContribution, decimal SimOvoFee, decimal SimEffectiveRate, decimal SimBrandContribution,
    decimal FeeDelta, decimal BrandContributionDelta,
    decimal BaseShareRate, decimal SimulatedShareRate, decimal BaseRetainer, decimal SimulatedRetainer,
    bool ShareApplied, bool RetainerApplied,
    IReadOnlyList<string> Notes);

// "If we changed X" negotiation scenario for a renewal meeting. It re-runs the existing
// commission engine on a copy of the deal; nothing is persisted and no agreement is touched.
public static class RenewalSimulation
{
    public const decimal MinRevenueChange = -0.9m;
    public const decimal MaxRevenueChange = 3m;
    public const decimal MinSharePoints = -10m;
    public const decimal MaxSharePoints = 20m;
    public const decimal MaxRetainerChange = 1_000_000m;

    public static RenewalSimulationResult Simulate(Deal deal, decimal baseRevenue, decimal baseContribution,
        decimal revenueChangeRate, decimal shareChangePoints, decimal retainerChange)
    {
        if (revenueChangeRate is < MinRevenueChange or > MaxRevenueChange)
            throw new ArgumentOutOfRangeException(nameof(revenueChangeRate), "Ciro değişimi -%90 ile +%300 arasında olmalıdır.");
        if (shareChangePoints is < MinSharePoints or > MaxSharePoints)
            throw new ArgumentOutOfRangeException(nameof(shareChangePoints), "Pay değişimi -10 ile +20 puan arasında olmalıdır.");
        if (retainerChange is < -MaxRetainerChange or > MaxRetainerChange)
            throw new ArgumentOutOfRangeException(nameof(retainerChange), "Sabit ücret değişimi çok büyük.");
        if (baseRevenue < 0 || baseContribution < 0)
            throw new ArgumentOutOfRangeException(nameof(baseRevenue), "Dayanak ciro ve katkı negatif olamaz.");

        var simRevenue = R(baseRevenue * (1 + revenueChangeRate));
        var ratio = baseRevenue == 0 ? 1 : simRevenue / baseRevenue;
        var simContribution = R(baseContribution * ratio);

        var notes = new List<string>
        {
            "Bu ekran yalnız simülasyondur; kaydetmez, hiçbir anlaşma koşulunu, hakedişi veya dönemi değiştirmez.",
            $"Dayanak olarak son kilitlenmiş dönemin cirosu ({R(baseRevenue)}) ve katkı tutarı ({R(baseContribution)}) alınmıştır; ciro değişimi katkı tutarını da aynı oranda değiştirdiği varsayılmıştır."
        };

        var shareRate = deal.DealType switch
        {
            DealType.IncrementalRevenueShare or DealType.RetainerPlusIncrementalRevenueShare => deal.IncrementalRate,
            DealType.ContributionProfitShare => deal.ProfitShareRate,
            _ => deal.RevenueShareRate
        };
        var shareApplied = true;
        var shareNote = shareChangePoints == 0 ? null : deal.DealType switch
        {
            DealType.TieredRevenueShare => "Kademeli pay modelinde pay değişimi uygulanmaz; kademeler olduğu gibi korunur.",
            DealType.FixedRetainer => "Sabit ücretli anlaşmada pay değişimi yoktur.",
            _ => null
        };
        if (shareNote is not null) shareApplied = false;
        var simShare = shareApplied ? Math.Max(0, shareRate + shareChangePoints / 100m) : shareRate;
        if (shareApplied && simShare != shareRate + shareChangePoints / 100m)
            notes.Add("Negatife düşen pay sıfıra sabitlendi.");
        if (deal.DealType == DealType.ContributionProfitShare && shareApplied && simShare >= 1)
        {
            simShare = 0.99m;
            notes.Add("Katkı payı %100 sınırını aşamaz; %99'a sabitlendi.");
        }
        if (shareNote is not null) notes.Add(shareNote);
        if (deal.DealType == DealType.MinimumFeePlusRevenueShare)
            notes.Add("Asgari aylık ücret uygulanır; hesaplanan pay asgari ücretin altına düşerse asgari ücret yazılır.");

        var simRetainer = deal.DealType is DealType.RetainerPlusRevenueShare or DealType.RetainerPlusIncrementalRevenueShare
            or DealType.ContributionProfitShare or DealType.FixedRetainer ? deal.MonthlyRetainer + retainerChange : deal.MonthlyRetainer;
        if (simRetainer < 0) throw new ArgumentOutOfRangeException(nameof(retainerChange), "Sabit ücret sıfırın altına inemez.");
        var retainerApplied = deal.DealType is DealType.RetainerPlusRevenueShare or DealType.RetainerPlusIncrementalRevenueShare
            or DealType.ContributionProfitShare or DealType.FixedRetainer;
        if (retainerChange != 0 && !retainerApplied)
            notes.Add("Bu anlaşma modelinde aylık sabit ücret yok; sabit ücret değişimi uygulanmadı.");

        var simulated = new Deal
        {
            BrandId = deal.BrandId, EvaluationId = deal.EvaluationId, Name = deal.Name, DealType = deal.DealType,
            MonthlyRetainer = simRetainer, MinimumMonthlyFee = deal.MinimumMonthlyFee,
            RevenueShareRate = deal.DealType switch
            {
                DealType.IncrementalRevenueShare or DealType.RetainerPlusIncrementalRevenueShare or DealType.ContributionProfitShare => deal.RevenueShareRate,
                _ => simShare
            },
            BaselineRevenue = deal.BaselineRevenue,
            IncrementalRate = deal.DealType is DealType.IncrementalRevenueShare or DealType.RetainerPlusIncrementalRevenueShare ? simShare : deal.IncrementalRate,
            ProfitShareRate = deal.DealType == DealType.ContributionProfitShare ? simShare : deal.ProfitShareRate,
            CommissionTiersJson = deal.CommissionTiersJson
        };

        var baseFee = DealCommissionCalculator.Calculate(deal, baseRevenue, baseContribution).FinalFee;
        var simFee = DealCommissionCalculator.Calculate(simulated, simRevenue, simContribution).FinalFee;
        return new(deal.Currency, deal.DealType.ToString(), revenueChangeRate, shareChangePoints, retainerChange,
            R(baseRevenue), R(baseContribution), R(baseFee), CommissionCalculator.EffectiveRate(baseFee, baseRevenue), R(baseContribution - baseFee),
            simRevenue, simContribution, R(simFee), CommissionCalculator.EffectiveRate(simFee, simRevenue), R(simContribution - simFee),
            R(simFee - baseFee), R((simContribution - simFee) - (baseContribution - baseFee)),
            R(shareRate), simShare, R(deal.MonthlyRetainer), simRetainer, shareApplied, retainerApplied, notes);
    }

    private static decimal R(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
