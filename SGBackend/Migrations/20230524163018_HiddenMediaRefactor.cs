using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGBackend.Migrations
{
    public partial class HiddenMediaRefactor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HiddenMediumId",
                table: "HiddenMedia",
                newName: "MediumId");

            migrationBuilder.RenameIndex(
                name: "IX_HiddenMedia_UserId_HiddenMediumId_HiddenOrigin",
                table: "HiddenMedia",
                newName: "IX_HiddenMedia_UserId_MediumId_HiddenOrigin");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenMedia_MediumId",
                table: "HiddenMedia",
                column: "MediumId");

            migrationBuilder.AddForeignKey(
                name: "FK_HiddenMedia_Media_MediumId",
                table: "HiddenMedia",
                column: "MediumId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HiddenMedia_Media_MediumId",
                table: "HiddenMedia");

            migrationBuilder.DropIndex(
                name: "IX_HiddenMedia_MediumId",
                table: "HiddenMedia");

            migrationBuilder.RenameColumn(
                name: "MediumId",
                table: "HiddenMedia",
                newName: "HiddenMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_HiddenMedia_UserId_MediumId_HiddenOrigin",
                table: "HiddenMedia",
                newName: "IX_HiddenMedia_UserId_HiddenMediumId_HiddenOrigin");
        }
    }
}
