using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGBackend.Migrations
{
    public partial class HiddenMedia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HiddenMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HiddenMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    HiddenOrigin = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HiddenMedia_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenMedia_UserId_HiddenMediumId_HiddenOrigin",
                table: "HiddenMedia",
                columns: new[] { "UserId", "HiddenMediumId", "HiddenOrigin" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HiddenMedia");
        }
    }
}
