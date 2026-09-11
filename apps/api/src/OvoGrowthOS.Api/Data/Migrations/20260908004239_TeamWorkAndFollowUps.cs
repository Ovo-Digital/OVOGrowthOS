using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TeamWorkAndFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandContactNotes",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandContactNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandContactNotes_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrandFollowUps",
                schema: "growth",
                columns: table => new
                {
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    WaitingReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NextContactOn = table.Column<DateOnly>(type: "date", nullable: true),
                    NextStep = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandFollowUps", x => x.BrandId);
                    table.ForeignKey(
                        name: "FK_BrandFollowUps_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrandFollowUps_UserAccounts_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkTasks",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Month = table.Column<int>(type: "integer", nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTasks", x => x.Id);
                    table.CheckConstraint("CK_WorkTasks_Target", "(\"Kind\" = 0 AND \"DealId\" IS NULL AND \"Year\" IS NULL AND \"Month\" IS NULL) OR (\"Kind\" = 1 AND \"DealId\" IS NOT NULL AND \"Year\" IS NOT NULL AND \"Month\" IS NOT NULL AND \"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12) OR (\"Kind\" = 2 AND \"DealId\" IS NOT NULL AND \"Year\" IS NULL AND \"Month\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_WorkTasks_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTasks_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTasks_UserAccounts_AssigneeId",
                        column: x => x.AssigneeId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandContactNotes_BrandId_ContactOn_CreatedAt",
                schema: "growth",
                table: "BrandContactNotes",
                columns: new[] { "BrandId", "ContactOn", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BrandFollowUps_OwnerId_NextContactOn",
                schema: "growth",
                table: "BrandFollowUps",
                columns: new[] { "OwnerId", "NextContactOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_AssigneeId_CompletedAt_DueOn",
                schema: "growth",
                table: "WorkTasks",
                columns: new[] { "AssigneeId", "CompletedAt", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_BrandId_CompletedAt_DueOn",
                schema: "growth",
                table: "WorkTasks",
                columns: new[] { "BrandId", "CompletedAt", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_BrandId_Year_Month",
                schema: "growth",
                table: "WorkTasks",
                columns: new[] { "BrandId", "Year", "Month" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_DealId",
                schema: "growth",
                table: "WorkTasks",
                column: "DealId",
                unique: true,
                filter: "\"Kind\" = 2");

            // Internal API tables: no Supabase browser/Data API policies. The application table owner retains access.
            migrationBuilder.Sql("""
                ALTER TABLE growth."WorkTasks" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."BrandFollowUps" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE growth."BrandContactNotes" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON growth."WorkTasks", growth."BrandFollowUps", growth."BrandContactNotes" FROM PUBLIC;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON growth."WorkTasks", growth."BrandFollowUps", growth."BrandContactNotes" FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON growth."WorkTasks", growth."BrandFollowUps", growth."BrandContactNotes" FROM authenticated;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandContactNotes",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "BrandFollowUps",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "WorkTasks",
                schema: "growth");
        }
    }
}
