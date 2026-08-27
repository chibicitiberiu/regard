using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regard.Backend.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddJobLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Jobs_JobId",
                table: "Messages");

            migrationBuilder.AddColumn<string>(
                name: "Log",
                table: "Jobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify",
                table: "Jobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Jobs_JobId",
                table: "Messages",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Jobs_JobId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Log",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Notify",
                table: "Jobs");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Jobs_JobId",
                table: "Messages",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id");
        }
    }
}
