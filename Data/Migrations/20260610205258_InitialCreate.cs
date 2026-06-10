using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BolaoCopa2026.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OfficialNumber = table.Column<int>(type: "integer", nullable: false),
                    RoundId = table.Column<int>(type: "integer", nullable: false),
                    HomeTeamCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HomeTeamName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HomeTeamIsBrazil = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTeamCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AwayTeamName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AwayTeamIsBrazil = table.Column<bool>(type: "boolean", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Kickoff = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Venue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Result_HomeGoals = table.Column<int>(type: "integer", nullable: true),
                    Result_AwayGoals = table.Column<int>(type: "integer", nullable: true),
                    ResultQualifiedTeamCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResultRegisteredBy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Result_RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Login = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    HomeGoals = table.Column<int>(type: "integer", nullable: false),
                    AwayGoals = table.Column<int>(type: "integer", nullable: false),
                    QualifiedTeamCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => new { x.ParticipantId, x.MatchId });
                });

            migrationBuilder.CreateTable(
                name: "ResultAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredBy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoundSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    RoundId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AuditDownloadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuditProofHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialPredictions",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Champion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RunnerUp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TopScorer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GoldenBall = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialPredictions", x => x.ParticipantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_OfficialNumber",
                table: "Matches",
                column: "OfficialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Email",
                table: "Participants",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Login",
                table: "Participants",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_MatchId",
                table: "Predictions",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSubmissions_ParticipantId_RoundId",
                table: "RoundSubmissions",
                columns: new[] { "ParticipantId", "RoundId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "ResultAudits");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "RoundSubmissions");

            migrationBuilder.DropTable(
                name: "SpecialPredictions");
        }
    }
}
