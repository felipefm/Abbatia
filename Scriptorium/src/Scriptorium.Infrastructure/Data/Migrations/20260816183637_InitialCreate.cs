using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scriptorium.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyDevotionals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LiturgicalTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyDevotionals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Homilies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyDevotionalId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    HomilyDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OriginalLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    TextoOriginal = table.Column<string>(type: "TEXT", nullable: false),
                    TextoTraduzido = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    TranslationAttempts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homilies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Homilies_DailyDevotionals_DailyDevotionalId",
                        column: x => x.DailyDevotionalId,
                        principalTable: "DailyDevotionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyDevotionalId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readings_DailyDevotionals_DailyDevotionalId",
                        column: x => x.DailyDevotionalId,
                        principalTable: "DailyDevotionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Saints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyDevotionalId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Biography = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Saints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Saints_DailyDevotionals_DailyDevotionalId",
                        column: x => x.DailyDevotionalId,
                        principalTable: "DailyDevotionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyDevotionals_Date",
                table: "DailyDevotionals",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Homilies_DailyDevotionalId",
                table: "Homilies",
                column: "DailyDevotionalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Homilies_SourceUrl",
                table: "Homilies",
                column: "SourceUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Readings_DailyDevotionalId",
                table: "Readings",
                column: "DailyDevotionalId");

            migrationBuilder.CreateIndex(
                name: "IX_Saints_DailyDevotionalId",
                table: "Saints",
                column: "DailyDevotionalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Homilies");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "Saints");

            migrationBuilder.DropTable(
                name: "DailyDevotionals");
        }
    }
}
