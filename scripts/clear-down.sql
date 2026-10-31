-- Clear-down: empties an environment back to "DGI and its users".
--
-- Written against the schema as of migration snapshot 2026-10-02 (CustomOrders / LeadCoatings
-- included). If a later migration adds a table, add it here.
--
-- KEEPS
--   * The DGI organisation (OrganisationNodes, Level = 0).
--   * Every user with an assignment to DGI — their sign-in, role, claims and tokens — and that
--     one assignment. Any other assignment they hold goes with the organisation it pointed at.
--   * AspNetRoles / AspNetRoleClaims (Admin, User — seeded by migration).
--   * Reference data and coating exclusions (see the optional block at the bottom).
--   * __EFMigrationsHistory and the OrganisationPathSegments sequence. The sequence is left
--     where it is on purpose: path segments are never reused.
--
-- REMOVES
--   * Every Test, Lead, Sale, custom order and customer.
--   * Every organisation below DGI, deactivated ones included.
--   * Every user not assigned to DGI (a user with no assignment at all included).
--   * Every lens set, its lenses and its assignments.
--
-- Soft-deleted rows are removed too: this is raw SQL, so the application's filters don't apply.
--
-- NOT FOR A LOCAL DEV DATABASE: DevUserSeeder and ExampleLensSets expect the seeded Kenya
-- organisations to exist. Locally, drop the Postgres volume instead.
--
-- It runs in one transaction and prints what is left. Read the output, then COMMIT or ROLLBACK
-- by hand — the script deliberately does not commit for you.

BEGIN;

-- Refuse to run unless exactly one DGI organisation exists and somebody is assigned to it:
-- otherwise the script would delete every user and leave nobody able to sign in.
DO $$
DECLARE
    dgi_count integer;
    keeper_count integer;
BEGIN
    SELECT count(*) INTO dgi_count FROM "OrganisationNodes" WHERE "Level" = 0;
    IF dgi_count <> 1 THEN
        RAISE EXCEPTION 'Expected exactly one DGI organisation (Level = 0), found %.', dgi_count;
    END IF;

    SELECT count(DISTINCT a."UserId") INTO keeper_count
    FROM "UserOrgAssignments" a
    JOIN "OrganisationNodes" o ON o."Id" = a."OrgNodeId"
    WHERE o."Level" = 0;
    IF keeper_count = 0 THEN
        RAISE EXCEPTION 'No user is assigned to DGI. Clearing down would leave nobody able to sign in.';
    END IF;
END $$;

-- 1. Records. No foreign keys join these, so one TRUNCATE takes them all.
TRUNCATE TABLE
    "CustomOrders",
    "SaleCoatings",
    "LeadCoatings",
    "Sales",
    "Leads",
    "Tests",
    "Customers";

-- 2. Lens sets. LensOptionCoatings and LensOptionCoatingPairings cascade from LensOptions;
--    naming them keeps TRUNCATE from needing CASCADE.
TRUNCATE TABLE
    "LensOptionCoatingPairings",
    "LensOptionCoatings",
    "LensOptions",
    "PresetCatalogueAssignments",
    "PresetCatalogues";

-- 3. Users not assigned to DGI. Their roles, claims, logins, tokens and assignments cascade.
DELETE FROM "AspNetUsers" u
WHERE NOT EXISTS (
    SELECT 1
    FROM "UserOrgAssignments" a
    JOIN "OrganisationNodes" o ON o."Id" = a."OrgNodeId"
    WHERE a."UserId" = u."Id" AND o."Level" = 0);

-- 4. What the kept users hold besides DGI. Must go before the organisations (restricting FK).
DELETE FROM "UserOrgAssignments" a
USING "OrganisationNodes" o
WHERE o."Id" = a."OrgNodeId" AND o."Level" <> 0;

-- 5. Every organisation below DGI. The parent link restricts deletes, so detach first rather
--    than depend on the order rows are removed in.
UPDATE "OrganisationNodes" SET "ParentId" = NULL WHERE "Level" <> 0;
DELETE FROM "OrganisationNodes" WHERE "Level" <> 0;

-- OPTIONAL: reference data back to what the migrations seeded.
-- Seeded items have ids starting b0000000-; anything else was added on the Reference Data
-- screen. This removes those additions and any coating exclusion naming one. It does NOT undo
-- edits made to a seeded item (a renamed label, a retired option, an uploaded picture), and it
-- leaves the uploaded picture files in the reference-data-images blob container.
-- Never empty the table outright: migrations do not put seeded rows back.
--
-- DELETE FROM "CoatingExclusions" e
-- USING "ReferenceDataItems" r
-- WHERE r."Id" IN (e."CoatingRefIdA", e."CoatingRefIdB")
--   AND r."Id"::text NOT LIKE 'b0000000-%';
-- DELETE FROM "ReferenceDataItems" WHERE "Id"::text NOT LIKE 'b0000000-%';

-- What is left.
SELECT 'OrganisationNodes' AS "table", count(*) FROM "OrganisationNodes"
UNION ALL SELECT 'AspNetUsers', count(*) FROM "AspNetUsers"
UNION ALL SELECT 'UserOrgAssignments', count(*) FROM "UserOrgAssignments"
UNION ALL SELECT 'AspNetUserRoles', count(*) FROM "AspNetUserRoles"
UNION ALL SELECT 'ReferenceDataItems', count(*) FROM "ReferenceDataItems"
UNION ALL SELECT 'PresetCatalogues', count(*) FROM "PresetCatalogues"
UNION ALL SELECT 'Customers', count(*) FROM "Customers"
UNION ALL SELECT 'Tests', count(*) FROM "Tests"
UNION ALL SELECT 'Leads', count(*) FROM "Leads"
UNION ALL SELECT 'Sales', count(*) FROM "Sales"
UNION ALL SELECT 'CustomOrders', count(*) FROM "CustomOrders";

SELECT u."Email", r."Name" AS "Role"
FROM "AspNetUsers" u
LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
LEFT JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
ORDER BY u."Email";

-- Happy with the above? Run:   COMMIT;
-- Otherwise:                   ROLLBACK;
