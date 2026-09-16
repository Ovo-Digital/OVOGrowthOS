using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkTemplatesAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskHourPlans",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskHourPlans", x => x.Id);
                    table.CheckConstraint("CK_TaskHourPlans_Values", "EXTRACT(ISODOW FROM \"WeekStart\") = 1 AND EXTRACT(YEAR FROM \"WeekStart\") BETWEEN 2020 AND 2100 AND \"Hours\" BETWEEN 0 AND 168 AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_TaskHourPlans_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "growth",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyCapacities",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkingHours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnavailableHours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyCapacities", x => x.Id);
                    table.CheckConstraint("CK_WeeklyCapacities_Values", "EXTRACT(ISODOW FROM \"WeekStart\") = 1 AND EXTRACT(YEAR FROM \"WeekStart\") BETWEEN 2020 AND 2100 AND \"WorkingHours\" BETWEEN 0 AND 168 AND \"UnavailableHours\" BETWEEN 0 AND \"WorkingHours\" AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_WeeklyCapacities_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkTemplateRuns",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTemplateRuns", x => x.Id);
                    table.CheckConstraint("CK_WorkTemplateRuns_Scope", "(\"Kind\" = 0 AND \"Year\" = 0 AND \"Month\" = 0 AND \"DealId\" IS NULL) OR (\"Kind\" = 1 AND \"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND \"DealId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkTemplateRuns_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTemplateRuns_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkTemplateTasks",
                schema: "growth",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTemplateTasks", x => new { x.RunId, x.Step });
                    table.ForeignKey(
                        name: "FK_WorkTemplateTasks_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "growth",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTemplateTasks_WorkTemplateRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "growth",
                        principalTable: "WorkTemplateRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskHourPlans_TaskId_WeekStart",
                schema: "growth",
                table: "TaskHourPlans",
                columns: new[] { "TaskId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskHourPlans_WeekStart",
                schema: "growth",
                table: "TaskHourPlans",
                column: "WeekStart");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyCapacities_UserId_WeekStart",
                schema: "growth",
                table: "WeeklyCapacities",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyCapacities_WeekStart",
                schema: "growth",
                table: "WeeklyCapacities",
                column: "WeekStart");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTemplateRuns_BrandId_Kind_Year_Month",
                schema: "growth",
                table: "WorkTemplateRuns",
                columns: new[] { "BrandId", "Kind", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkTemplateRuns_DealId",
                schema: "growth",
                table: "WorkTemplateRuns",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTemplateTasks_TaskId",
                schema: "growth",
                table: "WorkTemplateTasks",
                column: "TaskId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskHourPlans",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "WeeklyCapacities",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "WorkTemplateTasks",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "WorkTemplateRuns",
                schema: "growth");
        }
    }
}
