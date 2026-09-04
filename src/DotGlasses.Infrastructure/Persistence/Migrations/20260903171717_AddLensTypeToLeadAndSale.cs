using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLensTypeToLeadAndSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LensTypeOtherText",
                table: "Sales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensTypeRefId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LensTypeOtherText",
                table: "Leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensTypeRefId",
                table: "Leads",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LensTypeOtherText",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LensTypeRefId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LensTypeOtherText",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LensTypeRefId",
                table: "Leads");
        }
    }
}
