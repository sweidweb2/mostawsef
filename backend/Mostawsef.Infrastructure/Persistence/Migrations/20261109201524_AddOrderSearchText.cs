using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "orders",
                type: "character varying(152)",
                maxLength: 152,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_orders_SearchText",
                table: "orders",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_SearchText",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "orders");
        }
    }
}
