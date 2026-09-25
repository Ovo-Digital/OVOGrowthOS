using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MailConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailConfigurations",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Secure = table.Column<bool>(type: "boolean", nullable: false),
                    User = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailConfigurations", x => x.Id);
                    table.CheckConstraint("CK_MailConfigurations_Values", "\"Id\" = 1 AND \"Revision\" > 0 AND ((\"Port\" = 465 AND \"Secure\") OR (\"Port\" = 587 AND NOT \"Secure\"))");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailConfigurations",
                schema: "growth");
        }
    }
}
