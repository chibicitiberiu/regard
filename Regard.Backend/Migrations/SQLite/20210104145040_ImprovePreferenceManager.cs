using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SQLite
{
    public partial class ImprovePreferenceManager : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoDownload",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AutomaticDeleteWatched",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DownloadMaxCount",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DownloadMaxSize",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DownloadOrder",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DownloadPath",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AutoDownload",
                table: "SubscriptionFolders");

            migrationBuilder.DropColumn(
                name: "AutomaticDeleteWatched",
                table: "SubscriptionFolders");

            migrationBuilder.DropColumn(
                name: "DownloadMaxCount",
                table: "SubscriptionFolders");

            migrationBuilder.DropColumn(
                name: "DownloadMaxSize",
                table: "SubscriptionFolders");

            migrationBuilder.DropColumn(
                name: "DownloadOrder",
                table: "SubscriptionFolders");

            migrationBuilder.DropColumn(
                name: "DownloadPath",
                table: "SubscriptionFolders");

            migrationBuilder.AlterColumn<string>(
                name: "VideoProviderId",
                table: "Videos",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VideoId",
                table: "Videos",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrl",
                table: "Videos",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Videos",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subscriptions",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SubscriptionFolders",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Configuration",
                table: "ProviderConfigurations",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Messages",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionFolderPreferences",
                columns: table => new
                {
                    Key = table.Column<string>(nullable: false),
                    SubscriptionFolderId = table.Column<int>(nullable: false),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionFolderPreferences", x => new { x.Key, x.SubscriptionFolderId });
                    table.ForeignKey(
                        name: "FK_SubscriptionFolderPreferences_SubscriptionFolders_SubscriptionFolderId",
                        column: x => x.SubscriptionFolderId,
                        principalTable: "SubscriptionFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPreferences",
                columns: table => new
                {
                    Key = table.Column<string>(nullable: false),
                    SubscriptionId = table.Column<int>(nullable: false),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPreferences", x => new { x.Key, x.SubscriptionId });
                    table.ForeignKey(
                        name: "FK_SubscriptionPreferences_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFolderPreferences_SubscriptionFolderId",
                table: "SubscriptionFolderPreferences",
                column: "SubscriptionFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPreferences_SubscriptionId",
                table: "SubscriptionPreferences",
                column: "SubscriptionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionFolderPreferences");

            migrationBuilder.DropTable(
                name: "SubscriptionPreferences");

            migrationBuilder.AlterColumn<string>(
                name: "VideoProviderId",
                table: "Videos",
                type: "TEXT",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "VideoId",
                table: "Videos",
                type: "TEXT",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrl",
                table: "Videos",
                type: "TEXT",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Videos",
                type: "TEXT",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subscriptions",
                type: "TEXT",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 250);

            migrationBuilder.AddColumn<bool>(
                name: "AutoDownload",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaticDeleteWatched",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadMaxCount",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadMaxSize",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadOrder",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadPath",
                table: "Subscriptions",
                type: "TEXT",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SubscriptionFolders",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 64);

            migrationBuilder.AddColumn<bool>(
                name: "AutoDownload",
                table: "SubscriptionFolders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaticDeleteWatched",
                table: "SubscriptionFolders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadMaxCount",
                table: "SubscriptionFolders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadMaxSize",
                table: "SubscriptionFolders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadOrder",
                table: "SubscriptionFolders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadPath",
                table: "SubscriptionFolders",
                type: "TEXT",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Configuration",
                table: "ProviderConfigurations",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Messages",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string));
        }
    }
}
