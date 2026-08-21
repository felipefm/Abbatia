using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scriptorium.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtherSaintsAndDiaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtherSaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyDevotionalId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ShortBiography = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherSaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherSaints_DailyDevotionals_DailyDevotionalId",
                        column: x => x.DailyDevotionalId,
                        principalTable: "DailyDevotionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_Date",
                table: "DiaryEntries",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherSaints_DailyDevotionalId",
                table: "OtherSaints",
                column: "DailyDevotionalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaryEntries");

            migrationBuilder.DropTable(
                name: "OtherSaints");
        }
    }
}
