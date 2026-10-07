using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SalesChannelSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesChannels",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesChannels", x => x.Id);
                    table.CheckConstraint("CK_SalesChannels_Name", "length(btrim(\"Name\")) BETWEEN 2 AND 80");
                    table.ForeignKey(
                        name: "FK_SalesChannels_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealChannelRates",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevenueShareRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealChannelRates", x => x.Id);
                    table.CheckConstraint("CK_DealChannelRates_Rate", "\"RevenueShareRate\" >= 0 AND \"RevenueShareRate\" <= 1");
                    table.ForeignKey(
                        name: "FK_DealChannelRates_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealChannelRates_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "growth",
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyPerformanceChannels",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossSales = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Vat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Refunds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Cancellations = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Chargebacks = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CustomerPaidShipping = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GiftCardTopups = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionableRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OvoFeeShare = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyPerformanceChannels", x => x.Id);
                    table.CheckConstraint("CK_MonthlyPerformanceChannels_Money", "\"GrossSales\" >= 0 AND \"Vat\" >= 0 AND \"Refunds\" >= 0 AND \"Cancellations\" >= 0 AND \"Chargebacks\" >= 0 AND \"CustomerPaidShipping\" >= 0 AND \"GiftCardTopups\" >= 0");
                    table.CheckConstraint("CK_MonthlyPerformanceChannels_Totals", "\"Vat\" + \"Refunds\" + \"Cancellations\" + \"Chargebacks\" + \"CustomerPaidShipping\" + \"GiftCardTopups\" <= \"GrossSales\"");
                    table.ForeignKey(
                        name: "FK_MonthlyPerformanceChannels_MonthlyPerformances_MonthlyPerfo~",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "MonthlyPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlyPerformanceChannels_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "growth",
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealChannelRates_DealId_SalesChannelId",
                schema: "growth",
                table: "DealChannelRates",
                columns: new[] { "DealId", "SalesChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealChannelRates_SalesChannelId",
                schema: "growth",
                table: "DealChannelRates",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPerformanceChannels_MonthlyPerformanceId_SalesChanne~",
                schema: "growth",
                table: "MonthlyPerformanceChannels",
                columns: new[] { "MonthlyPerformanceId", "SalesChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPerformanceChannels_SalesChannelId",
                schema: "growth",
                table: "MonthlyPerformanceChannels",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesChannels_BrandId_IsActive",
                schema: "growth",
                table: "SalesChannels",
                columns: new[] { "BrandId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesChannels_BrandId_Name",
                schema: "growth",
                table: "SalesChannels",
                columns: new[] { "BrandId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealChannelRates",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "MonthlyPerformanceChannels",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "SalesChannels",
                schema: "growth");
        }
    }
}
