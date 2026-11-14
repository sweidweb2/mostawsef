using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostawsef.Infrastructure.Persistence.Context;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <summary>A patient's orders by status (the patient page and the unpaid-orders check): one (PatientId, Status) index replaces the PatientId-only one.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261114130000_OrderPatientStatusIndex")]
    public class OrderPatientStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_PatientId",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_orders_PatientId_Status",
                table: "orders",
                columns: new[] { "PatientId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_PatientId_Status",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_orders_PatientId",
                table: "orders",
                column: "PatientId");
        }
    }
}
