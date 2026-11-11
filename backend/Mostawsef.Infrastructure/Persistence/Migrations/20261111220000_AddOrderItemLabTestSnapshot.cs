using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostawsef.Infrastructure.Persistence.Context;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111220000_AddOrderItemLabTestSnapshot")]
    public partial class AddOrderItemLabTestSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabTestCode",
                table: "order_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LabTestName",
                table: "order_items",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabTestCode",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "LabTestName",
                table: "order_items");
        }
    }
}
