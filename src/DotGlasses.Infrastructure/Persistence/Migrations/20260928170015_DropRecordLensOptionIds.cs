using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRecordLensOptionIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LensOptionLeftId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensOptionRightId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensOptionLeftId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LensOptionRightId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LensOptionLeftId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LensOptionRightId",
                table: "Leads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionLeftId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionRightId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionLeftId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionRightId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionLeftId",
                table: "Leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionRightId",
                table: "Leads",
                type: "uuid",
                nullable: true);
        }
    }
}
