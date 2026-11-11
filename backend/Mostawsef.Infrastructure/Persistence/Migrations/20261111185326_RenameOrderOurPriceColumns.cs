using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrderOurPriceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OurPrice",
                table: "orders",
                newName: "Total");

            migrationBuilder.RenameColumn(
                name: "OurPrice",
                table: "order_items",
                newName: "Price");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Total",
                table: "orders",
                newName: "OurPrice");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "order_items",
                newName: "OurPrice");
        }
    }
}
