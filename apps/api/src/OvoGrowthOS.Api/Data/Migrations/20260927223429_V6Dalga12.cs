using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V6Dalga12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkTasks_Target",
                schema: "growth",
                table: "WorkTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications");

            migrationBuilder.AddColumn<bool>(
                name: "PromiseRemindersEmail",
                schema: "growth",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WeeklyDigestEmail",
                schema: "growth",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TimeoutTaskId",
                schema: "growth",
                table: "BrandStageHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Platform",
                schema: "growth",
                table: "BrandApiSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BrandAdSettings",
                schema: "growth",
                columns: table => new
                {
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                    ProtectedClientSecret = table.Column<string>(type: "text", nullable: false),
                    ProtectedDeveloperToken = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandAdSettings", x => new { x.Platform, x.BrandId });
                    table.CheckConstraint("CK_BrandAdSettings_Platform", "\"Platform\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_BrandAdSettings_Revision", "\"Revision\" > 0");
                    table.CheckConstraint("CK_BrandAdSettings_Values", "length(\"AccountId\") BETWEEN 1 AND 64 AND length(\"ClientId\") <= 320");
                    table.ForeignKey(
                        name: "FK_BrandAdSettings_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeriodApprovals",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodApprovals", x => x.Id);
                    table.CheckConstraint("CK_PeriodApprovals_Values", "\"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND length(\"Reason\") <= 1000");
                    table.ForeignKey(
                        name: "FK_PeriodApprovals_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkTasks_Target",
                schema: "growth",
                table: "WorkTasks",
                sql: "(\"Kind\" = 0 AND \"DealId\" IS NULL AND \"Year\" IS NULL AND \"Month\" IS NULL) OR (\"Kind\" = 1 AND \"DealId\" IS NOT NULL AND \"Year\" IS NOT NULL AND \"Month\" IS NOT NULL AND \"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12) OR (\"Kind\" = 2 AND \"DealId\" IS NOT NULL AND \"Year\" IS NULL AND \"Month\" IS NULL) OR (\"Kind\" = 3 AND \"DealId\" IS NULL AND \"Year\" IS NULL AND \"Month\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications",
                sql: "\"Revision\" > 0 AND \"Kind\" BETWEEN 0 AND 6 AND (\"EmailStatus\" IS NULL OR \"EmailStatus\" BETWEEN 0 AND 4)");

            migrationBuilder.CreateIndex(
                name: "IX_BrandStageHistories_TimeoutTaskId",
                schema: "growth",
                table: "BrandStageHistories",
                column: "TimeoutTaskId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BrandApiSettings_Platform",
                schema: "growth",
                table: "BrandApiSettings",
                sql: "\"Platform\" BETWEEN 0 AND 1");

            migrationBuilder.CreateIndex(
                name: "IX_BrandAdSettings_BrandId",
                schema: "growth",
                table: "BrandAdSettings",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodApprovals_BrandId_Year_Month",
                schema: "growth",
                table: "PeriodApprovals",
                columns: new[] { "BrandId", "Year", "Month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BrandStageHistories_WorkTasks_TimeoutTaskId",
                schema: "growth",
                table: "BrandStageHistories",
                column: "TimeoutTaskId",
                principalSchema: "growth",
                principalTable: "WorkTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BrandStageHistories_WorkTasks_TimeoutTaskId",
                schema: "growth",
                table: "BrandStageHistories");

            migrationBuilder.DropTable(
                name: "BrandAdSettings",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "PeriodApprovals",
                schema: "growth");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkTasks_Target",
                schema: "growth",
                table: "WorkTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_BrandStageHistories_TimeoutTaskId",
                schema: "growth",
                table: "BrandStageHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BrandApiSettings_Platform",
                schema: "growth",
                table: "BrandApiSettings");

            migrationBuilder.DropColumn(
                name: "PromiseRemindersEmail",
                schema: "growth",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "WeeklyDigestEmail",
                schema: "growth",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TimeoutTaskId",
                schema: "growth",
                table: "BrandStageHistories");

            migrationBuilder.DropColumn(
                name: "Platform",
                schema: "growth",
                table: "BrandApiSettings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkTasks_Target",
                schema: "growth",
                table: "WorkTasks",
                sql: "(\"Kind\" = 0 AND \"DealId\" IS NULL AND \"Year\" IS NULL AND \"Month\" IS NULL) OR (\"Kind\" = 1 AND \"DealId\" IS NOT NULL AND \"Year\" IS NOT NULL AND \"Month\" IS NOT NULL AND \"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12) OR (\"Kind\" = 2 AND \"DealId\" IS NOT NULL AND \"Year\" IS NULL AND \"Month\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications",
                sql: "\"Revision\" > 0 AND \"Kind\" BETWEEN 0 AND 4 AND (\"EmailStatus\" IS NULL OR \"EmailStatus\" BETWEEN 0 AND 4)");
        }
    }
}
