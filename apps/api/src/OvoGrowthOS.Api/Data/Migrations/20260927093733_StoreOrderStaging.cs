using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreOrderStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreOrderStagings",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceStoreId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderNumber = table.Column<int>(type: "integer", nullable: false),
                    PlacedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OrderTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OrderStatus = table.Column<int>(type: "integer", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreOrderStagings", x => x.Id);
                    table.CheckConstraint("CK_StoreOrderStaging_Amounts", "\"OrderTotal\" >= 0 AND \"PaidAmount\" >= 0 AND \"RefundedAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_StoreOrderStagings_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreOrderStagings_BrandId_PlacedOnUtc",
                schema: "growth",
                table: "StoreOrderStagings",
                columns: new[] { "BrandId", "PlacedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreOrderStagings_BrandId_SourceOrderId",
                schema: "growth",
                table: "StoreOrderStagings",
                columns: new[] { "BrandId", "SourceOrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreOrderStagings",
                schema: "growth");
        }
    }
}
