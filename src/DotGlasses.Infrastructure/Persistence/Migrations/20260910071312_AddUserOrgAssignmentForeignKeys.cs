using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOrgAssignmentForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive cleanup before the constraints below can start rejecting writes: any row
            // that predates this migration and happens to reference a nonexistent org/user is
            // repaired rather than left to fail AddForeignKey outright. AspNetUsers rows are
            // reset to the same "unassigned" shape DevUserSeeder/UserAdminService already produce
            // and the app already handles (see the empty-HierarchyPath fail-closed fix in
            // DotGlassesDbContext's query filter); UserOrgAssignments rows are simply orphaned
            // rows with nothing left to point at, so they're removed outright.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "OrgNodeId" = NULL, "HierarchyPath" = '', "OrgLevel" = NULL
                WHERE "OrgNodeId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "OrganisationNodes" o WHERE o."Id" = "AspNetUsers"."OrgNodeId");
                """);

            migrationBuilder.Sql("""
                DELETE FROM "UserOrgAssignments" a
                WHERE NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."Id" = a."UserId")
                   OR NOT EXISTS (SELECT 1 FROM "OrganisationNodes" o WHERE o."Id" = a."OrgNodeId");
                """);

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

            migrationBuilder.AddForeignKey(
                name: "FK_UserOrgAssignments_AspNetUsers_UserId",
                table: "UserOrgAssignments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserOrgAssignments_OrganisationNodes_OrgNodeId",
                table: "UserOrgAssignments",
                column: "OrgNodeId",
                principalTable: "OrganisationNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_OrganisationNodes_OrgNodeId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserOrgAssignments_AspNetUsers_UserId",
                table: "UserOrgAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_UserOrgAssignments_OrganisationNodes_OrgNodeId",
                table: "UserOrgAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrgNodeId",
                table: "AspNetUsers");
        }
    }
}
