using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChannelTotalRevenueShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TotalRevenueShare",
                schema: "growth",
                table: "ScenarioRevenueChannels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TotalRevenueShare",
                schema: "growth",
                table: "DealChannelRates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalRevenueShare",
                schema: "growth",
                table: "ScenarioRevenueChannels");

            migrationBuilder.DropColumn(
                name: "TotalRevenueShare",
                schema: "growth",
                table: "DealChannelRates");
        }
    }
}
