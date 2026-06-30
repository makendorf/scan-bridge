using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanBridge.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScenarioConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScenarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceNodeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetNodeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourcePort = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetPort = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScenarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PositionX = table.Column<double>(type: "REAL", nullable: false),
                    PositionY = table.Column<double>(type: "REAL", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScannerNamesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioConnections_ScenarioId",
                table: "ScenarioConnections",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNodes_ScenarioId",
                table: "ScenarioNodes",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_Name",
                table: "Scenarios",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScenarioConnections");

            migrationBuilder.DropTable(
                name: "ScenarioNodes");

            migrationBuilder.DropTable(
                name: "Scenarios");
        }
    }
}
