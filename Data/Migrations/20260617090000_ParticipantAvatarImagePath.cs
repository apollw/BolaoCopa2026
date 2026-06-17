using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BolaoCopa2026.Data.Migrations
{
    public partial class ParticipantAvatarImagePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarImagePath",
                table: "Participants",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarImagePath",
                table: "Participants");
        }
    }
}
