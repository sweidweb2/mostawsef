using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostawsef.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledDumps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scheduled_dumps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderPath = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    KeepCount = table.Column<int>(type: "integer", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    DeletedOldFiles = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_dumps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_dumps_CreatedAt",
                table: "scheduled_dumps",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scheduled_dumps");
        }
    }
}
