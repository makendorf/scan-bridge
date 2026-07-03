using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanBridge.Migrations
{
    /// <inheritdoc />
    public partial class RemoveActionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostScanActionGroups");

            migrationBuilder.DropTable(
                name: "PostScanActionGroupScanners");

            migrationBuilder.DropTable(
                name: "PostScanActions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostScanActionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostScanActionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostScanActionGroupScanners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScannerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostScanActionGroupScanners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostScanActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostScanActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostScanActionGroupScanners_GroupId_ScannerName",
                table: "PostScanActionGroupScanners",
                columns: new[] { "GroupId", "ScannerName" },
                unique: true);
        }
    }
}
