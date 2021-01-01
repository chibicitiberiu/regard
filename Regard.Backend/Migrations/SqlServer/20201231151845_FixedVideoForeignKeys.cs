using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SqlServer
{
    public partial class FixedVideoForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionFolders_AspNetUsers_UserId",
                table: "SubscriptionFolders");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionFolders_ParentFolderId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Subscriptions",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "SubscriptionFolders",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionFolders_AspNetUsers_UserId",
                table: "SubscriptionFolders",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionFolders_ParentFolderId",
                table: "Subscriptions",
                column: "ParentFolderId",
                principalTable: "SubscriptionFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionFolders_AspNetUsers_UserId",
                table: "SubscriptionFolders");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionFolders_ParentFolderId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Subscriptions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "SubscriptionFolders",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionFolders_AspNetUsers_UserId",
                table: "SubscriptionFolders",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionFolders_ParentFolderId",
                table: "Subscriptions",
                column: "ParentFolderId",
                principalTable: "SubscriptionFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
