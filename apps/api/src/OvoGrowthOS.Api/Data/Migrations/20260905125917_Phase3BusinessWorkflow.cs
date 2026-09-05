using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3BusinessWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RenewalOfDealId",
                schema: "growth",
                table: "PartnershipDeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                schema: "growth",
                table: "PartnershipDeals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceUrl",
                schema: "growth",
                table: "PartnershipConditions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResolutionReason",
                schema: "growth",
                table: "PartnershipConditions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                schema: "growth",
                table: "PartnershipConditions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedBy",
                schema: "growth",
                table: "PartnershipConditions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                schema: "growth",
                table: "MonthlyPerformances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                schema: "growth",
                table: "MonthlyPerformances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedBy",
                schema: "growth",
                table: "MonthlyPerformances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                schema: "growth",
                table: "MonthlyPerformances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                schema: "growth",
                table: "MonthlyPerformances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DealTemplates",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    DealType = table.Column<int>(type: "integer", nullable: false),
                    ContractMonths = table.Column<int>(type: "integer", nullable: false),
                    MonthlyRetainer = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumMonthlyFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RevenueShareRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IncrementalRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProfitShareRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionTiersJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAttachments",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAttachments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealTemplates_Enabled_DisplayOrder",
                schema: "growth",
                table: "DealTemplates",
                columns: new[] { "Enabled", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAttachments_EntityType_EntityId_CreatedAt",
                schema: "growth",
                table: "DocumentAttachments",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealTemplates",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "DocumentAttachments",
                schema: "growth");

            migrationBuilder.DropColumn(
                name: "RenewalOfDealId",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                schema: "growth",
                table: "PartnershipDeals");

            migrationBuilder.DropColumn(
                name: "EvidenceUrl",
                schema: "growth",
                table: "PartnershipConditions");

            migrationBuilder.DropColumn(
                name: "ResolutionReason",
                schema: "growth",
                table: "PartnershipConditions");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                schema: "growth",
                table: "PartnershipConditions");

            migrationBuilder.DropColumn(
                name: "ResolvedBy",
                schema: "growth",
                table: "PartnershipConditions");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropColumn(
                name: "PreparedBy",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                schema: "growth",
                table: "MonthlyPerformances");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                schema: "growth",
                table: "MonthlyPerformances");
        }
    }
}
