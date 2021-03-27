using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SQLite
{
    public partial class AddModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionFolders",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(maxLength: 64, nullable: true),
                    UserId = table.Column<string>(nullable: true),
                    ParentId = table.Column<int>(nullable: true),
                    AutoDownload = table.Column<bool>(nullable: true),
                    DownloadMaxCount = table.Column<int>(nullable: true),
                    DownloadOrder = table.Column<int>(nullable: true),
                    AutomaticDeleteWatched = table.Column<bool>(nullable: true),
                    DownloadPath = table.Column<string>(maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionFolders_SubscriptionFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "SubscriptionFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionFolders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubscriptionProviderId = table.Column<string>(maxLength: 60, nullable: true),
                    SubscriptionId = table.Column<string>(maxLength: 2048, nullable: true),
                    Name = table.Column<string>(maxLength: 250, nullable: true),
                    Description = table.Column<string>(maxLength: 2048, nullable: true),
                    ParentFolderId = table.Column<int>(nullable: true),
                    ThumbnailPath = table.Column<string>(maxLength: 2048, nullable: true),
                    UserId = table.Column<string>(nullable: true),
                    ProviderData = table.Column<string>(nullable: true),
                    AutoDownload = table.Column<bool>(nullable: true),
                    DownloadMaxCount = table.Column<int>(nullable: true),
                    DownloadOrder = table.Column<int>(nullable: true),
                    AutomaticDeleteWatched = table.Column<bool>(nullable: true),
                    DownloadPath = table.Column<string>(maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_SubscriptionFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "SubscriptionFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderId = table.Column<string>(maxLength: 60, nullable: true),
                    VideoId = table.Column<string>(maxLength: 60, nullable: true),
                    Name = table.Column<string>(maxLength: 250, nullable: true),
                    Description = table.Column<string>(maxLength: 1024, nullable: true),
                    IsWatched = table.Column<bool>(nullable: false),
                    IsNew = table.Column<bool>(nullable: false),
                    DownloadedPath = table.Column<string>(maxLength: 260, nullable: true),
                    DownloadedSize = table.Column<int>(nullable: true),
                    SubscriptionId = table.Column<int>(nullable: false),
                    PlaylistIndex = table.Column<int>(nullable: false),
                    Published = table.Column<DateTime>(nullable: false),
                    LastUpdated = table.Column<DateTime>(nullable: false),
                    ThumbnailPath = table.Column<string>(maxLength: 2048, nullable: true),
                    UploaderName = table.Column<string>(nullable: true),
                    Views = table.Column<int>(nullable: true),
                    Rating = table.Column<float>(nullable: true),
                    ProviderData = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFolders_ParentId",
                table: "SubscriptionFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFolders_UserId",
                table: "SubscriptionFolders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ParentFolderId",
                table: "Subscriptions",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_SubscriptionId",
                table: "Videos",
                column: "SubscriptionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionFolders");
        }
    }
}
