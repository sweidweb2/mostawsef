using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientRequestIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_ClientRequestId",
                table: "payments",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_positive_amount",
                table: "payments",
                sql: "\"Amount\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ClientRequestId",
                table: "orders",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_non_negative_money",
                table: "orders",
                sql: "\"Total\" >= 0 AND \"Paid\" >= 0 AND \"Addon\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_paid_within_total",
                table: "orders",
                sql: "\"Paid\" <= \"Total\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_ClientRequestId",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_positive_amount",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_orders_ClientRequestId",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_non_negative_money",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_paid_within_total",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "orders");
        }
    }
}
