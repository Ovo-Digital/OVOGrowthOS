namespace OvoGrowthOS.Domain;

public sealed record TargetProjection(
    bool Calculable,
    string? NotReason,
    string? DealName,
    string DealType,
    decimal NetRevenueGoal,
    string Currency,
    decimal? OvoFee,
    decimal? EffectiveRate,
    decimal? OvoInternalCost,
    decimal? OvoGrossProfit,
    decimal? BrandContributionProfit,
    decimal? BrandContributionMarginGoal);

public static class TargetProjectionEngine
{
    // "If the target is met" scenario, never a forecast. The commission base is the net
    // revenue goal itself; contribution-profit deals additionally need the margin goal.
    public static TargetProjection Project(MonthlyTarget target, Deal? deal)
    {
        var reason = deal is null ? "Bu marka ve para birimi için etkin anlaşma yok; hakediş oranları uygulanamadı."
            : target.NetRevenueGoal <= 0 ? "Net ciro hedefi sıfırdan büyük olmadığı için projeksiyon hesaplanamaz."
            : null;
        var marginGoal = target.ContributionMarginGoal > 0 ? target.ContributionMarginGoal : (decimal?)null;
        decimal? profitGoal = marginGoal is { } margin ? R(margin * target.NetRevenueGoal) : null;
        decimal? fee = null;
        if (reason is null)
        {
            if (deal!.DealType == DealType.ContributionProfitShare)
            {
                if (profitGoal is not { } profit)
                    reason = "Katkı payı anlaşmalarında projeksiyonun hesaplanması için katkı marjı hedefi gerekir.";
                else if (deal.ProfitShareRate >= 1)
                    reason = "Katkı payı oranı %100 veya üzerinde olduğu için projeksiyon hesaplanamaz.";
                else
                    fee = R((deal.MonthlyRetainer + deal.ProfitShareRate * profit) / (1 - deal.ProfitShareRate));
            }
            else
                fee = DealCommissionCalculator.Calculate(deal, target.NetRevenueGoal, profitGoal ?? 0).FinalFee;
        }
        return new(reason is null, reason, deal?.Name, deal?.DealType.ToString() ?? "", target.NetRevenueGoal, target.Currency,
            fee, fee is { } f ? CommissionCalculator.EffectiveRate(f, target.NetRevenueGoal) : null,
            deal?.EstimatedMonthlyInternalCost, fee is { } paid ? R(paid - deal!.EstimatedMonthlyInternalCost) : null,
            profitGoal, marginGoal);
    }
    private static decimal R(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
