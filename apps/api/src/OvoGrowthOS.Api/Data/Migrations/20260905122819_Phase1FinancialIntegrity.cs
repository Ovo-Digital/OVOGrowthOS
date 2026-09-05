using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1FinancialIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "growth",
                table: "PartnershipDeals",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "growth",
                table: "MonthlyPerformances",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<decimal>(
                name: "ConditionalPartnershipScore",
                schema: "growth",
                table: "GeneralSettings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 55m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumDataConfidenceScore",
                schema: "growth",
                table: "GeneralSettings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumPartnershipScore",
                schema: "growth",
                table: "GeneralSettings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 40m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumRecommendedAdSpend",
                schema: "growth",
                table: "GeneralSettings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 150000m);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "growth",
                table: "GeneralSettings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "growth",
                table: "Evaluations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_PartnershipDeals_BrandId",
                schema: "growth",
                table: "PartnershipDeals",
                column: "BrandId",
                unique: true,
                filter: "\"Status\" = 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MonthlyPerformances_Counts",
                schema: "growth",
                table: "MonthlyPerformances",
                sql: "\"Orders\" >= 0 AND \"Sessions\" >= 0 AND \"NewCustomers\" >= 0 AND \"ReturningCustomers\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MonthlyPerformances_Money",
                schema: "growth",
                table: "MonthlyPerformances",
                sql: "\"GrossSales\" >= 0 AND \"Vat\" >= 0 AND \"Refunds\" >= 0 AND \"Cancellations\" >= 0 AND \"Chargebacks\" >= 0 AND \"Cogs\" >= 0 AND \"PaymentFees\" >= 0 AND \"FulfillmentCosts\" >= 0 AND \"ShippingSubsidy\" >= 0 AND \"OtherVariableCosts\" >= 0 AND \"MetaSpend\" >= 0 AND \"GoogleSpend\" >= 0 AND \"TikTokSpend\" >= 0 AND \"InfluencerSpend\" >= 0 AND \"OtherAdSpend\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MonthlyPerformances_Month",
                schema: "growth",
                table: "MonthlyPerformances",
                sql: "\"Month\" BETWEEN 1 AND 12");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MonthlyPerformances_Year",
                schema: "growth",
                table: "MonthlyPerformances",
                sql: "\"Year\" BETWEEN 2020 AND 2100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartnershipDeals_BrandId",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MonthlyPerformances_Counts",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MonthlyPerformances_Money",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MonthlyPerformances_Month",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MonthlyPerformances_Year",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropColumn(
                name: "ConditionalPartnershipScore",
                schema: "growth",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "MinimumDataConfidenceScore",
                schema: "growth",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "MinimumPartnershipScore",
                schema: "growth",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "MinimumRecommendedAdSpend",
                schema: "growth",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "growth",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "growth",
                table: "Evaluations");
        }
    }
}
