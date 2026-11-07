using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemErrorLevelIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_system_errors_Level_OccurredAt",
                table: "system_errors",
                columns: new[] { "Level", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_errors_Level_OccurredAt",
                table: "system_errors");
        }
    }
}
