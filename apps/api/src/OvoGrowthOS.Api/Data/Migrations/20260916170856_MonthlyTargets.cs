using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MonthlyTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyTargets",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetRevenueGoal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AdBudget = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ContributionMarginGoal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyTargets", x => x.Id);
                    table.CheckConstraint("CK_MonthlyTargets_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_MonthlyTargets_Money", "\"NetRevenueGoal\" >= 0 AND \"AdBudget\" >= 0 AND \"ContributionMarginGoal\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_MonthlyTargets_Period", "\"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_MonthlyTargets_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlyTargets_UserAccounts_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TargetActions",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRevision = table.Column<int>(type: "integer", nullable: false),
                    PerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformanceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetActions", x => x.Id);
                    table.CheckConstraint("CK_TargetActions_Metric", "\"Metric\" BETWEEN 0 AND 2 AND \"TargetRevision\" > 0");
                    table.ForeignKey(
                        name: "FK_TargetActions_MonthlyPerformances_PerformanceId",
                        column: x => x.PerformanceId,
                        principalSchema: "growth",
                        principalTable: "MonthlyPerformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TargetActions_MonthlyTargets_TargetId",
                        column: x => x.TargetId,
                        principalSchema: "growth",
                        principalTable: "MonthlyTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TargetActions_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "growth",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTargets_BrandId_Year_Month_Currency",
                schema: "growth",
                table: "MonthlyTargets",
                columns: new[] { "BrandId", "Year", "Month", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTargets_OwnerId",
                schema: "growth",
                table: "MonthlyTargets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetActions_PerformanceId",
                schema: "growth",
                table: "TargetActions",
                column: "PerformanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetActions_TargetId_Metric",
                schema: "growth",
                table: "TargetActions",
                columns: new[] { "TargetId", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetActions_TaskId",
                schema: "growth",
                table: "TargetActions",
                column: "TaskId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TargetActions",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "MonthlyTargets",
                schema: "growth");
        }
    }
}
