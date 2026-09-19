using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortalCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                schema: "growth",
                table: "PortalQuestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                schema: "growth",
                table: "PortalQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "growth",
                table: "PortalQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PortalDataRequests",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalDataRequests", x => x.Id);
                    table.CheckConstraint("CK_PortalDataRequests_Values", "\"Revision\" > 0 AND \"Status\" BETWEEN 0 AND 2 AND length(btrim(\"Title\")) BETWEEN 1 AND 200 AND length(btrim(\"Instructions\")) BETWEEN 1 AND 2000 AND (\"DueOn\" IS NULL OR EXTRACT(YEAR FROM \"DueOn\") BETWEEN 2020 AND 2100)");
                    table.ForeignKey(
                        name: "FK_PortalDataRequests_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalMessages",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStaff = table.Column<bool>(type: "boolean", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalMessages", x => x.Id);
                    table.CheckConstraint("CK_PortalMessages_Content", "length(btrim(\"Text\")) BETWEEN 1 AND 4000 AND \"Sequence\" > 1");
                    table.ForeignKey(
                        name: "FK_PortalMessages_PortalQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "growth",
                        principalTable: "PortalQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalMessages_UserAccounts_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalReportReadings",
                schema: "growth",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalReportReadings", x => new { x.ReportId, x.UserId });
                    table.CheckConstraint("CK_PortalReportReadings_Dates", "\"LastViewedAt\" >= \"FirstViewedAt\" AND (\"ReviewedAt\" IS NULL OR \"ReviewedAt\" >= \"FirstViewedAt\")");
                    table.ForeignKey(
                        name: "FK_PortalReportReadings_PortalAccesses_UserId_BrandId",
                        columns: x => new { x.UserId, x.BrandId },
                        principalSchema: "growth",
                        principalTable: "PortalAccesses",
                        principalColumns: new[] { "UserId", "BrandId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReportReadings_PortalReports_ReportId_BrandId",
                        columns: x => new { x.ReportId, x.BrandId },
                        principalSchema: "growth",
                        principalTable: "PortalReports",
                        principalColumns: new[] { "Id", "BrandId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalQuestions_OwnerId",
                schema: "growth",
                table: "PortalQuestions",
                column: "OwnerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PortalQuestions_Tracking",
                schema: "growth",
                table: "PortalQuestions",
                sql: "\"Revision\" > 0 AND (\"Status\" IS NULL OR \"Status\" BETWEEN 0 AND 2)");

            migrationBuilder.CreateIndex(
                name: "IX_PortalDataRequests_BrandId_CreatedAt",
                schema: "growth",
                table: "PortalDataRequests",
                columns: new[] { "BrandId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalMessages_AuthorId",
                schema: "growth",
                table: "PortalMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalMessages_QuestionId_Sequence",
                schema: "growth",
                table: "PortalMessages",
                columns: new[] { "QuestionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalReportReadings_BrandId",
                schema: "growth",
                table: "PortalReportReadings",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReportReadings_ReportId_BrandId",
                schema: "growth",
                table: "PortalReportReadings",
                columns: new[] { "ReportId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalReportReadings_UserId_BrandId",
                schema: "growth",
                table: "PortalReportReadings",
                columns: new[] { "UserId", "BrandId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PortalQuestions_UserAccounts_OwnerId",
                schema: "growth",
                table: "PortalQuestions",
                column: "OwnerId",
                principalSchema: "growth",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortalQuestions_UserAccounts_OwnerId",
                schema: "growth",
                table: "PortalQuestions");

            migrationBuilder.DropTable(
                name: "PortalDataRequests",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PortalMessages",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PortalReportReadings",
                schema: "growth");

            migrationBuilder.DropIndex(
                name: "IX_PortalQuestions_OwnerId",
                schema: "growth",
                table: "PortalQuestions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PortalQuestions_Tracking",
                schema: "growth",
                table: "PortalQuestions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "growth",
                table: "PortalQuestions");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "growth",
                table: "PortalQuestions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "growth",
                table: "PortalQuestions");
        }
    }
}
