using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V8PortalRequestResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                schema: "growth",
                table: "PortalDataRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespondedBy",
                schema: "growth",
                table: "PortalDataRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResponseText",
                schema: "growth",
                table: "PortalDataRequests",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondedAt",
                schema: "growth",
                table: "PortalDataRequests");

            migrationBuilder.DropColumn(
                name: "RespondedBy",
                schema: "growth",
                table: "PortalDataRequests");

            migrationBuilder.DropColumn(
                name: "ResponseText",
                schema: "growth",
                table: "PortalDataRequests");
        }
    }
}
