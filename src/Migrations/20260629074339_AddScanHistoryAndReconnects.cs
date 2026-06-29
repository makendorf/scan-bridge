using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanBridge.Migrations
{
    /// <inheritdoc />
    public partial class AddScanHistoryAndReconnects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconnectEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScannerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconnectEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScannerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RawData = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ParsedData = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReconnectEvents_ScannerName",
                table: "ReconnectEvents",
                column: "ScannerName");

            migrationBuilder.CreateIndex(
                name: "IX_ReconnectEvents_Timestamp",
                table: "ReconnectEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Format",
                table: "ScanHistory",
                column: "Format");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_ScannerName",
                table: "ScanHistory",
                column: "ScannerName");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_Timestamp",
                table: "ScanHistory",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconnectEvents");

            migrationBuilder.DropTable(
                name: "ScanHistory");
        }
    }
}
