using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InvitationPending",
                schema: "growth",
                table: "UserAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountLinks",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    AccountVersion = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLinks", x => x.Id);
                    table.CheckConstraint("CK_AccountLinks_Values", "\"Revision\" > 0 AND \"Purpose\" BETWEEN 0 AND 1 AND \"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_AccountLinks_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MailDeliveries",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedBody = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailDeliveries", x => x.Id);
                    table.CheckConstraint("CK_MailDeliveries_Values", "\"Revision\" > 0 AND \"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_MailDeliveries_AccountLinks_AccountLinkId",
                        column: x => x.AccountLinkId,
                        principalSchema: "growth",
                        principalTable: "AccountLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MailDeliveries_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinks_TokenHash",
                schema: "growth",
                table: "AccountLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinks_UserId_CreatedAt",
                schema: "growth",
                table: "AccountLinks",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MailDeliveries_AccountLinkId",
                schema: "growth",
                table: "MailDeliveries",
                column: "AccountLinkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailDeliveries_Status_CreatedAt",
                schema: "growth",
                table: "MailDeliveries",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MailDeliveries_UserId",
                schema: "growth",
                table: "MailDeliveries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailDeliveries",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "AccountLinks",
                schema: "growth");

            migrationBuilder.DropColumn(
                name: "InvitationPending",
                schema: "growth",
                table: "UserAccounts");
        }
    }
}
