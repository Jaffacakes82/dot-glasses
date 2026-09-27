using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollapseLensRangeToLensSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-0005: 6-Lens (0) and 9-Lens (1) become one LensSet (0). Which set a record used
            // is PresetCatalogueId, untouched here, so nothing is lost. Custom keeps its value 2.
            migrationBuilder.Sql("""UPDATE "Tests" SET "LensRangeType" = 0 WHERE "LensRangeType" = 1;""");
            migrationBuilder.Sql("""UPDATE "Leads" SET "LensRangeType" = 0 WHERE "LensRangeType" = 1;""");
            migrationBuilder.Sql("""UPDATE "Sales" SET "LensRangeType" = 0 WHERE "LensRangeType" = 1;""");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PresetCatalogues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "PresetCatalogues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "PresetCatalogues",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000001"),
                column: "Kind",
                value: 1);

            migrationBuilder.UpdateData(
                table: "PresetCatalogues",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000002"),
                column: "Kind",
                value: 2);

            // The best inverse available: a record on the lens set that was the 9-Lens one goes back
            // to NineLensSet (1). Any other lens set collapses to SixLensSet (0), the old type had no
            // way to say otherwise.
            foreach (var table in new[] { "Tests", "Leads", "Sales" })
            {
                migrationBuilder.Sql($"""UPDATE "{table}" SET "LensRangeType" = 1 WHERE "LensRangeType" = 0 AND "PresetCatalogueId" = 'c0000000-0000-0000-0000-000000000002';""");
            }
        }
    }
}
