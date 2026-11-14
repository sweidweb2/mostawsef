using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostawsef.Infrastructure.Persistence.Context;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A patient's orders by status (the patient page and the unpaid-orders check): one (PatientId, Status) index replaces the
    /// PatientId-only one. Built CONCURRENTLY (outside a transaction) so orders can still be written meanwhile; IF [NOT] EXISTS
    /// makes a re-run after an interrupted build safe (an interrupted build leaves an invalid index, dropped first).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261114130000_OrderPatientStatusIndex")]
    public class OrderPatientStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_orders_PatientId_Status\";", suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_orders_PatientId_Status\" ON orders (\"PatientId\", \"Status\");",
                suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_orders_PatientId\";", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_orders_PatientId\";", suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_orders_PatientId\" ON orders (\"PatientId\");",
                suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_orders_PatientId_Status\";", suppressTransaction: true);
        }
    }
}
