using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManagerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Manager was collapsed into Admin (2026-08-10, see CLAUDE.md's Access model
            // section) — reassign any existing AspNetUserRoles membership before the role row
            // itself is deleted below, or the FK delete would fail/orphan data for any
            // environment that already had a Manager-role user.
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUserRoles"
                SET "RoleId" = 'f3b1f4a0-1c4a-4a3e-9c1a-000000000001'
                WHERE "RoleId" = 'f3b1f4a0-1c4a-4a3e-9c1a-000000000002';
                """);

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000002"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreates the Manager role row, but deliberately does not reverse the Up()
            // reassignment above — there is no record of which users were originally in Manager
            // vs Admin, so that half of this migration is not reversible.
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { new Guid("f3b1f4a0-1c4a-4a3e-9c1a-000000000002"), "f3b1f4a0-1c4a-4a3e-9c1a-000000000002", "Manager", "MANAGER" });
        }
    }
}
