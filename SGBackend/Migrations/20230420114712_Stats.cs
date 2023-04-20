using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGBackend.Migrations
{
    public partial class Stats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsCalculation",
                table: "PlaybackSummaries");

            migrationBuilder.AddColumn<Guid>(
                name: "StatsId",
                table: "User",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "GroupedFetchJobInstalled",
                table: "States",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Stats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    LatestFetch = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_User_StatsId",
                table: "User",
                column: "StatsId");

            migrationBuilder.AddForeignKey(
                name: "FK_User_Stats_StatsId",
                table: "User",
                column: "StatsId",
                principalTable: "Stats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_Stats_StatsId",
                table: "User");

            migrationBuilder.DropTable(
                name: "Stats");

            migrationBuilder.DropIndex(
                name: "IX_User_StatsId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "StatsId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "GroupedFetchJobInstalled",
                table: "States");

            migrationBuilder.AddColumn<bool>(
                name: "NeedsCalculation",
                table: "PlaybackSummaries",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
