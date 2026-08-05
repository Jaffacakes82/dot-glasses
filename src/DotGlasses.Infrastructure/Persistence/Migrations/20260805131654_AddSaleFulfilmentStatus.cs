using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleFulfilmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FulfilmentStatus",
                table: "Sales",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfilmentStatus",
                table: "Sales");
        }
    }
}
