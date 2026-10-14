using System.Text.Json;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Domain.Tests;

public sealed class FinancialEngineTests
{
    private static readonly CommissionTier[] Tiers = [new(0, 500_000, .08m), new(500_000, 1_500_000, .06m), new(1_500_000, 3_000_000, .045m), new(3_000_000, null, .035m)];
    [Fact] public void Tiered_commission_is_marginal() => Assert.Equal(122_500m, CommissionCalculator.Tiered(2_000_000m, Tiers));
    [Fact] public void Tier_breakdown_is_transparent() { var x=CommissionCalculator.TieredBreakdown(2_000_000,Tiers);Assert.Equal(3,x.Count);Assert.Equal(22_500,x[2].Fee); }
    [Theory] [InlineData(1_000_000,.05,50_000)] [InlineData(0,.05,0)] public void Flat_commission(decimal revenue,decimal rate,decimal expected)=>Assert.Equal(expected,CommissionCalculator.Flat(revenue,rate));
    [Fact] public void Minimum_fee_floors_share()=>Assert.Equal(45_000,CommissionCalculator.MinimumFee(32_000,45_000));
    [Fact] public void Incremental_charges_above_baseline()=>Assert.Equal(80_000,CommissionCalculator.Incremental(8_000_000,5_000_000,20_000,.02m));
    [Fact] public void Retainer_plus_incremental_is_supported()=>Assert.Equal(80_000,DealCommissionCalculator.Calculate(Deal(DealType.RetainerPlusIncrementalRevenueShare,retainer:20_000,incremental:.02m,baseline:5_000_000),8_000_000,0).FinalFee);
    [Fact] public void Profit_share_ignores_negative_profit()=>Assert.Equal(20_000,CommissionCalculator.ContributionProfitShare(-10_000,20_000,.1m));
    [Fact] public void Effective_rate_is_calculated()=>Assert.Equal(.0613m,CommissionCalculator.EffectiveRate(122_500,2_000_000));
    [Fact] public void Adjustments_do_not_replace_calculated_fee()=>Assert.Equal(55_000,DealCommissionCalculator.Calculate(Deal(DealType.FlatRevenueShare,rate:.05m),1_000_000,0,5_000).FinalFee);
    [Fact] public void Contribution_waterfall_is_correct(){var x=FinancialCalculator.Calculate(new(1_000_000,50_000,10_000,0,350_000,80_000,200_000,45_000));Assert.Equal(940_000,x.NetRevenue);Assert.Equal(265_000,x.BrandContributionProfit);Assert.Equal(.2819m,x.BrandContributionMargin);}
    [Fact] public void Break_even_returns_zero_when_unsustainable()=>Assert.Equal(0,FinancialCalculator.BreakEvenMer(.25m,.10m,.05m,.15m));
    [Fact] public void Setup_payback_is_calculated()=>Assert.Equal(5,FinancialCalculator.PaybackMonths(300_000,60_000));
    [Fact] public void Data_confidence_uses_documented_weights()=>Assert.Equal(53.8m,DataConfidenceCalculator.Calculate(DataConfidence.Verified,DataConfidence.ProvidedByBrand,DataConfidence.Estimated,DataConfidence.Unknown));
    [Fact] public void Need_more_data_blocks_recommendation(){var e=Economics();e.GrossMarginConfidence=DataConfidence.Unknown;var x=DealRecommendationEngine.Recommend(e,Evaluation(),Rules(),Settings());Assert.Equal(DecisionStatus.NeedMoreData,x.Decision);Assert.Contains("Brüt kâr marjı",x.MissingInputs);}
    [Fact] public void Recommendation_thresholds_come_from_settings(){var settings=Settings();settings.MinimumPartnershipScore=95;var x=DealRecommendationEngine.Recommend(Economics(),Evaluation(),Rules(),settings);Assert.Equal(DecisionStatus.Reject,x.Decision);}
    [Fact] public void Minimum_ad_spend_comes_from_settings(){var settings=Settings();settings.MinimumRecommendedAdSpend=275_000;var x=DealRecommendationEngine.Recommend(Economics(),Evaluation(),Rules(),settings);Assert.Equal(275_000,x.RecommendedAdSpend);}
    [Fact] public void Highest_severity_and_weight_rule_wins_regardless_of_input_order(){var high=new Rule{Name="Öncelikli",Field=RuleField.GrossMargin,Operator=RuleOperator.GreaterThan,Value=.1m,Severity=RuleSeverity.High,Weight=10,RecommendationEffect=RecommendationEffect.PreferRetainerLowShare};var low=new Rule{Name="İkincil",Field=RuleField.GrossMargin,Operator=RuleOperator.GreaterThan,Value=.1m,Severity=RuleSeverity.High,Weight=1,RecommendationEffect=RecommendationEffect.AllowTieredHybrid};var first=new RuleSet{Name="A",Status=RuleSetStatus.Published,Rules=[low,high]};var second=new RuleSet{Name="B",Status=RuleSetStatus.Published,Rules=[high,low]};var a=DealRecommendationEngine.Recommend(Economics(),Evaluation(),first,Settings());var b=DealRecommendationEngine.Recommend(Economics(),Evaluation(),second,Settings());Assert.Equal(DealType.RetainerPlusRevenueShare,a.RecommendedDealType);Assert.Equal(a.RecommendedDealType,b.RecommendedDealType);}
    [Fact] public void Ruleset_published_is_immutable()=>Assert.Throws<InvalidOperationException>(()=>RuleSetVersioning.EnsureEditable(new RuleSet{Name="Rules",Status=RuleSetStatus.Published}));
    [Fact] public void Ruleset_clone_increments_version_and_copies_rules(){var source=Rules();var clone=RuleSetVersioning.Clone(source);Assert.Equal(2,clone.Version);Assert.Equal(RuleSetStatus.Draft,clone.Status);Assert.Equal(source.Rules.Count,clone.Rules.Count);Assert.NotEqual(source.Rules[0].Id,clone.Rules[0].Id);}
    [Fact] public void Between_rule_matches_lower_and_excludes_upper(){var rule=new Rule{Name="Range",Field=RuleField.GrossMargin,Operator=RuleOperator.Between,Value=.3m,SecondaryValue=.4m};Assert.Single(RuleEvaluator.Evaluate([rule],new(.35m,0,60,4,4,4,1,1,70)));Assert.Empty(RuleEvaluator.Evaluate([rule],new(.4m,0,60,4,4,4,1,1,70)));}
    [Fact] public void Scenario_calculation_is_authoritative(){var x=ScenarioCalculator.Calculate(Scenario());Assert.True(x.OvoFee>0);Assert.True(x.BrandContributionProfit>0);Assert.Equal(DecisionStatus.Accept,x.ScenarioDecision);}
    [Fact] public void Scenario_channel_rates_blend_fee()
    {
        var s = Scenario(); s.CommissionModel = DealType.FlatRevenueShare; s.ReturnRate = 0;
        s.RevenueChannels = [new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 600_000, RevenueShareRate = .05m },
            new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 400_000, RevenueShareRate = .03m }];
        var x = ScenarioCalculator.Calculate(s);
        Assert.Equal(42_000, x.OvoFee);
        Assert.Equal(.042m, x.EffectiveCommissionRate);
        Assert.Equal(2, x.ChannelShares.Count);
        Assert.Equal(30_000, x.ChannelShares[0].Fee);
        Assert.Equal(12_000, x.ChannelShares[1].Fee);
    }
    [Fact] public void Scenario_channel_rate_falls_back_to_general_rate()
    {
        var s = Scenario(); s.CommissionModel = DealType.FlatRevenueShare; s.ReturnRate = 0; s.RevenueShareRate = .05m;
        s.RevenueChannels = [new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 600_000, RevenueShareRate = .04m },
            new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 400_000 }];
        var x = ScenarioCalculator.Calculate(s);
        Assert.Equal(44_000, x.OvoFee);
    }
    [Fact] public void Scenario_channel_retainer_applies_at_total_level()
    {
        var s = Scenario(); s.CommissionModel = DealType.RetainerPlusRevenueShare; s.ReturnRate = 0; s.MonthlyRetainer = 10_000;
        s.RevenueChannels = [new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 600_000, RevenueShareRate = .05m },
            new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 400_000, RevenueShareRate = .03m }];
        Assert.Equal(52_000, ScenarioCalculator.Calculate(s).OvoFee);
    }
    [Fact] public void Scenario_channel_rates_ignored_for_unsupported_model()
    {
        var plain = Scenario(); plain.CommissionModel = DealType.TieredRevenueShare;
        var channeled = Scenario(); channeled.CommissionModel = DealType.TieredRevenueShare;
        channeled.RevenueChannels = [new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 600_000, RevenueShareRate = .10m },
            new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 400_000, RevenueShareRate = .10m }];
        var x = ScenarioCalculator.Calculate(channeled);
        Assert.Equal(ScenarioCalculator.Calculate(plain).OvoFee, x.OvoFee);
        Assert.Empty(x.ChannelShares);
    }
    [Fact] public void Scenario_without_channel_rates_has_no_shares()
    {
        var s = Scenario();
        s.RevenueChannels = [new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 600_000 },
            new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 400_000 }];
        var x = ScenarioCalculator.Calculate(s);
        Assert.Empty(x.ChannelShares);
        Assert.True(x.OvoFee > 0);
    }
    [Fact] public void Scenario_channel_zero_revenue_row_is_safe()
    {
        var s = Scenario(); s.CommissionModel = DealType.FlatRevenueShare; s.ReturnRate = 0;
        s.RevenueChannels = [new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 0, RevenueShareRate = .05m },
            new ScenarioRevenueChannel { SalesChannelId = Guid.NewGuid(), MonthlyRevenue = 400_000, RevenueShareRate = .03m }];
        Assert.Equal(12_000, ScenarioCalculator.Calculate(s).OvoFee);
    }
    [Fact] public void Deal_comparison_recommends_highest_deterministic_score(){var deals=new[]{Deal(DealType.FlatRevenueShare,rate:.10m),Deal(DealType.MinimumFeePlusRevenueShare,rate:.04m,minimum:45_000)};deals[0].Name="High flat";deals[1].Name="Protected";var x=DealComparisonEngine.Compare(deals,Scenario(),Settings());Assert.True(x[0].Score>=x[1].Score);Assert.NotEmpty(x[0].Reasons);}
    [Fact] public void Monthly_performance_calculates_derived_values(){var p=Performance();var result=MonthlyPerformanceCalculator.Calculate(p,Deal(DealType.FlatRevenueShare,rate:.05m));Assert.Equal(800_000,p.NetRevenue);Assert.Equal(200_000,p.TotalAdSpend);Assert.Equal(result.FinalFee,p.OvoFee);}
    [Fact] public void Locked_period_cannot_be_edited()=>Assert.False(MonthlyCloseWorkflow.CanEdit(MonthlyPerformanceStatus.Locked));
    [Theory] [InlineData(MonthlyPerformanceStatus.Draft,true)] [InlineData(MonthlyPerformanceStatus.UnderReview,false)] [InlineData(MonthlyPerformanceStatus.Approved,false)] [InlineData(MonthlyPerformanceStatus.Locked,false)] [InlineData(MonthlyPerformanceStatus.Invoiced,false)] [InlineData(MonthlyPerformanceStatus.Paid,false)] public void Adjustments_require_a_draft_period(MonthlyPerformanceStatus status,bool expected)=>Assert.Equal(expected,MonthlyCloseWorkflow.CanAdjust(status));
    [Theory] [InlineData(MonthlyPerformanceStatus.Locked,true)] [InlineData(MonthlyPerformanceStatus.Invoiced,false)] [InlineData(MonthlyPerformanceStatus.Paid,false)] public void Only_uninvoiced_locked_period_can_be_unlocked(MonthlyPerformanceStatus status,bool expected)=>Assert.Equal(expected,MonthlyCloseWorkflow.CanUnlock(status));
    [Theory] [InlineData(MonthlyPerformanceStatus.Draft,MonthlyPerformanceStatus.UnderReview,true)] [InlineData(MonthlyPerformanceStatus.Draft,MonthlyPerformanceStatus.Locked,false)] [InlineData(MonthlyPerformanceStatus.Locked,MonthlyPerformanceStatus.Invoiced,true)] public void Monthly_close_transitions_are_guarded(MonthlyPerformanceStatus from,MonthlyPerformanceStatus to,bool expected)=>Assert.Equal(expected,MonthlyCloseWorkflow.CanTransition(from,to));
    [Fact] public void Portfolio_concentration_is_calculated(){Assert.Equal(.50m,PortfolioRiskCalculator.LargestShare([50,30,20]));Assert.Equal(.90m,PortfolioRiskCalculator.TopThreeShare([50,20,20,10]));}
    [Fact] public void Channel_rate_recommendation_distributes_minimum_fee()
    {
        // 25.000 iç maliyet x 1,8 = 45.000 asgari ücret; 1.000.000 ciroya oranla %4,5.
        var x = DealRecommendationEngine.Recommend(Economics(), Evaluation(), HybridRules(), Settings(), Channels());
        Assert.Equal(DealType.MinimumFeePlusRevenueShare, x.RecommendedDealType);
        Assert.Equal(2, x.RecommendedChannelShares.Count);
        Assert.Equal(.045m, x.RecommendedChannelShares[0].RecommendedRate);
        Assert.Equal(27_000, x.RecommendedChannelShares[0].EstimatedFee);
        Assert.Equal(18_000, x.RecommendedChannelShares[1].EstimatedFee);
        Assert.Equal(45_000, x.RecommendedChannelShares.Sum(c => c.EstimatedFee));
    }
    [Fact] public void Channel_rate_recommendation_empty_without_channels_support_or_total()
    {
        var tiered = Rules();
        Assert.Empty(DealRecommendationEngine.Recommend(Economics(), Evaluation(), tiered, Settings(), Channels()).RecommendedChannelShares);
        Assert.Empty(DealRecommendationEngine.Recommend(Economics(), Evaluation(), HybridRules(), Settings()).RecommendedChannelShares);
        Assert.Empty(DealRecommendationEngine.Recommend(Economics(), Evaluation(), HybridRules(), Settings(),
            [new ChannelRevenueInput(Guid.NewGuid(), "Boş", 0)]).RecommendedChannelShares);
    }
    [Fact] public void Channel_rate_recommendation_empty_for_missing_inputs()
    {
        var e = Economics(); e.GrossMarginConfidence = DataConfidence.Unknown;
        Assert.Empty(DealRecommendationEngine.Recommend(e, Evaluation(), HybridRules(), Settings(), Channels()).RecommendedChannelShares);
    }
    [Fact] public void Channel_rate_zero_revenue_channel_has_no_fee()
    {
        var x = DealRecommendationEngine.Recommend(Economics(), Evaluation(), HybridRules(), Settings(),
            [new ChannelRevenueInput(Guid.NewGuid(), "Web sitesi", 1_000_000), new ChannelRevenueInput(Guid.NewGuid(), "Boş", 0)]);
        Assert.Equal(.045m, x.RecommendedChannelShares[0].RecommendedRate);
        Assert.Equal(0, x.RecommendedChannelShares[1].EstimatedFee);
    }

    private static Deal Deal(DealType type,decimal rate=0,decimal minimum=0,decimal retainer=0,decimal incremental=0,decimal baseline=0)=>new(){BrandId=Guid.NewGuid(),EvaluationId=Guid.NewGuid(),Name="Option",DealType=type,RevenueShareRate=rate,MinimumMonthlyFee=minimum,MonthlyRetainer=retainer,IncrementalRate=incremental,BaselineRevenue=baseline,EstimatedMonthlyInternalCost=25_000,SetupInvestment=250_000,CommissionTiersJson=JsonSerializer.Serialize(Tiers)};
    private static BrandEconomics Economics()=>new(){AverageMonthlyRevenue=1_000_000,RevenueConfidence=DataConfidence.Verified,GrossMarginRate=.6m,GrossMarginConfidence=DataConfidence.Verified,CogsRate=.4m,CogsConfidence=DataConfidence.Verified,AverageOrderValue=1_000,AovConfidence=DataConfidence.Verified,ReturnRate=.05m,ReturnRateConfidence=DataConfidence.Verified,CurrentAdSpend=200_000,AdSpendConfidence=DataConfidence.Verified,CurrentCac=500,CacConfidence=DataConfidence.Estimated,AverageCustomerLtv=2_000,LtvConfidence=DataConfidence.Estimated,StockCoverageDays=90,StockCoverageConfidence=DataConfidence.Verified,VariableCostRate=.08m};
    private static BrandEvaluation Evaluation()=>new(){BrandId=Guid.NewGuid(),ProductMarketFit=4,GrowthPotential=4,OperationalReadiness=4,CreativeCapability=4,FounderCooperation=4,DataMaturity=4,InternalMonthlyCost=25_000,SetupInvestment=250_000};
    private static GeneralSettings Settings()=>new();
    private static RuleSet Rules()=>new(){Name="OVO Default Rules",Version=1,Status=RuleSetStatus.Published,Rules=[new Rule{Name="Healthy margin",Field=RuleField.GrossMargin,Operator=RuleOperator.GreaterThanOrEqual,Value=.5m,Severity=RuleSeverity.Info,RecommendationEffect=RecommendationEffect.AllowFourToSixPercent}]};
    private static RuleSet HybridRules()=>new(){Name="OVO Hybrid Rules",Version=1,Status=RuleSetStatus.Published,Rules=[new Rule{Name="Kademeli karma",Field=RuleField.GrossMargin,Operator=RuleOperator.GreaterThanOrEqual,Value=.1m,Severity=RuleSeverity.Info,RecommendationEffect=RecommendationEffect.AllowTieredHybrid}]};
    private static List<ChannelRevenueInput> Channels()=> [new ChannelRevenueInput(Guid.NewGuid(), "Web sitesi", 600_000), new ChannelRevenueInput(Guid.NewGuid(), "Pazaryeri", 400_000)];
    private static Scenario Scenario()=>new(){EvaluationId=Guid.NewGuid(),MonthlyRevenue=1_000_000,GrossMarginRate=.65m,AdSpend=200_000,ReturnRate=.05m,AverageOrderValue=1_600,NewCustomers=350,VariableCostRate=.08m,OvoInternalMonthlyCost=25_000,MinimumMonthlyFee=45_000,CommissionModel=DealType.MinimumFeePlusRevenueShare,RevenueShareRate=.05m,TargetBrandContributionMargin=.15m,SetupInvestment=250_000,ContractMonths=24};
    private static MonthlyPerformance Performance()=>new(){BrandId=Guid.NewGuid(),DealId=Guid.NewGuid(),Year=2026,Month=8,GrossSales=1_000_000,Vat=150_000,Refunds=50_000,Orders=600,Sessions=30_000,NewCustomers=300,Cogs=300_000,PaymentFees=20_000,FulfillmentCosts=30_000,MetaSpend=120_000,GoogleSpend=80_000};
}
