using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoatingSetsWithPairingAndExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoatingExclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefIdA = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefIdB = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoatingExclusions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoatingPairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerCoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairedCoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoatingPairings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleCoatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleCoatings", x => x.Id);
                });

            // Backfill: one SaleCoatings row per existing Sale, carrying its former single
            // CoatingRefId forward into the new set-shaped model (Sale's coating requirement was
            // already compulsory, so every row has a value) — must run before the DropColumn
            // below, or the value being backfilled would already be gone.
            migrationBuilder.Sql(
                """
                INSERT INTO "SaleCoatings" ("Id", "SaleId", "CoatingRefId", "CreatedAtUtc")
                SELECT gen_random_uuid(), "Id", "CoatingRefId", "CreatedAtUtc" FROM "Sales";
                """);

            migrationBuilder.DropColumn(
                name: "CoatingRefId",
                table: "Sales");

            migrationBuilder.InsertData(
                table: "ReferenceDataItems",
                columns: new[] { "Id", "Category", "Code", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "ImageUrl", "IsActive", "IsDeleted", "IsOtherOption", "Label", "ModifiedAtUtc", "ModifiedBy", "SortOrder" },
                values: new object[] { new Guid("b0000000-0000-0000-0000-000000000062"), 3, "anti_glare", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, true, false, false, "Anti-glare", null, null, 5 });

            migrationBuilder.CreateIndex(
                name: "IX_CoatingExclusions_CoatingRefIdA",
                table: "CoatingExclusions",
                column: "CoatingRefIdA");

            migrationBuilder.CreateIndex(
                name: "IX_CoatingExclusions_CoatingRefIdA_CoatingRefIdB",
                table: "CoatingExclusions",
                columns: new[] { "CoatingRefIdA", "CoatingRefIdB" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoatingExclusions_CoatingRefIdB",
                table: "CoatingExclusions",
                column: "CoatingRefIdB");

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
                name: "IX_SaleCoatings_SaleId",
                table: "SaleCoatings",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleCoatings_SaleId_CoatingRefId",
                table: "SaleCoatings",
                columns: new[] { "SaleId", "CoatingRefId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoatingExclusions");

            migrationBuilder.DropTable(
                name: "CoatingPairings");

            migrationBuilder.DropTable(
                name: "SaleCoatings");

            migrationBuilder.DeleteData(
                table: "ReferenceDataItems",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000062"));

            migrationBuilder.AddColumn<Guid>(
                name: "CoatingRefId",
                table: "Sales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
