using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BrandReportSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ScheduledReportEnabled",
                schema: "growth",
                table: "BrandMailPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledSendDay",
                schema: "growth",
                table: "BrandMailPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledSendHour",
                schema: "growth",
                table: "BrandMailPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 9);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BrandMailPolicies_Schedule",
                schema: "growth",
                table: "BrandMailPolicies",
                sql: "\"ScheduledSendDay\" BETWEEN 1 AND 31 AND \"ScheduledSendHour\" BETWEEN 0 AND 23");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BrandMailPolicies_Schedule",
                schema: "growth",
                table: "BrandMailPolicies");

            migrationBuilder.DropColumn(
                name: "ScheduledReportEnabled",
                schema: "growth",
                table: "BrandMailPolicies");

            migrationBuilder.DropColumn(
                name: "ScheduledSendDay",
                schema: "growth",
                table: "BrandMailPolicies");

            migrationBuilder.DropColumn(
                name: "ScheduledSendHour",
                schema: "growth",
                table: "BrandMailPolicies");
        }
    }
}
