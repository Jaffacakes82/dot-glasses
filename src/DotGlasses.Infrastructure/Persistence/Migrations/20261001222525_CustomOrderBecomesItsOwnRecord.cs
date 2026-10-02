using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomOrderBecomesItsOwnRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HierarchyPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeadCoatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadCoatings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomOrders_HierarchyPath",
                table: "CustomOrders",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_CustomOrders_LeadId",
                table: "CustomOrders",
                column: "LeadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomOrders_SaleId",
                table: "CustomOrders",
                column: "SaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadCoatings_LeadId",
                table: "LeadCoatings",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadCoatings_LeadId_CoatingRefId",
                table: "LeadCoatings",
                columns: new[] { "LeadId", "CoatingRefId" },
                unique: true);

            // Hand-written: the scaffold dropped the two Sale columns first, which would have lost
            // every existing order. Each Sale with a fulfilment status becomes one order — same
            // retail point, same status, placed when the Sale was recorded — and only then do
            // the columns go (ADR-0008).
            migrationBuilder.Sql("""
                INSERT INTO "CustomOrders"
                    ("Id", "HierarchyPath", "Status", "PlacedAtUtc", "LeadId", "SaleId",
                     "CreatedAtUtc", "CreatedBy", "ModifiedAtUtc", "ModifiedBy", "IsDeleted", "DeletedAtUtc", "DeletedBy")
                SELECT gen_random_uuid(), s."HierarchyPath", s."FulfilmentStatus", s."CreatedAtUtc", NULL, s."Id",
                       s."CreatedAtUtc", s."CreatedBy", s."ModifiedAtUtc", s."ModifiedBy", s."IsDeleted", s."DeletedAtUtc", s."DeletedBy"
                FROM "Sales" s
                WHERE s."FulfilmentStatus" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "FulfilmentStatus",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OrderFromDotGlasses",
                table: "Sales");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FulfilmentStatus",
                table: "Sales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OrderFromDotGlasses",
                table: "Sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Back onto the Sale, before the table goes. An order no Sale has paid for yet (one a
            // Lead placed) has nowhere to go in the old shape and is lost on the way down.
            migrationBuilder.Sql("""
                UPDATE "Sales" s
                SET "OrderFromDotGlasses" = TRUE, "FulfilmentStatus" = o."Status"
                FROM "CustomOrders" o
                WHERE o."SaleId" = s."Id";
                """);

            migrationBuilder.DropTable(
                name: "CustomOrders");

            migrationBuilder.DropTable(
                name: "LeadCoatings");
        }
    }
}
