using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6PipelineTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LostBy",
                schema: "growth",
                table: "BrandFollowUps",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "LostOn",
                schema: "growth",
                table: "BrandFollowUps",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LostReason",
                schema: "growth",
                table: "BrandFollowUps",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SourceChannel",
                schema: "growth",
                table: "BrandFollowUps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceNote",
                schema: "growth",
                table: "BrandFollowUps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BrandStageHistories",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    EntryKnown = table.Column<bool>(type: "boolean", nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnteredBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ExitedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandStageHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandStageHistories_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandFollowUps_LostOn",
                schema: "growth",
                table: "BrandFollowUps",
                column: "LostOn");

            migrationBuilder.CreateIndex(
                name: "IX_BrandStageHistories_BrandId_EnteredAt",
                schema: "growth",
                table: "BrandStageHistories",
                columns: new[] { "BrandId", "EnteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BrandStageHistories_Stage_ExitedAt",
                schema: "growth",
                table: "BrandStageHistories",
                columns: new[] { "Stage", "ExitedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandStageHistories",
                schema: "growth");

            migrationBuilder.DropIndex(
                name: "IX_BrandFollowUps_LostOn",
                schema: "growth",
                table: "BrandFollowUps");

            migrationBuilder.DropColumn(
                name: "LostBy",
                schema: "growth",
                table: "BrandFollowUps");

            migrationBuilder.DropColumn(
                name: "LostOn",
                schema: "growth",
                table: "BrandFollowUps");

            migrationBuilder.DropColumn(
                name: "LostReason",
                schema: "growth",
                table: "BrandFollowUps");

            migrationBuilder.DropColumn(
                name: "SourceChannel",
                schema: "growth",
                table: "BrandFollowUps");

            migrationBuilder.DropColumn(
                name: "SourceNote",
                schema: "growth",
                table: "BrandFollowUps");
        }
    }
}
