using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V9SatisfactionAndReadingIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewedIpAddress",
                schema: "growth",
                table: "PortalReportReadings",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SatisfactionRatings",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatisfactionRatings", x => x.Id);
                    table.CheckConstraint("CK_SatisfactionRatings_Values", "\"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND \"Score\" BETWEEN 1 AND 5 AND length(\"Comment\") <= 1000");
                    table.ForeignKey(
                        name: "FK_SatisfactionRatings_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatisfactionRatings_BrandId_Year_Month",
                schema: "growth",
                table: "SatisfactionRatings",
                columns: new[] { "BrandId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatisfactionRatings",
                schema: "growth");

            migrationBuilder.DropColumn(
                name: "ReviewedIpAddress",
                schema: "growth",
                table: "PortalReportReadings");
        }
    }
}
