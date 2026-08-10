using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInertFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tests_CustomerId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CanHandleCustomOrders",
                table: "OrganisationNodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanHandleCustomOrders",
                table: "OrganisationNodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CanHandleCustomOrders",
                value: false);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CanHandleCustomOrders",
                value: true);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CanHandleCustomOrders",
                value: false);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CanHandleCustomOrders",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tests_CustomerId",
                table: "Tests",
                column: "CustomerId");
        }
    }
}
