using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5ScopeTimeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceTimeEntryId",
                schema: "growth",
                table: "ServiceCostEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DealScopeItems",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedBy = table.Column<string>(type: "text", nullable: false),
                    RemoveReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealScopeItems", x => x.Id);
                    table.CheckConstraint("CK_DealScopeItems_Values", "length(btrim(\"Title\")) BETWEEN 1 AND 200 AND length(btrim(\"Description\")) BETWEEN 0 AND 2000 AND (\"RemovedAt\" IS NULL OR (length(btrim(\"RemoveReason\")) > 0 AND length(btrim(\"RemovedBy\")) > 0))");
                    table.ForeignKey(
                        name: "FK_DealScopeItems_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskTimeEntries",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTimeEntries", x => x.Id);
                    table.CheckConstraint("CK_TaskTimeEntries_Values", "EXTRACT(ISODOW FROM \"WeekStart\") = 1 AND EXTRACT(YEAR FROM \"WeekStart\") BETWEEN 2020 AND 2100 AND \"Hours\" > 0 AND \"Hours\" <= 168 AND (\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0))");
                    table.ForeignKey(
                        name: "FK_TaskTimeEntries_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTimeEntries_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "growth",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DealScopeRequests",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedBy = table.Column<string>(type: "text", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ScopeItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealScopeRequests", x => x.Id);
                    table.CheckConstraint("CK_DealScopeRequests_Values", "\"Status\" BETWEEN 0 AND 2 AND length(btrim(\"Title\")) BETWEEN 1 AND 200 AND length(btrim(\"Description\")) BETWEEN 0 AND 2000 AND ((\"Status\" = 0 AND \"DecidedAt\" IS NULL AND \"ScopeItemId\" IS NULL) OR (\"Status\" <> 0 AND \"DecidedAt\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0 AND length(btrim(\"DecidedBy\")) > 0 AND (\"Status\" = 1) = (\"ScopeItemId\" IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_DealScopeRequests_DealScopeItems_ScopeItemId",
                        column: x => x.ScopeItemId,
                        principalSchema: "growth",
                        principalTable: "DealScopeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DealScopeRequests_PartnershipDeals_DealId",
                        column: x => x.DealId,
                        principalSchema: "growth",
                        principalTable: "PartnershipDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCostEntries_SourceTimeEntryId",
                schema: "growth",
                table: "ServiceCostEntries",
                column: "SourceTimeEntryId",
                unique: true,
                filter: "\"SourceTimeEntryId\" IS NOT NULL AND \"VoidedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DealScopeItems_DealId_CreatedAt",
                schema: "growth",
                table: "DealScopeItems",
                columns: new[] { "DealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DealScopeItems_DealId_Title",
                schema: "growth",
                table: "DealScopeItems",
                columns: new[] { "DealId", "Title" },
                unique: true,
                filter: "\"RemovedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DealScopeRequests_DealId_Status_RequestedAt",
                schema: "growth",
                table: "DealScopeRequests",
                columns: new[] { "DealId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DealScopeRequests_ScopeItemId",
                schema: "growth",
                table: "DealScopeRequests",
                column: "ScopeItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeEntries_TaskId_CreatedAt",
                schema: "growth",
                table: "TaskTimeEntries",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeEntries_UserId_WeekStart",
                schema: "growth",
                table: "TaskTimeEntries",
                columns: new[] { "UserId", "WeekStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealScopeRequests",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "TaskTimeEntries",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "DealScopeItems",
                schema: "growth");

            migrationBuilder.DropIndex(
                name: "IX_ServiceCostEntries_SourceTimeEntryId",
                schema: "growth",
                table: "ServiceCostEntries");

            migrationBuilder.DropColumn(
                name: "SourceTimeEntryId",
                schema: "growth",
                table: "ServiceCostEntries");
        }
    }
}
