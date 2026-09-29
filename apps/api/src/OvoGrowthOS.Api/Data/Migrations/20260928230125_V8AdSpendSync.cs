using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V8AdSpendSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications",
                sql: "\"Revision\" > 0 AND \"Kind\" BETWEEN 0 AND 9 AND (\"EmailStatus\" IS NULL OR \"EmailStatus\" BETWEEN 0 AND 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserNotifications_Values",
                schema: "growth",
                table: "UserNotifications",
                sql: "\"Revision\" > 0 AND \"Kind\" BETWEEN 0 AND 6 AND (\"EmailStatus\" IS NULL OR \"EmailStatus\" BETWEEN 0 AND 4)");
        }
    }
}
