using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueOrganisationPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An environment that already holds two nodes on one path can't take the unique index
            // below, and must not have it "fixed" by this migration: which org keeps the path, and
            // which org the rows stamped with it belong to, is a judgement call. Fail before
            // changing anything, naming the paths and pointing at the repair.
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicated text;
                BEGIN
                    SELECT string_agg(DISTINCT "HierarchyPath", ', ') INTO duplicated
                    FROM "OrganisationNodes"
                    WHERE "HierarchyPath" IN (
                        SELECT "HierarchyPath" FROM "OrganisationNodes"
                        GROUP BY "HierarchyPath" HAVING COUNT(*) > 1);

                    IF duplicated IS NOT NULL THEN
                        RAISE EXCEPTION 'OrganisationNodes holds duplicate HierarchyPaths (%). Repair them before applying this migration — see .scratch/triage-2026-09-26/issues/02-repair-duplicate-org-paths-in-affected-environment.md.', duplicated;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_OrganisationNodes_HierarchyPath",
                table: "OrganisationNodes");

            migrationBuilder.CreateSequence<int>(
                name: "OrganisationPathSegments");

            // Start above every segment already minted, deactivated nodes included — a
            // deactivated node keeps its path and can be reactivated, so its segments are spent.
            // Derived from the data rather than hard-coded: every environment has minted a
            // different number of nodes. With no numeric segment at all, is_called = false makes
            // the first nextval return 1.
            migrationBuilder.Sql("""
                SELECT setval('"OrganisationPathSegments"', GREATEST(max_segment, 1), max_segment > 0)
                FROM (
                    SELECT COALESCE(MAX(segment::int), 0) AS max_segment
                    FROM "OrganisationNodes",
                         unnest(string_to_array(trim(both '/' from "HierarchyPath"), '/')) AS segment
                    WHERE segment ~ '^[0-9]+$'
                ) AS existing;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationNodes_HierarchyPath",
                table: "OrganisationNodes",
                column: "HierarchyPath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganisationNodes_HierarchyPath",
                table: "OrganisationNodes");

            migrationBuilder.DropSequence(
                name: "OrganisationPathSegments");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationNodes_HierarchyPath",
                table: "OrganisationNodes",
                column: "HierarchyPath");
        }
    }
}
