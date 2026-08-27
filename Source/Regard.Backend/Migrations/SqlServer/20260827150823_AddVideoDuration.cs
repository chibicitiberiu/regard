using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regard.Backend.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddVideoDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Videos",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Videos");
        }
    }
}
