using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGBackend.Migrations
{
    public partial class RemoveTogetherSeconds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaybackSeconds",
                table: "MutualPlaybackEntries");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PlaybackSeconds",
                table: "MutualPlaybackEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
