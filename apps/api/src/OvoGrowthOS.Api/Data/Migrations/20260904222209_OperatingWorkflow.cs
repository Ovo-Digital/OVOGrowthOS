using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OperatingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Rules",
                schema: "growth",
                newName: "LegacyRules",
                newSchema: "growth");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_BrandId_CreatedAt",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Deals",
                schema: "growth",
                table: "Deals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditRecords",
                schema: "growth",
                table: "AuditRecords");

            migrationBuilder.DropIndex(
                name: "IX_AuditRecords_Entity_EntityId_Timestamp",
                schema: "growth",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.RenameTable(
                name: "Deals",
                schema: "growth",
                newName: "PartnershipDeals",
                newSchema: "growth");

            migrationBuilder.RenameTable(
                name: "AuditRecords",
                schema: "growth",
                newName: "AuditTrail",
                newSchema: "growth");

            migrationBuilder.RenameColumn(
                name: "SnapshotJson",
                schema: "growth",
                table: "Evaluations",
                newName: "RuleSnapshotJson");

            migrationBuilder.RenameColumn(
                name: "Score",
                schema: "growth",
                table: "Evaluations",
                newName: "RecommendedTargetMer");

            migrationBuilder.RenameColumn(
                name: "ExplanationJson",
                schema: "growth",
                table: "Evaluations",
                newName: "RecommendationSnapshotJson");

            migrationBuilder.RenameColumn(
                name: "SnapshotJson",
                schema: "growth",
                table: "PartnershipDeals",
                newName: "RuleSnapshotJson");

            migrationBuilder.RenameIndex(
                name: "IX_Deals_BrandId_Status",
                schema: "growth",
                table: "PartnershipDeals",
                newName: "IX_PartnershipDeals_BrandId_Status");

            migrationBuilder.RenameColumn(
                name: "UserEmail",
                schema: "growth",
                table: "AuditTrail",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                schema: "growth",
                table: "AuditTrail",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "OldValue",
                schema: "growth",
                table: "AuditTrail",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "NewValue",
                schema: "growth",
                table: "AuditTrail",
                newName: "OldValueJson");

            migrationBuilder.RenameColumn(
                name: "Entity",
                schema: "growth",
                table: "AuditTrail",
                newName: "NewValueJson");

            migrationBuilder.AddColumn<string>(
                name: "CalculationSnapshotJson",
                schema: "growth",
                table: "Evaluations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "growth",
                table: "Evaluations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DataConfidenceScore",
                schema: "growth",
                table: "Evaluations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InputSnapshotJson",
                schema: "growth",
                table: "Evaluations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PartnershipScore",
                schema: "growth",
                table: "Evaluations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedAdSpend",
                schema: "growth",
                table: "Evaluations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RecommendedContractMonths",
                schema: "growth",
                table: "Evaluations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedSetupInvestment",
                schema: "growth",
                table: "Evaluations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedTargetContributionMargin",
                schema: "growth",
                table: "Evaluations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RuleSetId",
                schema: "growth",
                table: "Evaluations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "growth",
                table: "Evaluations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "growth",
                table: "Evaluations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "AdSpendConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AovConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCustomerLtv",
                schema: "growth",
                table: "BrandEconomics",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CacConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CogsConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CogsRate",
                schema: "growth",
                table: "BrandEconomics",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentCac",
                schema: "growth",
                table: "BrandEconomics",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GrossMarginConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LtvConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnRateConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevenueConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StockCoverageConfidence",
                schema: "growth",
                table: "BrandEconomics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                ALTER TABLE growth."PartnershipDeals"
                ALTER COLUMN "Status" TYPE integer USING CASE "Status"
                    WHEN 'Draft' THEN 0 WHEN 'Proposed' THEN 2 WHEN 'Negotiation' THEN 3
                    WHEN 'Accepted' THEN 4 WHEN 'Rejected' THEN 5 WHEN 'Active' THEN 6
                    WHEN 'Expired' THEN 7 WHEN 'Terminated' THEN 8 ELSE 0 END;
                """);

            migrationBuilder.AddColumn<int>(
                name: "BaselineCalculationMethod",
                schema: "growth",
                table: "PartnershipDeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BaselinePeriodEnd",
                schema: "growth",
                table: "PartnershipDeals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BaselinePeriodStart",
                schema: "growth",
                table: "PartnershipDeals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommissionTiersJson",
                schema: "growth",
                table: "PartnershipDeals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConditionsSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "growth",
                table: "PartnershipDeals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvaluationId",
                schema: "growth",
                table: "PartnershipDeals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "EvaluationSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FinancialSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitShareRate",
                schema: "growth",
                table: "PartnershipDeals",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                schema: "growth",
                table: "PartnershipDeals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "growth",
                table: "PartnershipDeals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                schema: "growth",
                table: "AuditTrail",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE growth."Evaluations"
                SET "PartnershipScore" = "RecommendedTargetMer",
                    "RecommendedTargetMer" = 0,
                    "Status" = CASE WHEN "CompletedAt" IS NULL THEN 0 ELSE 3 END,
                    "UpdatedAt" = "CreatedAt";
                UPDATE growth."AuditTrail"
                SET "EntityType" = "NewValueJson",
                    "NewValueJson" = "OldValueJson",
                    "OldValueJson" = "Reason",
                    "Reason" = '';
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PartnershipDeals",
                schema: "growth",
                table: "PartnershipDeals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditTrail",
                schema: "growth",
                table: "AuditTrail",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "GeneralSettings",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultCurrency = table.Column<string>(type: "text", nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DefaultContractMonths = table.Column<int>(type: "integer", nullable: false),
                    DefaultSetupInvestment = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetOvoGrossMargin = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetBrandContributionMargin = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumFeeMultiplier = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExistingRevenueThreshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ConcentrationRiskThreshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DefaultRuleSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyPerformances",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    GrossSales = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Vat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Refunds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Cancellations = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Chargebacks = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CustomerPaidShipping = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GiftCardTopups = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionableRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Orders = table.Column<int>(type: "integer", nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false),
                    NewCustomers = table.Column<int>(type: "integer", nullable: false),
                    ReturningCustomers = table.Column<int>(type: "integer", nullable: false),
                    Cogs = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PaymentFees = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FulfillmentCosts = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ShippingSubsidy = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OtherVariableCosts = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MetaSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GoogleSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TikTokSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InfluencerSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OtherAdSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAdSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ContributionBeforeMarketing = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ContributionBeforeOvo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BrandContributionProfit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Aov = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ConversionRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Mer = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Cac = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReturnRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OvoFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OvoInternalCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OvoGrossProfit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OvoMargin = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionBreakdownJson = table.Column<string>(type: "text", nullable: false),
                    CommissionStatus = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyPerformances_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlyPerformances_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartnershipConditions",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnershipConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnershipConditions_Evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalSchema: "growth",
                        principalTable: "Evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartnershipConditions_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleSets",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    MonthlyRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossMarginRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AdSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReturnRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageOrderValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NewCustomers = table.Column<int>(type: "integer", nullable: false),
                    VariableCostRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OvoInternalMonthlyCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumMonthlyFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionModel = table.Column<int>(type: "integer", nullable: false),
                    RevenueShareRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MonthlyRetainer = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BaselineRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IncrementalRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProfitShareRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionTiersJson = table.Column<string>(type: "text", nullable: false),
                    TargetBrandContributionMargin = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SetupInvestment = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ContractMonths = table.Column<int>(type: "integer", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenarios_Evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalSchema: "growth",
                        principalTable: "Evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionAdjustments",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustments_MonthlyPerformances_MonthlyPerformanc~",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "MonthlyPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleDefinitions",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SecondaryValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendationEffect = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleDefinitions_RuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalSchema: "growth",
                        principalTable: "RuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_BrandId_Status_CreatedAt",
                schema: "growth",
                table: "Evaluations",
                columns: new[] { "BrandId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrail_EntityType_EntityId_CreatedAt",
                schema: "growth",
                table: "AuditTrail",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustments_MonthlyPerformanceId",
                schema: "growth",
                table: "CommissionAdjustments",
                column: "MonthlyPerformanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPerformances_BrandId_Year_Month",
                schema: "growth",
                table: "MonthlyPerformances",
                columns: new[] { "BrandId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPerformances_DealId",
                schema: "growth",
                table: "MonthlyPerformances",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnershipConditions_DealId",
                schema: "growth",
                table: "PartnershipConditions",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnershipConditions_EvaluationId",
                schema: "growth",
                table: "PartnershipConditions",
                column: "EvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleDefinitions_RuleSetId",
                schema: "growth",
                table: "RuleDefinitions",
                column: "RuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSets_Name_Version",
                schema: "growth",
                table: "RuleSets",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_EvaluationId_Name",
                schema: "growth",
                table: "Scenarios",
                columns: new[] { "EvaluationId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_PartnershipDeals_Brands_BrandId",
                schema: "growth",
                table: "PartnershipDeals",
                column: "BrandId",
                principalSchema: "growth",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartnershipDeals_Brands_BrandId",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropTable(
                name: "CommissionAdjustments",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "GeneralSettings",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PartnershipConditions",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "RuleDefinitions",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "Scenarios",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "MonthlyPerformances",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "RuleSets",
                schema: "growth");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_BrandId_Status_CreatedAt",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PartnershipDeals",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditTrail",
                schema: "growth",
                table: "AuditTrail");

            migrationBuilder.DropIndex(
                name: "IX_AuditTrail_EntityType_EntityId_CreatedAt",
                schema: "growth",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "CalculationSnapshotJson",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "DataConfidenceScore",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "InputSnapshotJson",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "PartnershipScore",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "RecommendedAdSpend",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "RecommendedContractMonths",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "RecommendedSetupInvestment",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "RecommendedTargetContributionMargin",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "RuleSetId",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "growth",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "AdSpendConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "AovConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "AverageCustomerLtv",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "CacConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "CogsConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "CogsRate",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "CurrentCac",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "GrossMarginConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "LtvConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "ReturnRateConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "RevenueConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "StockCoverageConfidence",
                schema: "growth",
                table: "BrandEconomics");

            migrationBuilder.DropColumn(
                name: "BaselineCalculationMethod",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "BaselinePeriodEnd",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "BaselinePeriodStart",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "CommissionSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "CommissionTiersJson",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "ConditionsSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "EvaluationId",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "EvaluationSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "FinancialSnapshotJson",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "ProfitShareRate",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "EntityType",
                schema: "growth",
                table: "AuditTrail");

            migrationBuilder.RenameTable(
                name: "PartnershipDeals",
                schema: "growth",
                newName: "Deals",
                newSchema: "growth");

            migrationBuilder.RenameTable(
                name: "AuditTrail",
                schema: "growth",
                newName: "AuditRecords",
                newSchema: "growth");

            migrationBuilder.RenameColumn(
                name: "RuleSnapshotJson",
                schema: "growth",
                table: "Evaluations",
                newName: "SnapshotJson");

            migrationBuilder.RenameColumn(
                name: "RecommendedTargetMer",
                schema: "growth",
                table: "Evaluations",
                newName: "Score");

            migrationBuilder.RenameColumn(
                name: "RecommendationSnapshotJson",
                schema: "growth",
                table: "Evaluations",
                newName: "ExplanationJson");

            migrationBuilder.RenameColumn(
                name: "RuleSnapshotJson",
                schema: "growth",
                table: "Deals",
                newName: "SnapshotJson");

            migrationBuilder.RenameIndex(
                name: "IX_PartnershipDeals_BrandId_Status",
                schema: "growth",
                table: "Deals",
                newName: "IX_Deals_BrandId_Status");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "growth",
                table: "AuditRecords",
                newName: "UserEmail");

            migrationBuilder.RenameColumn(
                name: "Reason",
                schema: "growth",
                table: "AuditRecords",
                newName: "OldValue");

            migrationBuilder.RenameColumn(
                name: "OldValueJson",
                schema: "growth",
                table: "AuditRecords",
                newName: "NewValue");

            migrationBuilder.RenameColumn(
                name: "NewValueJson",
                schema: "growth",
                table: "AuditRecords",
                newName: "Entity");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "growth",
                table: "AuditRecords",
                newName: "Timestamp");

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                schema: "growth",
                table: "Evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                ALTER TABLE growth."Deals"
                ALTER COLUMN "Status" TYPE text USING CASE "Status"
                    WHEN 0 THEN 'Draft' WHEN 1 THEN 'Draft' WHEN 2 THEN 'Proposed'
                    WHEN 3 THEN 'Negotiation' WHEN 4 THEN 'Accepted' WHEN 5 THEN 'Rejected'
                    WHEN 6 THEN 'Active' WHEN 7 THEN 'Expired' WHEN 8 THEN 'Terminated' ELSE 'Draft' END;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Deals",
                schema: "growth",
                table: "Deals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditRecords",
                schema: "growth",
                table: "AuditRecords",
                column: "Id");

            migrationBuilder.RenameTable(
                name: "LegacyRules",
                schema: "growth",
                newName: "Rules",
                newSchema: "growth");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_BrandId_CreatedAt",
                schema: "growth",
                table: "Evaluations",
                columns: new[] { "BrandId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_Entity_EntityId_Timestamp",
                schema: "growth",
                table: "AuditRecords",
                columns: new[] { "Entity", "EntityId", "Timestamp" });

        }
    }
}
