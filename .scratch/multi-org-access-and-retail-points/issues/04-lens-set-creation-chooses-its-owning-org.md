# 04 — Creating a lens set means choosing its owning org

**What to build:** An admin with several DGI- or Country-level assignments chooses which of them owns
a new lens set. With only one qualifying assignment it is chosen automatically and no field is shown.
The server refuses an owning org that isn't one of the admin's DGI or Country assignments. The rest of
the Lens Sets screen works on the combined scope from ticket 01.

**Blocked by:** 01

**Status:** resolved

**Model:** Sonnet 5 — a bounded form field and server check; the scope-aware permission requirements
already exist after ticket 01.

**Seam:** `DotGlasses.Web.Tests` — the Lens Sets screen's create POST (POST-redirect-GET).

**Don't run alongside:** 01. Spec B's lens-set tickets (B03, B07, B08) build on this one.

## Acceptance criteria

- [x] Lens set creation takes an explicit owning org, offered from the admin's assignments at DGI or
      Country level; the field shows only when more than one qualifies.
- [x] The server checks the chosen org is one of those assignments; otherwise the request is refused.
- [x] Lens set creation no longer reads the old single active org.
- [x] Web.Tests cover: create with an explicit owning org; a choice outside the admin's DGI/Country
      assignments is refused; a single qualifying assignment is used automatically.

## Notes

- Spec: `../spec.md` — user stories 20–21, "Admin Portal screens".
- Prior art: `RetireLensSetScreenTests`, `CataloguesScreenWordingTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

Implemented on `feat/multi-org-access-04-owning-org`.

- Added `IUserOrgAssignmentService.ListDgiOrCountryAssignmentsAsync(userId)` (Application/Users),
  implemented in `UserOrgAssignmentService` via `IUnscopedReportQueryService` — the "small,
  well-placed query" for the admin's own Dgi/Country assignments, id+name, ordered Dgi first then
  by name. Used both to build the create-lens-set picker and to validate a posted choice.
- `CreateCatalogueRequest` gained an optional `OwningOrgNodeId`. `CreateCatalogueRequestValidator`
  refuses a choice outside the caller's Dgi/Country assignments, and refuses a blank choice unless
  exactly one assignment qualifies. `CataloguesController.CreateCatalogue` no longer reads
  `ICurrentUserContext.OrgNodeId`; it resolves the owning org from the validated choice or, when
  none was posted, the caller's single qualifying assignment.
  `CataloguesController.BuildViewModelAsync` offers the same options to the view; `Index.cshtml`
  shows the "Owning org" select only when more than one qualifies.
- `PresetCatalogueAdminService.CreateAsync`'s own Dgi/Country-level check on the owning org is kept
  as defence in depth (its doc comment updated), but can no longer be reached over HTTP now that
  the controller only ever passes a validated Dgi/Country org. The existing web-level test that
  exercised it via the old single-active-org mismatch
  (`DomainRuleViolationScreenTests.Catalogues_CreatingACatalogueOwnedByARetailPoint_...`) was
  removed as obsolete and replaced with a direct service-seam test,
  `LensSetAvailabilityTests.CreatingALensSetOwnedByARetailPoint_IsRefused` (Infrastructure.Tests).
- Added `AdminPortalFactory.CreateAdminClientWithAssignments(params Guid[])` test helper for
  scenarios needing more than one org assignment, alongside the existing single-assignment
  `CreateAdminClient`.
- New tests: `tests/DotGlasses.Web.Tests/Catalogues/CreateLensSetOwningOrgTests.cs` — explicit
  choice used when offered; a choice outside the admin's Dgi/Country assignments refused (also
  asserts no catalogue was created); single qualifying assignment used automatically with the
  field hidden.
- Full suite green at completion: 434 tests (Rules.Tests 225, Application.Tests 96,
  Infrastructure.Tests 51, Web.Tests 62), 0 failures.
- No deviations from the ticket. Deferred to later tickets, per the ticket's own scope: anything
  about the User Directory/invite forms, the Organisations screen, sidebar, Field App API, and the
  wider lens redesign (ADR-0007).
