# 02 — The Organisations screen shows one tree per part of the scope

**What to build:** An admin with assignments in separate parts of the hierarchy sees each part as
its own tree on the Organisations screen, highest level first (DGI, then Country, …) and alphabetical
within a level. Nested assignments (DGI plus a retail point under it) show only the outer tree, so no
org appears twice.

**Blocked by:** 01

**Status:** resolved

**Model:** Sonnet 5 — a bounded change to one screen's tree-building, driven by the scope paths
ticket 01 provides.

**Seam:** `DotGlasses.Web.Tests` — the rendered Organisations screen.

**Don't run alongside:** 01.

## Acceptance criteria

- [x] The Organisations screen builds one tree per scope path (from the current-user abstraction,
      not the old single-org prefix).
- [x] Trees are ordered by level (DGI first), then by name.
- [x] A user with nested assignments sees one tree, not two.
- [x] Web.Tests cover a user with two countries (two trees, in order) and a DGI-plus-retail-point
      user (one DGI tree).

## Comments

Implemented in `src/DotGlasses.Web/Controllers/OrganisationsController.cs`: `BuildViewModelAsync`
previously picked one root via `nodes.First(n => n.ParentId is null || !byId.ContainsKey(...))`,
silently dropping every other root in the caller's scoped node set. Replaced with `BuildTrees`,
which builds an `OrgNode` tree for *every* such root, ordered by `OrganisationLevel` (DGI=0 first)
then by name (`StringComparer.OrdinalIgnoreCase`). No change was needed to how the node set itself
is scoped — `IOrganisationAdminService.ListAsync` already runs through the global hierarchy query
filter, which ticket 01 moved onto `ICurrentUserContext.ScopePaths` — so this ticket is purely the
screen's tree-building, as scoped.

The nested-assignment case (DGI + a retail point beneath it → one tree, not two) needed no explicit
dedup here: `ScopePaths` is already `HierarchyPath.Outermost`-collapsed before `ListAsync`'s query
runs, so the retail point's row is simply never a "no visible parent" candidate. Confirmed by a new
test rather than assumed.

`OrganisationsIndexViewModel.Tree` (single `OrgNode`) became `Trees` (`IReadOnlyList<OrgNode>`);
`Index.cshtml` loops over them with the existing `_OrgTreeNode` partial, one call per tree, so the
partial itself is unchanged. `FindNode` gained an overload that searches across all trees for the
selected-node lookup; the default selection when no `selectedId` matches is `trees[0]` (a scope is
never empty per the spec's "a user always keeps at least one assignment").

New test file `tests/DotGlasses.Web.Tests/AccessControl/OrganisationsScreenTreesTests.cs`, reusing
`AccessControlFixture`'s existing `TwoCountriesAdmin` (Kenya + Uganda, separate trees) and
`DgiAndOutletAdmin` (DGI + a retail point beneath it, nested) accounts rather than seeding new
ones:
- `AnAdminAssignedToTwoCountries_SeesBothCountriesAsSeparateTreesInLevelThenNameOrder` — asserts
  both countries' own subtrees render (a node found only under each), each root appears exactly
  once (`?selectedId=<id>` link count), and Kenya's tree precedes Uganda's in the HTML (alphabetical
  at the same level). Also asserts DGI never appears, since it sits outside both assignments' scope.
- `AnAdminAssignedToDgiAndARetailPointBeneathIt_SeesOneDgiTreeNotTwo` — asserts DGI's tree-row link
  and the nested retail point's tree-row link each appear exactly once. (An earlier draft counted
  raw substring occurrences of the org names instead of tree-row links, which false-failed: the
  selected node's name legitimately repeats in the detail panel and modal titles outside the tree —
  switched to counting `?selectedId=<id>` links, which only `_OrgTreeNode` emits, one per node.)

Full suite: `dotnet test DotGlasses.sln` — 433 passed (225 Rules.Tests, 96 Application.Tests, 50
Infrastructure.Tests, 62 Web.Tests including the 2 new tests), 0 failed.

No deviations from the ticket. No changes made outside the Organisations screen/controller/view
and its own test file.

## Notes

- Spec: `../spec.md` — user stories 5–6, "Admin Portal screens". Ancestor names still resolve via
  `IUnscopedReportQueryService` (CLAUDE.md pitfall).
- Prior art: `AccessControlPolicyTests` (screen rendering after sign-in).
- Skills: `/implement` (with `/tdd`), then `/code-review`.
