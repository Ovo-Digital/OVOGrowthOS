using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V9PortalPeriodSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalPeriodSubmissions",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    GrossSales = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Refunds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MetaSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    GoogleSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalPeriodSubmissions", x => x.Id);
                    table.CheckConstraint("CK_PortalPeriodSubmissions_Values", "\"Revision\" > 0 AND \"Month\" BETWEEN 1 AND 12 AND \"Year\" BETWEEN 2020 AND 2100 AND length(btrim(\"Note\")) <= 1000 AND (\"GrossSales\" IS NULL OR \"GrossSales\" BETWEEN 0 AND 1000000000000) AND (\"Refunds\" IS NULL OR \"Refunds\" BETWEEN 0 AND 1000000000000) AND (\"MetaSpend\" IS NULL OR \"MetaSpend\" BETWEEN 0 AND 1000000000000) AND (\"GoogleSpend\" IS NULL OR \"GoogleSpend\" BETWEEN 0 AND 1000000000000)");
                    table.ForeignKey(
                        name: "FK_PortalPeriodSubmissions_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalPeriodSubmissions_BrandId_Year_Month",
                schema: "growth",
                table: "PortalPeriodSubmissions",
                columns: new[] { "BrandId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalPeriodSubmissions",
                schema: "growth");
        }
    }
}
