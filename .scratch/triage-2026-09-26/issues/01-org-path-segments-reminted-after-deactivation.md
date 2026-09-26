# 01 — Org path segments are re-minted after deactivation, creating duplicate HierarchyPaths

Status: ready-for-agent
Blocked by: None to build and merge. **Deploying it to an environment that already holds duplicates
is blocked by 02** (the new unique index cannot be created over duplicate rows).
Category: bug

Source: real testing, 2026-09-26, Joe. The Dashboard threw
`System.ArgumentException: An item with the same key has already been added. Key: /1/2/` at
`OrgTreeLookup..ctor` (`OrgTreeLookup.cs:47`), called from `DashboardQueryService.GetAsync`.

## Agent Brief

**Category:** bug

**Summary:** `OrganisationAdminService.CreateChildAsync` mints a new path segment as
(max segment over **active** nodes) + 1. A deactivated node keeps its `HierarchyPath` and can be
reactivated, so its segments are never free again, yet the minter ignores them. Deactivating the
node(s) that hold the highest segment and then creating a new org hands out a segment that is
still in use. Reactivating the old node, or a second concurrent create, then leaves two active
nodes with the same path. Nothing in the database stops this: `HierarchyPath` has a non-unique
index.

**Root cause (traced):**
- `OrganisationAdminService.CreateChildAsync`
  (`src/DotGlasses.Infrastructure/Persistence/OrganisationAdminService.cs:60-65`) takes the max
  segment from `IUnscopedReportQueryService.GetOrganisationNodePathsUnscopedAsync`.
- That query deliberately re-applies `!x.IsDeleted`
  (`src/DotGlasses.Infrastructure/Persistence/UnscopedReportQueryService.cs:11-16`). This is correct
  for its other caller (`PresetCatalogueQueryService`) and wrong for minting.
- `OrganisationAdminService.SetActiveAsync` (same file, ~line 97) soft-deletes on deactivate and
  can reactivate later, so a deactivated node's path is only dormant, never released.
- `OrganisationNodeConfiguration` (`.../Configurations/OrganisationNodeConfiguration.cs:20`):
  `builder.HasIndex(x => x.HierarchyPath)` is not unique.
- The read-max-then-increment race recorded in `docs/open-issues.md` ("path-segment minting") is
  a second route to the same corruption: a double-clicked "Create" submits twice.

For the exact `/1/2/` seen, the seeded Kenya chain (`/1/2/`, `/1/2/3/`, `/1/2/3/4/` in
`OrganisationSeedConfiguration`) must have been fully deactivated, so the active max was 1. A new
Country was then created and got `/1/2/`, and the old Kenya was later reactivated (or the create
was double-submitted). The collisions compound: the next child created under the new `/1/2/` gets
`/1/2/3/`, which collides with the seeded Retailer.

**Why it matters beyond the 500:** hierarchy scoping is `HierarchyPath.StartsWith(prefix)`. Two
orgs sharing a path means a user assigned to either one sees the other's Tests/Leads/Sales/
Customers, and rows stamped with the shared path can no longer be attributed to one org.

**Desired behavior:**
1. **A segment, once minted, is never minted again**, whether its node is active, deactivated,
   or the create lost a race. Mint segments from a Postgres sequence (EF
   `modelBuilder.HasSequence<int>(...)`, read with `nextval`) rather than read-max-then-increment.
   The migration must start the sequence above the current max segment across **all** nodes,
   deleted ones included, using `setval` computed in SQL at migration time, not hard-coded.
   Gaps in the numbering are fine: segments are identifiers, not counts.
2. **The database refuses a duplicate path.** Make the `HierarchyPath` index unique, covering every
   row with no filter on `IsDeleted`, since a deactivated node can be reactivated. This is a
   backstop, not the mechanism.
3. **`OrgTreeLookup` names the problem when it does find one.** Replace the bare `ToDictionary`
   `ArgumentException` with an exception whose message lists each duplicated path and the node
   Ids sharing it, so the next occurrence can be diagnosed from the log line alone. It should still
   throw: the class doc says duplicates are "data corruption worth surfacing, not working around".
4. Update `docs/open-issues.md`: remove the "path-segment minting race" entry (the sequence
   resolves it), and update the comment above the minting code in `CreateChildAsync`.

**Key interfaces:**
- `OrganisationAdminService.CreateChildAsync` — `src/DotGlasses.Infrastructure/Persistence/OrganisationAdminService.cs:44-83`
- `OrganisationNodeConfiguration` — `src/DotGlasses.Infrastructure/Persistence/Configurations/OrganisationNodeConfiguration.cs`
- `OrgTreeLookup` constructor — `src/DotGlasses.Application/Reporting/OrgTreeLookup.cs:43-51`
- Leave `IUnscopedReportQueryService.GetOrganisationNodePathsUnscopedAsync` unchanged.
  `PresetCatalogueQueryService` also uses it and correctly wants active nodes only.

**Acceptance criteria:**
- [ ] Infrastructure test (real Postgres, per CLAUDE.md Testing): create a child, deactivate it,
      create another child under the same parent. The two paths differ. Reactivate the first:
      both are active with distinct paths.
- [ ] Infrastructure test: deactivate every node holding the current highest segment, then
      create. The new segment is still above every existing segment, deleted ones included.
- [ ] Infrastructure test: inserting a second `OrganisationNode` with an existing `HierarchyPath`
      fails at `SaveChanges` (unique index), including when the existing row is soft-deleted.
- [ ] The migration's sequence start is derived from existing data. A migration test (or the
      existing harness applying real migrations) shows the first minted segment after migrating a
      DB holding the seeded tree is > 4.
- [ ] `Application.Tests`: `OrgTreeLookup` fed two nodes with the same path throws, and the
      message contains the path and both Ids.
- [ ] `docs/open-issues.md` updated as above.

**Out of scope:**
- Repairing data already corrupted in a deployed environment (ticket 02).
- Changing `HierarchyPath` persistence from `string` (ADR-0004: leave it `string`).
- Any change to how deactivation or reactivation works.

## Comments

> *This was generated by AI during triage.*
