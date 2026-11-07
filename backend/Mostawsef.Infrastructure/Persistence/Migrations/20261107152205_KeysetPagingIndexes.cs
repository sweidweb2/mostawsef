using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeysetPagingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_errors_OccurredAt",
                table: "system_errors");

            migrationBuilder.DropIndex(
                name: "IX_activity_logs_OccurredAt",
                table: "activity_logs");

            migrationBuilder.CreateIndex(
                name: "IX_system_errors_OccurredAt_Id",
                table: "system_errors",
                columns: new[] { "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_OccurredAt_Id",
                table: "activity_logs",
                columns: new[] { "OccurredAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_errors_OccurredAt_Id",
                table: "system_errors");

            migrationBuilder.DropIndex(
                name: "IX_activity_logs_OccurredAt_Id",
                table: "activity_logs");

            migrationBuilder.CreateIndex(
                name: "IX_system_errors_OccurredAt",
                table: "system_errors",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_OccurredAt",
                table: "activity_logs",
                column: "OccurredAt");
        }
    }
}
