using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR-0006: a user's access is their org assignments, and the user row stops carrying a
    /// single "active org". Until now that org counted as an assignment even with no
    /// UserOrgAssignment row behind it, so it is turned into a real one before the columns go —
    /// otherwise its scope would vanish the moment this ships (spec user story 41).
    /// Tests/Leads/Sales are not touched: records already stamped above retail-point level stay
    /// as they are.
    /// </summary>
    public partial class RemoveUserActiveOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only where no row exists yet (the unique index on (UserId, OrgNodeId) would refuse a
            // duplicate), so re-running it is harmless. A deactivated org is backfilled too: the
            // assignment still belongs to the user and comes back into force on reactivation.
            // The FK on OrgNodeId guarantees the org row exists.
            migrationBuilder.Sql("""
                INSERT INTO "UserOrgAssignments" ("Id", "UserId", "OrgNodeId", "CreatedAtUtc")
                SELECT gen_random_uuid(), u."Id", u."OrgNodeId", now()
                FROM "AspNetUsers" u
                WHERE u."OrgNodeId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "UserOrgAssignments" a
                      WHERE a."UserId" = u."Id" AND a."OrgNodeId" = u."OrgNodeId");
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_OrganisationNodes_OrgNodeId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrgNodeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HierarchyPath",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrgLevel",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrgNodeId",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy on purpose: the columns come back in the shape an unassigned account had
            // (HierarchyPath "", OrgNodeId/OrgLevel null) — which org was "active" is gone for
            // good. Nothing is lost access-wise: every former active org is an assignment row
            // after Up, and the code before this migration counted those rows as access too.
            migrationBuilder.AddColumn<string>(
                name: "HierarchyPath",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OrgLevel",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrgNodeId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrgNodeId",
                table: "AspNetUsers",
                column: "OrgNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_OrganisationNodes_OrgNodeId",
                table: "AspNetUsers",
                column: "OrgNodeId",
                principalTable: "OrganisationNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
