using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostawsef.Infrastructure.Persistence.Context;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111200000_AddUserNameIndexes")]
    public class AddUserNameIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_FullName",
                table: "users",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_users_SearchName",
                table: "users",
                column: "SearchName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_FullName",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_SearchName",
                table: "users");
        }
    }
}
