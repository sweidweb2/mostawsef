using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LogSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "system_errors",
                type: "character varying(2904)",
                maxLength: 2904,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "activity_logs",
                type: "character varying(703)",
                maxLength: 703,
                nullable: false,
                defaultValue: "");

            // Rows written before this migration: same fields and separator as Text.SearchKey (lowercase only;
            // the Arabic normalization of SearchText.Normalize applies to rows written from now on).
            migrationBuilder.Sql("""
                UPDATE activity_logs
                SET "SearchText" = lower(concat_ws(E'\n', "Username", "Action", "Module", "Description"));
                """);
            migrationBuilder.Sql("""
                UPDATE system_errors
                SET "SearchText" = lower(concat_ws(E'\n', "Message", "Source", "ExceptionType", "RequestPath", "Username"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_system_errors_SearchText",
                table: "system_errors",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_SearchText",
                table: "activity_logs",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_errors_SearchText",
                table: "system_errors");

            migrationBuilder.DropIndex(
                name: "IX_activity_logs_SearchText",
                table: "activity_logs");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "system_errors");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "activity_logs");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
