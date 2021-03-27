using Microsoft.EntityFrameworkCore.Migrations;

namespace Regard.Backend.Migrations.SqlServer
{
    public partial class ChangeDownloadedSizeToLong : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "DownloadedSize",
                table: "Videos",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DownloadedSize",
                table: "Videos",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldNullable: true);
        }
    }
}
