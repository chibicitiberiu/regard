using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SQLite
{
    public partial class AddProviderConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "Videos");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Discovered",
                table: "Videos",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "OriginalUrl",
                table: "Videos",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadMaxSize",
                table: "Subscriptions",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadMaxSize",
                table: "SubscriptionFolders",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false),
                    Content = table.Column<string>(nullable: true),
                    Details = table.Column<string>(nullable: true),
                    Severity = table.Column<int>(nullable: false),
                    UserId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderConfigurations",
                columns: table => new
                {
                    ProviderId = table.Column<string>(maxLength: 60, nullable: false),
                    Configuration = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderConfigurations", x => x.ProviderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId",
                table: "Messages",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "ProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "Discovered",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "OriginalUrl",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "DownloadMaxSize",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DownloadMaxSize",
                table: "SubscriptionFolders");

            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "Videos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
