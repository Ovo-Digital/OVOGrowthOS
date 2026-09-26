using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BrandMailPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandMailPolicies",
                schema: "growth",
                columns: table => new
                {
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportEmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    BodyTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandMailPolicies", x => x.BrandId);
                    table.CheckConstraint("CK_BrandMailPolicies_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_BrandMailPolicies_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandMailPolicies",
                schema: "growth");
        }
    }
}
