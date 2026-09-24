using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountSecurities",
                schema: "growth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                    SetupExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecoveryHashes = table.Column<string[]>(type: "text[]", nullable: false),
                    LastTimeStep = table.Column<long>(type: "bigint", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChallengeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChallengeExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChallengeAccountVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSecurities", x => x.UserId);
                    table.CheckConstraint("CK_AccountSecurities_Values", "\"Revision\" > 0 AND \"FailedAttempts\" BETWEEN 0 AND 5 AND \"LastTimeStep\" >= -1");
                    table.ForeignKey(
                        name: "FK_AccountSecurities_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "growth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSecurities_ChallengeHash",
                schema: "growth",
                table: "AccountSecurities",
                column: "ChallengeHash",
                unique: true,
                filter: "\"ChallengeHash\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountSecurities",
                schema: "growth");
        }
    }
}
