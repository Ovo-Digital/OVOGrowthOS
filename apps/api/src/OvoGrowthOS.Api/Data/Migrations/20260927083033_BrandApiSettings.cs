using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BrandApiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandApiSettings",
                schema: "growth",
                columns: table => new
                {
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ApiUser = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandApiSettings", x => x.BrandId);
                    table.CheckConstraint("CK_BrandApiSettings_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_BrandApiSettings_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandApiSettings",
                schema: "growth");
        }
    }
}
