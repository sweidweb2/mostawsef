using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_errors_Level",
                table: "system_errors");

            migrationBuilder.DropIndex(
                name: "IX_activity_logs_Action",
                table: "activity_logs");

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_Action_OccurredAt",
                table: "activity_logs",
                columns: new[] { "Action", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_activity_logs_Action_OccurredAt",
                table: "activity_logs");

            migrationBuilder.CreateIndex(
                name: "IX_system_errors_Level",
                table: "system_errors",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_Action",
                table: "activity_logs",
                column: "Action");
        }
    }
}
