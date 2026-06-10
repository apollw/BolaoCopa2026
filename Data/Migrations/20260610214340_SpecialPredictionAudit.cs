using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BolaoCopa2026.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpecialPredictionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AuditDownloadedAt",
                table: "SpecialPredictions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditProofHash",
                table: "SpecialPredictions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SavedAt",
                table: "SpecialPredictions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "SpecialPredictions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuditDownloadedAt",
                table: "SpecialPredictions");

            migrationBuilder.DropColumn(
                name: "AuditProofHash",
                table: "SpecialPredictions");

            migrationBuilder.DropColumn(
                name: "SavedAt",
                table: "SpecialPredictions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "SpecialPredictions");
        }
    }
}
