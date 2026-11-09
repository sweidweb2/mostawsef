using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "patients",
                type: "character varying(162)",
                maxLength: 162,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_patients_SearchText",
                table: "patients",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_patients_SearchText",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "patients");
        }
    }
}
