using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CollectionPromises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionPromises",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyPerformanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PromisedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentIdsAtRecording = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPromises", x => x.Id);
                    table.CheckConstraint("CK_CollectionPromises_Values", "\"Amount\" > 0 AND \"Revision\" > 0 AND EXTRACT(YEAR FROM \"PromisedOn\") BETWEEN 2020 AND 2100");
                    table.ForeignKey(
                        name: "FK_CollectionPromises_BrandContactNotes_ContactNoteId",
                        column: x => x.ContactNoteId,
                        principalSchema: "growth",
                        principalTable: "BrandContactNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPromises_CollectionAccounts_MonthlyPerformanceId",
                        column: x => x.MonthlyPerformanceId,
                        principalSchema: "growth",
                        principalTable: "CollectionAccounts",
                        principalColumn: "MonthlyPerformanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPromises_UserAccounts_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPromises_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "growth",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPromises_ContactNoteId",
                schema: "growth",
                table: "CollectionPromises",
                column: "ContactNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPromises_MonthlyPerformanceId",
                schema: "growth",
                table: "CollectionPromises",
                column: "MonthlyPerformanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPromises_OwnerId",
                schema: "growth",
                table: "CollectionPromises",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPromises_TaskId",
                schema: "growth",
                table: "CollectionPromises",
                column: "TaskId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionPromises",
                schema: "growth");
        }
    }
}
