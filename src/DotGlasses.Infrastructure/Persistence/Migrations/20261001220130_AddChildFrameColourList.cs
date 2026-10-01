using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChildFrameColourList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ReferenceDataItems",
                columns: new[] { "Id", "Category", "Code", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "ImageUrl", "IsActive", "IsDeleted", "IsOtherOption", "Label", "ModifiedAtUtc", "ModifiedBy", "SortOrder" },
                values: new object[] { new Guid("b0000000-0000-0000-0000-000000000063"), 8, "other", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, true, "Other", null, null, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000063"));
        }
    }
}
