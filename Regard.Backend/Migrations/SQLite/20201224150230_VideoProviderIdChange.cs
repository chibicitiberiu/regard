using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SQLite
{
    public partial class VideoProviderIdChange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Videos");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionProviderId",
                table: "Videos",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoProviderId",
                table: "Videos",
                maxLength: 60,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionProviderId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "VideoProviderId",
                table: "Videos");

            migrationBuilder.AddColumn<string>(
                name: "ProviderId",
                table: "Videos",
                type: "TEXT",
                maxLength: 60,
                nullable: true);
        }
    }
}
