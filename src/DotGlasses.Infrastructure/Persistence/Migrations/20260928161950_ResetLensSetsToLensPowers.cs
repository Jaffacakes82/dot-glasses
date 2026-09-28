using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResetLensSetsToLensPowers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The reset (lens-power spec, ADR-0007): existing lens-set data is retired and emptied,
            // not converted — a lens-strength label can't be turned into a lens power reliably,
            // and the product isn't live. It runs first, in the same migration as the schema
            // change, so no row ever exists in a half-converted shape.
            //
            // Hand-edited. Removing the lens sets' HasData seeds made EF scaffold DeleteData for
            // the two seeded lens sets; a hard delete would leave every historical record that
            // names them by PresetCatalogueId unable to resolve its set's name. They are retired
            // here instead, along with every other lens set. The scaffolded DeleteData for the
            // seeded lenses, assignments and Lens strength items is replaced by the whole-table
            // deletes below, which cover admin-created rows as well as seeded ones.
            //
            // - Every lens set is retired (soft-deleted), keeping its name for old records.
            // - Their lenses and assignments are deleted: a retired set is offered nowhere, and a
            //   reactivated one comes back empty and unassigned rather than with rows in the old
            //   shape. Records keep LensRangeType/PresetCatalogueId; the lens ids some still carry
            //   (until lens-power ticket 05 drops them) simply resolve to nothing.
            // - Every "Lens strength" reference item (category 6, now retired and reserved) is
            //   deleted — nothing references one by id once the lenses are gone.
            // - The global coating availability grid and global pairings go with their tables,
            //   dropped below. Coating exclusions are untouched.
            migrationBuilder.Sql(
                """
                UPDATE "PresetCatalogues"
                SET "IsDeleted" = TRUE,
                    "DeletedAtUtc" = now(),
                    "DeletedBy" = 'Lens set reset (ADR-0007)'
                WHERE NOT "IsDeleted";

                DELETE FROM "LensOptions";
                DELETE FROM "PresetCatalogueAssignments";
                DELETE FROM "ReferenceDataItems" WHERE "Category" = 6;
                """);

            migrationBuilder.DropTable(
                name: "CoatingPairings");

            migrationBuilder.DropTable(
                name: "LensStrengthCoatingOptions");

            migrationBuilder.DropIndex(
                name: "IX_LensOptions_LensStrengthRefId",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "LensStrengthRefId",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "LensOptions");

            migrationBuilder.AddColumn<decimal>(
                name: "Add",
                table: "LensOptions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Axis",
                table: "LensOptions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cylinder",
                table: "LensOptions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "LensOptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LensTypeOtherText",
                table: "LensOptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensTypeRefId",
                table: "LensOptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Sphere",
                table: "LensOptions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "LensOptionCoatingPairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LensOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerCoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairedCoatingRefId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LensOptionCoatingPairings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LensOptionCoatingPairings_LensOptions_LensOptionId",
                        column: x => x.LensOptionId,
                        principalTable: "LensOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LensOptionCoatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LensOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LensOptionCoatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LensOptionCoatings_LensOptions_LensOptionId",
                        column: x => x.LensOptionId,
                        principalTable: "LensOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LensOptionCoatingPairings_LensOptionId_TriggerCoatingRefId_~",
                table: "LensOptionCoatingPairings",
                columns: new[] { "LensOptionId", "TriggerCoatingRefId", "PairedCoatingRefId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LensOptionCoatings_LensOptionId_CoatingRefId",
                table: "LensOptionCoatings",
                columns: new[] { "LensOptionId", "CoatingRefId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best effort only: the reset is not reversible. This restores the old schema and its
            // seed rows; lens sets built in the new shape are emptied, since a lens power can't be
            // expressed as a Lens strength reference.
            migrationBuilder.Sql("""DELETE FROM "LensOptions";""");

            migrationBuilder.DropTable(
                name: "LensOptionCoatingPairings");

            migrationBuilder.DropTable(
                name: "LensOptionCoatings");

            migrationBuilder.DropColumn(
                name: "Add",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "Axis",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "Cylinder",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "LensTypeOtherText",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "LensTypeRefId",
                table: "LensOptions");

            migrationBuilder.DropColumn(
                name: "Sphere",
                table: "LensOptions");

            migrationBuilder.AddColumn<Guid>(
                name: "LensStrengthRefId",
                table: "LensOptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "LensOptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CoatingPairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PairedCoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerCoatingRefId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoatingPairings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LensStrengthCoatingOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LensStrengthRefId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LensStrengthCoatingOptions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LensOptions",
                columns: new[] { "Id", "LensStrengthRefId", "PresetCatalogueId", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("d0000000-0000-0000-0000-000000000001"), new Guid("b0000000-0000-0000-0000-000000000044"), new Guid("c0000000-0000-0000-0000-000000000001"), 0 },
                    { new Guid("d0000000-0000-0000-0000-000000000002"), new Guid("b0000000-0000-0000-0000-000000000046"), new Guid("c0000000-0000-0000-0000-000000000001"), 1 },
                    { new Guid("d0000000-0000-0000-0000-000000000003"), new Guid("b0000000-0000-0000-0000-000000000047"), new Guid("c0000000-0000-0000-0000-000000000001"), 2 },
                    { new Guid("d0000000-0000-0000-0000-000000000004"), new Guid("b0000000-0000-0000-0000-000000000049"), new Guid("c0000000-0000-0000-0000-000000000001"), 3 },
                    { new Guid("d0000000-0000-0000-0000-000000000005"), new Guid("b0000000-0000-0000-0000-000000000052"), new Guid("c0000000-0000-0000-0000-000000000001"), 4 },
                    { new Guid("d0000000-0000-0000-0000-000000000006"), new Guid("b0000000-0000-0000-0000-000000000054"), new Guid("c0000000-0000-0000-0000-000000000001"), 5 },
                    { new Guid("d0000000-0000-0000-0000-000000000007"), new Guid("b0000000-0000-0000-0000-000000000056"), new Guid("c0000000-0000-0000-0000-000000000001"), 6 },
                    { new Guid("d0000000-0000-0000-0000-000000000008"), new Guid("b0000000-0000-0000-0000-000000000058"), new Guid("c0000000-0000-0000-0000-000000000001"), 7 },
                    { new Guid("d0000000-0000-0000-0000-000000000009"), new Guid("b0000000-0000-0000-0000-000000000043"), new Guid("c0000000-0000-0000-0000-000000000002"), 0 },
                    { new Guid("d0000000-0000-0000-0000-000000000010"), new Guid("b0000000-0000-0000-0000-000000000045"), new Guid("c0000000-0000-0000-0000-000000000002"), 1 },
                    { new Guid("d0000000-0000-0000-0000-000000000011"), new Guid("b0000000-0000-0000-0000-000000000046"), new Guid("c0000000-0000-0000-0000-000000000002"), 2 },
                    { new Guid("d0000000-0000-0000-0000-000000000012"), new Guid("b0000000-0000-0000-0000-000000000047"), new Guid("c0000000-0000-0000-0000-000000000002"), 3 },
                    { new Guid("d0000000-0000-0000-0000-000000000013"), new Guid("b0000000-0000-0000-0000-000000000048"), new Guid("c0000000-0000-0000-0000-000000000002"), 4 },
                    { new Guid("d0000000-0000-0000-0000-000000000014"), new Guid("b0000000-0000-0000-0000-000000000049"), new Guid("c0000000-0000-0000-0000-000000000002"), 5 },
                    { new Guid("d0000000-0000-0000-0000-000000000015"), new Guid("b0000000-0000-0000-0000-000000000050"), new Guid("c0000000-0000-0000-0000-000000000002"), 6 },
                    { new Guid("d0000000-0000-0000-0000-000000000016"), new Guid("b0000000-0000-0000-0000-000000000051"), new Guid("c0000000-0000-0000-0000-000000000002"), 7 },
                    { new Guid("d0000000-0000-0000-0000-000000000017"), new Guid("b0000000-0000-0000-0000-000000000053"), new Guid("c0000000-0000-0000-0000-000000000002"), 8 },
                    { new Guid("d0000000-0000-0000-0000-000000000018"), new Guid("b0000000-0000-0000-0000-000000000055"), new Guid("c0000000-0000-0000-0000-000000000002"), 9 },
                    { new Guid("d0000000-0000-0000-0000-000000000019"), new Guid("b0000000-0000-0000-0000-000000000057"), new Guid("c0000000-0000-0000-0000-000000000002"), 10 },
                    { new Guid("d0000000-0000-0000-0000-000000000020"), new Guid("b0000000-0000-0000-0000-000000000058"), new Guid("c0000000-0000-0000-0000-000000000002"), 11 }
                });

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

            migrationBuilder.InsertData(
                table: "PresetCatalogueAssignments",
                columns: new[] { "Id", "CreatedAtUtc", "OrgNodeId", "PresetCatalogueId" },
                values: new object[,]
                {
                    { new Guid("e0000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("a0000000-0000-0000-0000-000000000002"), new Guid("c0000000-0000-0000-0000-000000000001") },
                    { new Guid("e0000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("a0000000-0000-0000-0000-000000000002"), new Guid("c0000000-0000-0000-0000-000000000002") }
                });

            // Up retired the two seeded lens sets rather than deleting them, so they are restored
            // in place rather than re-inserted.
            migrationBuilder.Sql(
                """
                UPDATE "PresetCatalogues"
                SET "IsDeleted" = FALSE, "DeletedAtUtc" = NULL, "DeletedBy" = NULL
                WHERE "Id" IN ('c0000000-0000-0000-0000-000000000001', 'c0000000-0000-0000-0000-000000000002');
                """);

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
                name: "IX_LensOptions_LensStrengthRefId",
                table: "LensOptions",
                column: "LensStrengthRefId");

            migrationBuilder.CreateIndex(
                name: "IX_CoatingPairings_PairedCoatingRefId",
                table: "CoatingPairings",
                column: "PairedCoatingRefId");

            migrationBuilder.CreateIndex(
                name: "IX_CoatingPairings_TriggerCoatingRefId",
                table: "CoatingPairings",
                column: "TriggerCoatingRefId");

            migrationBuilder.CreateIndex(
                name: "IX_CoatingPairings_TriggerCoatingRefId_PairedCoatingRefId",
                table: "CoatingPairings",
                columns: new[] { "TriggerCoatingRefId", "PairedCoatingRefId" },
                unique: true);

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
    }
}
