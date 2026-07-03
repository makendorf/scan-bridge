using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanBridge.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggerTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerSettingsJson",
                table: "Scenarios",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "Scenarios",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerSettingsJson",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "Scenarios");
        }
    }
}
