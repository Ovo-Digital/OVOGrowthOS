using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalAccesses",
                schema: "growth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAccesses", x => x.UserId);
                    table.UniqueConstraint("AK_PortalAccesses_UserId_BrandId", x => new { x.UserId, x.BrandId });
                    table.ForeignKey(
                        name: "FK_PortalAccesses_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalAccesses_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalDocumentShares",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalDocumentShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalDocumentShares_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalDocumentShares_DocumentAttachments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "growth",
                        principalTable: "DocumentAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalReports",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalReports", x => x.Id);
                    table.UniqueConstraint("AK_PortalReports_Id_BrandId", x => new { x.Id, x.BrandId });
                    table.CheckConstraint("CK_PortalReports_Period", "\"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_PortalReports_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReports_MonthlyPerformances_PerformanceId",
                        column: x => x.PerformanceId,
                        principalSchema: "growth",
                        principalTable: "MonthlyPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalQuestions",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalQuestions_PortalAccesses_UserId_BrandId",
                        columns: x => new { x.UserId, x.BrandId },
                        principalSchema: "growth",
                        principalTable: "PortalAccesses",
                        principalColumns: new[] { "UserId", "BrandId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalQuestions_PortalReports_ReportId_BrandId",
                        columns: x => new { x.ReportId, x.BrandId },
                        principalSchema: "growth",
                        principalTable: "PortalReports",
                        principalColumns: new[] { "Id", "BrandId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccesses_BrandId",
                schema: "growth",
                table: "PortalAccesses",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalDocumentShares_BrandId",
                schema: "growth",
                table: "PortalDocumentShares",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalDocumentShares_DocumentId",
                schema: "growth",
                table: "PortalDocumentShares",
                column: "DocumentId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PortalQuestions_BrandId_CreatedAt",
                schema: "growth",
                table: "PortalQuestions",
                columns: new[] { "BrandId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalQuestions_ReportId_BrandId",
                schema: "growth",
                table: "PortalQuestions",
                columns: new[] { "ReportId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalQuestions_UserId_BrandId",
                schema: "growth",
                table: "PortalQuestions",
                columns: new[] { "UserId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalReports_BrandId_Year_Month_Version",
                schema: "growth",
                table: "PortalReports",
                columns: new[] { "BrandId", "Year", "Month", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalReports_PerformanceId",
                schema: "growth",
                table: "PortalReports",
                column: "PerformanceId");

            // Access is through the scoped .NET API, never through the public Supabase Data API.
            migrationBuilder.Sql("""
                ALTER TABLE growth."PortalAccesses" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."PortalReports" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."PortalDocumentShares" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."PortalQuestions" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON growth."PortalAccesses", growth."PortalReports", growth."PortalDocumentShares", growth."PortalQuestions" FROM PUBLIC;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON growth."PortalAccesses", growth."PortalReports", growth."PortalDocumentShares", growth."PortalQuestions" FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON growth."PortalAccesses", growth."PortalReports", growth."PortalDocumentShares", growth."PortalQuestions" FROM authenticated;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalDocumentShares",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PortalQuestions",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PortalAccesses",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PortalReports",
                schema: "growth");
        }
    }
}
