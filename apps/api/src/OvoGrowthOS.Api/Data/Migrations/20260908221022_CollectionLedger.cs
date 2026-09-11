using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CollectionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionAccounts",
                schema: "growth",
                columns: table => new
                {
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivableAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LegacyPaidAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InvoiceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvoiceOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAccounts", x => x.MonthlyPerformanceId);
                    table.CheckConstraint("CK_CollectionAccounts_Amounts", "\"ReceivableAmount\" >= 0 AND \"LegacyPaidAmount\" >= 0 AND \"LegacyPaidAmount\" <= \"ReceivableAmount\"");
                    table.ForeignKey(
                        name: "FK_CollectionAccounts_MonthlyPerformances_MonthlyPerformanceId",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "MonthlyPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPayments",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPayments", x => x.Id);
                    table.CheckConstraint("CK_CollectionPayments_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CollectionPayments_Reference", "length(btrim(\"Reference\")) > 0");
                    table.CheckConstraint("CK_CollectionPayments_Void", "\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0)");
                    table.ForeignKey(
                        name: "FK_CollectionPayments_CollectionAccounts_MonthlyPerformanceId",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "CollectionAccounts",
                        principalColumn: "MonthlyPerformanceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAccounts_DueOn",
                schema: "growth",
                table: "CollectionAccounts",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPayments_MonthlyPerformanceId_PaidOn",
                schema: "growth",
                table: "CollectionPayments",
                columns: new[] { "MonthlyPerformanceId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPayments_Reference",
                schema: "growth",
                table: "CollectionPayments",
                column: "Reference",
                unique: true,
                filter: "\"VoidedAt\" IS NULL");

            // Internal ledger is accessed only through the authorized .NET API and its table owner.
            migrationBuilder.Sql("""
                ALTER TABLE growth."CollectionAccounts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."CollectionPayments" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON growth."CollectionAccounts", growth."CollectionPayments" FROM PUBLIC;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON growth."CollectionAccounts", growth."CollectionPayments" FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON growth."CollectionAccounts", growth."CollectionPayments" FROM authenticated;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionPayments",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "CollectionAccounts",
                schema: "growth");
        }
    }
}
