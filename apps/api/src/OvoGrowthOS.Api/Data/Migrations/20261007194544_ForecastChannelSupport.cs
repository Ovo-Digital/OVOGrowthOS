using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ForecastChannelSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandRevenueChannels",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandRevenueChannels", x => x.Id);
                    table.CheckConstraint("CK_BrandRevenueChannels_Money", "\"MonthlyRevenue\" >= 0");
                    table.ForeignKey(
                        name: "FK_BrandRevenueChannels_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrandRevenueChannels_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "growth",
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioRevenueChannels",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioRevenueChannels", x => x.Id);
                    table.CheckConstraint("CK_ScenarioRevenueChannels_Money", "\"MonthlyRevenue\" >= 0");
                    table.ForeignKey(
                        name: "FK_ScenarioRevenueChannels_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "growth",
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioRevenueChannels_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "growth",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandRevenueChannels_BrandId_SalesChannelId",
                schema: "growth",
                table: "BrandRevenueChannels",
                columns: new[] { "BrandId", "SalesChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandRevenueChannels_SalesChannelId",
                schema: "growth",
                table: "BrandRevenueChannels",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioRevenueChannels_SalesChannelId",
                schema: "growth",
                table: "ScenarioRevenueChannels",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioRevenueChannels_ScenarioId_SalesChannelId",
                schema: "growth",
                table: "ScenarioRevenueChannels",
                columns: new[] { "ScenarioId", "SalesChannelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandRevenueChannels",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "ScenarioRevenueChannels",
                schema: "growth");
        }
    }
}
