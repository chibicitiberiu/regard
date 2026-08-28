using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regard.Backend.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class AddVideoEnrichedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EnrichedAt",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            // Videos that already exist were synced with full (non-flat) metadata, so treat them as
            // enriched — otherwise the UI would hide their valid published dates until re-opened.
            migrationBuilder.Sql("UPDATE Videos SET EnrichedAt = LastUpdated WHERE EnrichedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrichedAt",
                table: "Videos");
        }
    }
}
