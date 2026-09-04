using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFrameColourIconsAndPinkRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000028"),
                column: "ImageUrl",
                value: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e780b5efa4a_Black_white.png");

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000029"),
                column: "ImageUrl",
                value: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e780b5efd97_Blue_white_1.png");

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000035"),
                column: "ImageUrl",
                value: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e7927c5ea8c_Blue_white.png");

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000036"),
                column: "ImageUrl",
                value: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e7927c5ee4e_Brown_white.png");

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000037"),
                columns: new[] { "Code", "ImageUrl", "Label" },
                values: new object[] { "pink", "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e780b5ef6b9_Purple_white.png", "Pink" });

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000038"),
                columns: new[] { "Code", "ImageUrl", "Label" },
                values: new object[] { "pink_black", "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e7914719e10_Purple_white_1.png", "Pink Black" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000028"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000029"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000035"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000036"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000037"),
                columns: new[] { "Code", "ImageUrl", "Label" },
                values: new object[] { "purple", null, "Purple" });

            migrationBuilder.UpdateData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000038"),
                columns: new[] { "Code", "ImageUrl", "Label" },
                values: new object[] { "purple_black", null, "Purple-Black" });
        }
    }
}
