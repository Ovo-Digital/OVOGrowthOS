using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChannelIncrementalBaselines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaselineRevenue",
                schema: "growth",
                table: "ScenarioRevenueChannels",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaselineRevenue",
                schema: "growth",
                table: "DealChannelRates",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineRevenue",
                schema: "growth",
                table: "ScenarioRevenueChannels");

            migrationBuilder.DropColumn(
                name: "BaselineRevenue",
                schema: "growth",
                table: "DealChannelRates");
        }
    }
}
