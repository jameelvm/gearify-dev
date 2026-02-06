using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gearify.OrderService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_requested_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_requested_by",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_by",
                table: "orders");
        }
    }
}
