using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "growth");

            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEmail = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Entity = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: false),
                    NewValue = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Brands",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LegalName = table.Column<string>(type: "text", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Industry = table.Column<string>(type: "text", nullable: false),
                    SubIndustry = table.Column<string>(type: "text", nullable: false),
                    BusinessModel = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContactName = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DealType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MonthlyRetainer = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumMonthlyFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RevenueShareRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IncrementalRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BaselineRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SetupInvestment = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EstimatedMonthlyInternalCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ContractMonths = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    Operator = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandEconomics",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AverageMonthlyRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossMarginRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageOrderValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MonthlyOrders = table.Column<int>(type: "integer", nullable: false),
                    MonthlySessions = table.Column<int>(type: "integer", nullable: false),
                    ReturnRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CancellationRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ChargebackRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    VariableCostRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentAdSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NewCustomers = table.Column<int>(type: "integer", nullable: false),
                    ReturningCustomers = table.Column<int>(type: "integer", nullable: false),
                    StockCoverageDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandEconomics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandEconomics_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evaluations",
                schema: "growth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProductMarketFit = table.Column<int>(type: "integer", nullable: false),
                    GrowthPotential = table.Column<int>(type: "integer", nullable: false),
                    OperationalReadiness = table.Column<int>(type: "integer", nullable: false),
                    CreativeCapability = table.Column<int>(type: "integer", nullable: false),
                    FounderCooperation = table.Column<int>(type: "integer", nullable: false),
                    DataMaturity = table.Column<int>(type: "integer", nullable: false),
                    InternalMonthlyCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SetupInvestment = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    RecommendedDealType = table.Column<int>(type: "integer", nullable: false),
                    RecommendedMinimumFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExplanationJson = table.Column<string>(type: "text", nullable: false),
                    RuleSetVersion = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evaluations_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "growth",
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_Entity_EntityId_Timestamp",
                schema: "growth",
                table: "AuditRecords",
                columns: new[] { "Entity", "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_BrandEconomics_BrandId",
                schema: "growth",
                table: "BrandEconomics",
                column: "BrandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Name",
                schema: "growth",
                table: "Brands",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Status",
                schema: "growth",
                table: "Brands",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_BrandId_Status",
                schema: "growth",
                table: "Deals",
                columns: new[] { "BrandId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_BrandId_CreatedAt",
                schema: "growth",
                table: "Evaluations",
                columns: new[] { "BrandId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Category_Version",
                schema: "growth",
                table: "Rules",
                columns: new[] { "Category", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "BrandEconomics",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "Deals",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "Evaluations",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "Rules",
                schema: "growth");

            migrationBuilder.DropTable(
                name: "Brands",
                schema: "growth");
        }
    }
}
