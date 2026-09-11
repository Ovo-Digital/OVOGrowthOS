using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OperatingCostsAndInvestment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvestmentAccounts",
                schema: "growth",
                columns: table => new
                {
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentAccounts", x => x.DealId);
                    table.ForeignKey(
                        name: "FK_InvestmentAccounts_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCostAccounts",
                schema: "growth",
                columns: table => new
                {
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "text", nullable: false),
                    LastReviewReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCostAccounts", x => x.MonthlyPerformanceId);
                    table.ForeignKey(
                        name: "FK_ServiceCostAccounts_MonthlyPerformances_MonthlyPerformanceId",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "MonthlyPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentEntries",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentEntries", x => x.Id);
                    table.CheckConstraint("CK_InvestmentEntries_Amount", "\"Amount\" > 0 AND \"Kind\" IN (0, 1)");
                    table.CheckConstraint("CK_InvestmentEntries_Reference", "length(btrim(\"Reference\")) > 0");
                    table.CheckConstraint("CK_InvestmentEntries_Void", "\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0)");
                    table.ForeignKey(
                        name: "FK_InvestmentEntries_InvestmentAccounts_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "InvestmentAccounts",
                        principalColumn: "DealId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCostEntries",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    HourlyCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    IncurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCostEntries", x => x.Id);
                    table.CheckConstraint("CK_ServiceCostEntries_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_ServiceCostEntries_Kind", "(\"Kind\" = 0 AND \"Hours\" IS NULL AND \"HourlyCost\" IS NULL) OR (\"Kind\" = 1 AND \"Hours\" IS NOT NULL AND \"HourlyCost\" IS NOT NULL AND \"Hours\" > 0 AND \"HourlyCost\" > 0 AND \"Amount\" = round(\"Hours\" * \"HourlyCost\", 4))");
                    table.CheckConstraint("CK_ServiceCostEntries_Reference", "length(btrim(\"Reference\")) > 0");
                    table.CheckConstraint("CK_ServiceCostEntries_Void", "\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0)");
                    table.ForeignKey(
                        name: "FK_ServiceCostEntries_ServiceCostAccounts_MonthlyPerformanceId",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "ServiceCostAccounts",
                        principalColumn: "MonthlyPerformanceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCostReviews",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Complete = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCostReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceCostReviews_ServiceCostAccounts_MonthlyPerformanceId",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "ServiceCostAccounts",
                        principalColumn: "MonthlyPerformanceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEntries_DealId_OccurredOn",
                schema: "growth",
                table: "InvestmentEntries",
                columns: new[] { "DealId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentEntries_Reference",
                schema: "growth",
                table: "InvestmentEntries",
                column: "Reference",
                unique: true,
                filter: "\"VoidedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCostEntries_MonthlyPerformanceId_IncurredOn",
                schema: "growth",
                table: "ServiceCostEntries",
                columns: new[] { "MonthlyPerformanceId", "IncurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCostEntries_Reference",
                schema: "growth",
                table: "ServiceCostEntries",
                column: "Reference",
                unique: true,
                filter: "\"VoidedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCostReviews_MonthlyPerformanceId_CreatedAt",
                schema: "growth",
                table: "ServiceCostReviews",
                columns: new[] { "MonthlyPerformanceId", "CreatedAt" });

            // Actual cost and recovery data is private to authorized internal API roles.
            migrationBuilder.Sql("""
                ALTER TABLE growth."ServiceCostAccounts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."ServiceCostEntries" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."ServiceCostReviews" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."InvestmentAccounts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."InvestmentEntries" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON growth."ServiceCostAccounts", growth."ServiceCostEntries", growth."ServiceCostReviews", growth."InvestmentAccounts", growth."InvestmentEntries" FROM PUBLIC;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON growth."ServiceCostAccounts", growth."ServiceCostEntries", growth."ServiceCostReviews", growth."InvestmentAccounts", growth."InvestmentEntries" FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON growth."ServiceCostAccounts", growth."ServiceCostEntries", growth."ServiceCostReviews", growth."InvestmentAccounts", growth."InvestmentEntries" FROM authenticated;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentEntries",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "ServiceCostEntries",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "ServiceCostReviews",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "InvestmentAccounts",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "ServiceCostAccounts",
                schema: "growth");
        }
    }
}
