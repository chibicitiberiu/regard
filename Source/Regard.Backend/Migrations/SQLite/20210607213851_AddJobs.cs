using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SQLite
{
    public partial class AddJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "JobId",
                table: "Messages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    TrackWhenScheduled = table.Column<bool>(type: "INTEGER", nullable: false),
                    JobDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Started = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Completed = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextRun = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Key = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_JobId",
                table: "Messages",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_UserId",
                table: "Jobs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Jobs_JobId",
                table: "Messages",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Jobs_JobId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Messages_JobId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "Messages");
        }
    }
}
