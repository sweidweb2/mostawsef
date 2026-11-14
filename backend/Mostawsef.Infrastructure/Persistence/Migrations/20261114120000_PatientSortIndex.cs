using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostawsef.Infrastructure.Persistence.Context;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <summary>The patient list sorts by (SearchName, Id): one index in that order replaces the SearchName-only one.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261114120000_PatientSortIndex")]
    public class PatientSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_patients_SearchName",
                table: "patients");

            migrationBuilder.CreateIndex(
                name: "IX_patients_SearchName_Id",
                table: "patients",
                columns: new[] { "SearchName", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_patients_SearchName_Id",
                table: "patients");

            migrationBuilder.CreateIndex(
                name: "IX_patients_SearchName",
                table: "patients",
                column: "SearchName");
        }
    }
}
