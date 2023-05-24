using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGBackend.Migrations
{
    public partial class BeatsPerMinute : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BeatsPerMinute",
                table: "Media",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BeatsPerMinute",
                table: "Media");
        }
    }
}
