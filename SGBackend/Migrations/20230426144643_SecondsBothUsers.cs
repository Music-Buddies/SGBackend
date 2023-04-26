using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGBackend.Migrations
{
    public partial class SecondsBothUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PlaybackSecondsUser1",
                table: "MutualPlaybackEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PlaybackSecondsUser2",
                table: "MutualPlaybackEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaybackSecondsUser1",
                table: "MutualPlaybackEntries");

            migrationBuilder.DropColumn(
                name: "PlaybackSecondsUser2",
                table: "MutualPlaybackEntries");
        }
    }
}
