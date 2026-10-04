using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V9AdCampaignSpends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdCampaignSpends",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    CampaignName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Spend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdCampaignSpends", x => x.Id);
                    table.CheckConstraint("CK_AdCampaignSpends_Values", "\"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND \"Platform\" BETWEEN 0 AND 1 AND \"Spend\" >= 0 AND length(\"CampaignName\") BETWEEN 1 AND 300 AND length(\"Currency\") = 3");
                    table.ForeignKey(
                        name: "FK_AdCampaignSpends_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdCampaignSpends_BrandId_Year_Month_Platform_CampaignName",
                schema: "growth",
                table: "AdCampaignSpends",
                columns: new[] { "BrandId", "Year", "Month", "Platform", "CampaignName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdCampaignSpends",
                schema: "growth");
        }
    }
}
