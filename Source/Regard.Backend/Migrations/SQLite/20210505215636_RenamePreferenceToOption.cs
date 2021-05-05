using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SQLite
{
    public partial class RenamePreferenceToOption : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Preferences",
                newName: "Options");

            migrationBuilder.RenameTable(
                name: "SubscriptionFolderPreferences",
                newName: "SubscriptionFolderOptions");

            migrationBuilder.RenameTable(
                name: "SubscriptionPreferences",
                newName: "SubscriptionOptions");

            migrationBuilder.RenameTable(
                name: "UserPreferences",
                newName: "UserOptions");

            migrationBuilder.RenameIndex(name: "PK_Preferences", newName: "PK_Options");
            migrationBuilder.RenameIndex(name: "PK_SubscriptionFolderPreferences", newName: "PK_SubscriptionFolderOptions");
            migrationBuilder.RenameIndex(name: "PK_SubscriptionPreferences", newName: "PK_SubscriptionOptions");
            migrationBuilder.RenameIndex(name: "PK_UserPreferences", newName: "PK_UserOptions");
            migrationBuilder.RenameIndex(name: "PK_UserPreferences", newName: "PK_UserOptions");
            migrationBuilder.RenameIndex(name: "IX_FolderPreferences_SubscriptionFolderId", newName: "IX_FolderOptions_SubscriptionFolderId");
            migrationBuilder.RenameIndex(name: "IX_SubscriptionPreferences_SubscriptionFolderId", newName: "IX_SubscriptionOptions_SubscriptionId");
            migrationBuilder.RenameIndex(name: "IX_UserPreferences_UserId", newName: "IX_UserOptions_UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Options",
                newName: "Preferences");

            migrationBuilder.RenameTable(
                name: "SubscriptionFolderOptions",
                newName: "SubscriptionFolderPreferences");

            migrationBuilder.RenameTable(
                name: "SubscriptionOptions",
                newName: "SubscriptionPreferences");

            migrationBuilder.RenameTable(
                name: "UserOptions",
                newName: "UserPreferences");

            migrationBuilder.RenameIndex(name: "PK_Options", newName: "PK_Preferences");
            migrationBuilder.RenameIndex(name: "PK_SubscriptionFolderOptions", newName: "PK_SubscriptionFolderPreferences");
            migrationBuilder.RenameIndex(name: "PK_SubscriptionOptions", newName: "PK_SubscriptionPreferences");
            migrationBuilder.RenameIndex(name: "PK_UserOptions", newName: "PK_UserPreferences");
            migrationBuilder.RenameIndex(name: "PK_UserOptions", newName: "PK_UserPreferences");
            migrationBuilder.RenameIndex(name: "IX_FolderOptions_SubscriptionFolderId", newName: "IX_FolderPreferences_SubscriptionFolderId");
            migrationBuilder.RenameIndex(name: "IX_SubscriptionOptions_SubscriptionId", newName: "IX_SubscriptionPreferences_SubscriptionFolderId");
            migrationBuilder.RenameIndex(name: "IX_UserOptions_UserId", newName: "IX_UserPreferences_UserId");
        }
    }
}
