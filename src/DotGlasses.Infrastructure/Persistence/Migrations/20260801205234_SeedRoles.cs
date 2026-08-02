using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000001"), "f3b1f4a0-1c4a-4a3e-9c1a-000000000001", "Admin", "ADMIN" },
                    { new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000002"), "f3b1f4a0-1c4a-4a3e-9c1a-000000000002", "Manager", "MANAGER" },
                    { new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000003"), "f3b1f4a0-1c4a-4a3e-9c1a-000000000003", "User", "USER" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000001"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000002"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000003"));
        }
    }
}
