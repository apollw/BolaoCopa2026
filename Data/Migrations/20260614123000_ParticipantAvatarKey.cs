using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BolaoCopa2026.Data.Migrations
{
    public partial class ParticipantAvatarKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarKey",
                table: "Participants",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarKey",
                table: "Participants");
        }
    }
}
