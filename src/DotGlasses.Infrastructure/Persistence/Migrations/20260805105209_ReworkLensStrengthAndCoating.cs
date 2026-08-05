using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReworkLensStrengthAndCoating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddPower",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "IsBifocal",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "SphericalPower",
                table: "LensOptions");

            migrationBuilder.RenameColumn(
                name: "CoatingId",
                table: "LensOptions",
                newName: "LensStrengthRefId");

            migrationBuilder.RenameIndex(
                name: "IX_LensOptions_CoatingId",
                table: "LensOptions",
                newName: "IX_LensOptions_LensStrengthRefId");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PresetCatalogues",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RangeDescription",
                table: "PresetCatalogues",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LensStrengthCoatingOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LensStrengthRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LensStrengthCoatingOptions", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000001"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000044"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000002"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000046"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000003"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000047"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000004"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000049"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000005"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000052"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000006"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000054"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000007"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000056"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000008"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000058"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000009"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000043"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000010"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000045"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000011"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000046"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000012"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000047"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000013"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000048"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000014"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000049"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000015"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000050"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000016"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000051"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000017"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000053"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000018"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000055"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000019"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000057"));

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000020"),
                column: "LensStrengthRefId",
                value: new Guid("b0000000-0000-0000-0000-000000000058"));

            migrationBuilder.InsertData(
                table: "LensStrengthCoatingOptions",
                columns: new[] { "Id", "CoatingRefId", "CreatedAtUtc", "LensStrengthRefId" },
                values: new object[,]
                {
                    { new Guid("e1000000-0000-0000-0000-000000000001"), new Guid("b0000000-0000-0000-0000-000000000023"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b0000000-0000-0000-0000-000000000055") },
                    { new Guid("e1000000-0000-0000-0000-000000000002"), new Guid("b0000000-0000-0000-0000-000000000023"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b0000000-0000-0000-0000-000000000056") },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), new Guid("b0000000-0000-0000-0000-000000000023"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b0000000-0000-0000-0000-000000000057") },
                    { new Guid("e1000000-0000-0000-0000-000000000004"), new Guid("b0000000-0000-0000-0000-000000000023"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b0000000-0000-0000-0000-000000000058") }
                });

            migrationBuilder.UpdateData(
                table: "PresetCatalogues",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000001"),
                columns: new[] { "Description", "RangeDescription" },
                values: new object[] { "Standard six-option lens range for outlets with local stock.", "+2.50 to -4.50" });

            migrationBuilder.UpdateData(
                table: "PresetCatalogues",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000002"),
                columns: new[] { "Description", "RangeDescription" },
                values: new object[] { "Extended nine-option lens range for outlets with wider stock.", "+3.00 to -4.00" });

            migrationBuilder.InsertData(
                table: "ReferenceDataItems",
                columns: new[] { "Id", "Category", "Code", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "ImageUrl", "IsActive", "IsDeleted", "IsOtherOption", "Label", "ModifiedAtUtc", "ModifiedBy", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("b0000000-0000-0000-0000-000000000043"), 6, "plus_3_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+3.00", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000044"), 6, "plus_2_50", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+2.50", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000045"), 6, "plus_2_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+2.00", null, null, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000046"), 6, "plus_1_25", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+1.25", null, null, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000047"), 6, "plus_0_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+0.00", null, null, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000048"), 6, "minus_1_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-1.00", null, null, 5 },
                    { new Guid("b0000000-0000-0000-0000-000000000049"), 6, "minus_1_50", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-1.50", null, null, 6 },
                    { new Guid("b0000000-0000-0000-0000-000000000050"), 6, "minus_2_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-2.00", null, null, 7 },
                    { new Guid("b0000000-0000-0000-0000-000000000051"), 6, "minus_2_50", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-2.50", null, null, 8 },
                    { new Guid("b0000000-0000-0000-0000-000000000052"), 6, "minus_3_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-3.00", null, null, 9 },
                    { new Guid("b0000000-0000-0000-0000-000000000053"), 6, "minus_4_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-4.00", null, null, 10 },
                    { new Guid("b0000000-0000-0000-0000-000000000054"), 6, "minus_4_50", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "-4.50", null, null, 11 },
                    { new Guid("b0000000-0000-0000-0000-000000000055"), 6, "bifocal_0_00_3_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+0.00 / +3.00 (Bifocal)", null, null, 12 },
                    { new Guid("b0000000-0000-0000-0000-000000000056"), 6, "bifocal_0_00_2_50", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+0.00 / +2.50 (Bifocal)", null, null, 13 },
                    { new Guid("b0000000-0000-0000-0000-000000000057"), 6, "bifocal_0_00_2_00", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+0.00 / +2.00 (Bifocal)", null, null, 14 },
                    { new Guid("b0000000-0000-0000-0000-000000000058"), 6, "bifocal_0_00_1_25", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "+0.00 / +1.25 (Bifocal)", null, null, 15 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LensStrengthCoatingOptions_CoatingRefId",
                table: "LensStrengthCoatingOptions",
                column: "CoatingRefId");

            migrationBuilder.CreateIndex(
                name: "IX_LensStrengthCoatingOptions_LensStrengthRefId",
                table: "LensStrengthCoatingOptions",
                column: "LensStrengthRefId");

            migrationBuilder.CreateIndex(
                name: "IX_LensStrengthCoatingOptions_LensStrengthRefId_CoatingRefId",
                table: "LensStrengthCoatingOptions",
                columns: new[] { "LensStrengthRefId", "CoatingRefId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LensStrengthCoatingOptions");

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000043"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000044"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000045"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000046"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000047"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000051"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000052"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000053"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000054"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000055"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000056"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000057"));

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000058"));

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PresetCatalogues");

            migrationBuilder.DropColumn(
                name: "RangeDescription",
                table: "PresetCatalogues");

            migrationBuilder.RenameColumn(
                name: "LensStrengthRefId",
                table: "LensOptions",
                newName: "CoatingId");

            migrationBuilder.RenameIndex(
                name: "IX_LensOptions_LensStrengthRefId",
                table: "LensOptions",
                newName: "IX_LensOptions_CoatingId");

            migrationBuilder.AddColumn<decimal>(
                name: "AddPower",
                table: "LensOptions",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBifocal",
                table: "LensOptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SphericalPower",
                table: "LensOptions",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000001"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 2.50m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000002"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 1.25m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000003"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 0.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000004"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -1.50m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000005"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -3.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000006"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -4.50m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000007"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { 2.50m, new Guid("b0000000-0000-0000-0000-000000000023"), true, 0.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000008"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { 1.25m, new Guid("b0000000-0000-0000-0000-000000000023"), true, 0.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000009"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 3.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000010"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 2.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000011"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 1.25m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000012"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, 0.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000013"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -1.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000014"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -1.50m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000015"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -2.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000016"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -2.50m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000017"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { null, new Guid("b0000000-0000-0000-0000-000000000024"), false, -4.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000018"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { 3.00m, new Guid("b0000000-0000-0000-0000-000000000023"), true, 0.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000019"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { 2.00m, new Guid("b0000000-0000-0000-0000-000000000023"), true, 0.00m });

            migrationBuilder.UpdateData(
                table: "LensOptions",
                keyColumn: "Id",
                keyValue: new Guid("d0000000-0000-0000-0000-000000000020"),
                columns: new[] { "AddPower", "CoatingId", "IsBifocal", "SphericalPower" },
                values: new object[] { 1.25m, new Guid("b0000000-0000-0000-0000-000000000023"), true, 0.00m });
        }
    }
}
