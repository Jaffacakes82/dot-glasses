# CLAUDE.md

Behavioural contract for Claude Code working in this repo. Keep this concise — durable
decisions only, not documentation (that's `README.md`).

## Architecture rules

- Clean Architecture, dependency direction: `Web`/`App` → `Application`/`Contracts`;
  `Application` → `Domain`; `Infrastructure` → `Domain`/`Application` (implements its
  interfaces). Nothing references `Infrastructure` except `Web`'s `Program.cs` (composition
  root) and `AppHost` (orchestration).
- **`DotGlasses.App` may only ever reference `DotGlasses.Contracts`.** If a change seems to
  need `App` to reference anything else, that's a signal the type belongs in `Contracts`
  instead — flag it, don't add the reference.
- No MediatR. Plain application services with interfaces in `Application`, implementations
  alongside.
- Controller-based Web API (not Minimal APIs), versioned from `v1`, Swagger-visible.

## Data scoping vs RBAC — do not conflate

- **Data scoping** (which rows a user can see) is a global EF Core query filter on
  `IHierarchyScoped` entities, keyed off `ICurrentUserContext.HierarchyPathPrefix`. It is
  role-independent.
- **RBAC** (what a user can do with rows they can see) is separate, policy-based
  `IAuthorizationHandler`/`[Authorize(Policy = ...)]` — role-dependent, never touches the
  query filter.
- The only sanctioned way to look outside a caller's hierarchy scope is the explicit
  `IUnscopedReportQueryService` — no ad hoc `.IgnoreQueryFilters()` elsewhere.

## Offline sync (Field App)

New `App` features that write data go through the outbox pattern (IndexedDB pending-sync
table, client-generated GUID as idempotency key, `ISyncService` draining on reconnect) — never
call the API directly from a Blazor page/component.

## Domain modelling

Real domain entities are now designed and persisted (2026-08-04, following the CEO conversation
+ `design/design_handoff_dot_glasses_platform/README.md` + same-day follow-up decisions on Custom
Orders scope, preset catalogue assignment, Manager RBAC scope, and Kobo-sourced reference data):
org hierarchy (`OrganisationNode` — arbitrary-depth, only `Dgi`/`Country`/`Intermediate`/
`RetailPoint` carry business rules), `Test`/`Lead`/`Sale` as separate atomic events, admin-
configurable `PresetCatalogue`/`LensOption` (lens range is picked per-transaction by the
technician now, not locked at the org level — there is no "Classical Optician" preset), a generic
`ReferenceDataItem` table backing every admin-managed dropdown (seeded from the Kobo `choices`
export — see `Persistence/Configurations/*SeedConfiguration.cs`), and a lightweight `Customer`
entity for name+phone matching. Full shape: `DotGlasses.Domain/Entities` and `/Enums`.

Two assumptions made while seeding, not explicitly discussed on the call — confirm once the
Reference Data / Catalogues admin screens are wired for real: (1) `FrameColour` has an "Other"
fallback row for consistency with every other reference list, even though the call named exactly
6 fixed colours; (2) every non-bifocal seeded `LensOption` defaults to the "Clear" coating (the
call only specified bifocals' forced Photochromic).

RBAC now has a real permission matrix backing it (`OrgLevelRequirement`/
`HierarchyDescendantRequirement` in `Web/Authorization`, see the RBAC section below) and all
seven Admin Portal controllers have `[Authorize]`.

`WidgetExample` remains the architectural reference pattern (audit/soft-delete/hierarchy-scoping/
offline-sync skeleton) and isn't deleted — real entities exist alongside it. **Application/
Contracts/API/UI wiring for the new entities is a deliberately deferred next phase** — no screen
consumes them yet (Admin Portal and `ConsultationForm.razor` are still static placeholder data,
see UI / design system below); wire it up screen by screen as each one gets real data, checking
field-level shape against the design README first, rather than as one big pass.
`HierarchyDescendantRequirement` is shipped but not yet wired to a controller action for the same
reason — no screen operates on a real per-user/per-org resource yet.

## Test/Lead/Sale API

**[IN PROGRESS 2026-08-04]** Building the Application/Contracts/API vertical slice for Test/Lead/
Sale (the first slice of the "deferred next phase" above), following `WidgetExampleService`/
`WidgetExampleRepository`/`WidgetExamplesController` as the structural template but with three
deliberate departures — see plan `breezy-conjuring-galaxy.md` for full rationale:
1. Create requests never accept `HierarchyPath`/`TechnicianUserId` from the client — the Web
   controller stamps both from `ICurrentUserContext` instead of trusting the request body.
2. No public Update endpoint — Test/Lead/Sale are create-once atomic events; server-side linking
   (Test→Lead, Lead→Sale) happens inside the service layer, not via a PUT contract.
3. New `IUnitOfWork` (`DotGlasses.Application/Common`, `DotGlassesDbContext` satisfies it
   directly) lets a service batch multiple repository writes into one transaction — needed because
   converting a Test into a Lead must set `Test.ConvertedToLeadId` atomically with creating the
   Lead. `WidgetExampleRepository` is untouched; this is additive, not a retrofit.

Status:
- ✅ Checkpoint 1 — shared plumbing: `IUnitOfWork`, `IReferenceDataLookupService`
  (`DotGlasses.Application/ReferenceData`, backs category-correctness + "Other"-text-required
  validation without validators touching Infrastructure), `ICustomerRepository`
  (`DotGlasses.Application/Customers`, exact-match find-or-create only — no public API; fuzzy
  matching is Field App UI work for later). Solution builds, `dotnet test` passes (18 tests).
- ⬜ Checkpoint 2 — Test vertical slice.
- ⬜ Checkpoint 3 — Lead vertical slice (Test-linking, Customer find-or-create).
- ⬜ Checkpoint 4 — Sale vertical slice (Lead-linking, derived coating for preset ranges).
- ⬜ Checkpoint 5 — wrap-up: replace this block with a permanent write-up, retire the
  Application/Contracts/API portion of the `[OPEN]` item below.

No new xUnit tests are planned for this pass (flagged as a gap, not silently skipped) —
verification is `dotnet build`/`dotnet test` per checkpoint plus a manual end-to-end smoke test
against the real running stack once all three entities are wired.

## RBAC permission matrix

Three roles (Admin/Manager/User), assignable at any org node, scope = that node + everything
beneath it:
- **Admin at DGI**: super admin — the only role/level that can edit reference data
  (`AuthorizationPolicies.ReferenceDataManage`) or touch DGI-critical settings.
- **Admin at a child org**: full control of that org and everything beneath it.
- **Manager**: can manage *any* user at/below their node, **including other Admins**, irrespective
  of that user's role (2026-08-04 decision — deliberately not role-gated the other way). Can
  create child orgs beneath their scope. Can create/assign preset catalogues at Country level and
  above (`AuthorizationPolicies.PresetCatalogueManage`).
- **User**: at a Retail Point, Field App access + read-only MI for that outlet only.
- **Custom Orders** page (`AuthorizationPolicies.CustomOrdersView`): DGI/Country only, hidden
  entirely below that (2026-08-04 decision).
- Event History is visible at every level, but scoped to the viewer's role + org — not yet
  enforced (still placeholder data, see UI / design system below).

Backed by `OrgLevelRequirement` (role + own org level at/above a threshold — no DB round trip,
reads `ICurrentUserContext.OrgLevel`, itself denormalized onto `ApplicationUser.OrgLevel` and
stamped as a claim at sign-in, same pattern as `HierarchyPath`) and
`HierarchyDescendantRequirement` (role + resource-based subtree check, for when a controller
acts on a specific target user/org — not yet wired to one, see above).

## UI / design system

- Design tokens (colors, type, spacing) live in `wwwroot/css/dot-glasses.css` in **both**
  `Web` and `App` — hand-ported from `/design/_ds/.../tokens/*.css` (gitignored, reference-only,
  never linked from either app). There's no shared static-asset project between a
  server-rendered MVC app and a WASM app to source one file from, so the two copies must be
  kept in sync **by hand**. If the token values ever change, update both.
- Bootstrap is still present in both projects (grid utilities, form controls, the native modal
  JS in `UserDirectory`) — the design system layers custom `dg-*` classes/tokens on top rather
  than replacing it.
- Admin Portal (`Web`) screens are skeletons over static placeholder data (in each
  `Controllers/*Controller.cs`, not a database) — Dashboard, Organisations, Event History, User
  Directory, Preset Catalogues, Custom Orders, Reference Data. `ConsultationForm.razor` in `App`
  (and its `Web` modal equivalent, not yet built) is the same story — a visual skeleton with no
  save wiring. Test/Lead/Sale and everything else are now designed real entities (see Domain
  modelling above) — don't wire these screens to them without checking each screen's field-level
  shape against the design README first (that wiring is the deliberately deferred next phase).
- All seven Admin Portal controllers now have `[Authorize]` (see RBAC permission matrix above) —
  `HomeController`'s `Error` action is `[AllowAnonymous]` so error pages render for logged-out
  users too.

## Deployment (Azure)

- `Web` deploys to Azure Container Apps; Postgres to Azure Database for PostgreSQL Flexible
  Server (Entra ID auth via managed identity, not a connection-string password) — both modelled
  explicitly in `AppHost.cs` (`AddAzureContainerAppEnvironment`, `AddAzurePostgresFlexibleServer`).
  Local dev still runs Postgres as a container via `.RunAsContainer(...)`, using a password
  parameter pinned by name (`postgres-password`, sourced from AppHost's user secrets, never
  hardcoded) — pinning it, rather than leaving Aspire to auto-generate one per resource shape,
  is what stops a future change to how the resource is declared from silently invalidating the
  local Postgres data volume's credentials again.
- **`DotGlasses.App` (the PWA) is a *separate* azd project** (`src/DotGlasses.App/azure.yaml`,
  its own `azd up`), not a service inside the root `azure.yaml`. Two reasons, both structural,
  not stylistic: azd (1.29.0) refuses to mix an Aspire-detected service with any hand-declared
  one in one project, and no Aspire hosting integration for Azure Static Web Apps exists to
  begin with (the only one, `CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps`, is
  local-emulator-only). Don't try to fold this into the AppHost model later without re-checking
  whether that constraint has lifted.
- `/infra` (root) is generated via `azd infra gen` from the AppHost model — treat it as
  regenerable output, not hand-maintained source; re-run `azd infra gen --force` after AppHost
  resource changes rather than hand-editing the Bicep.
- **No infra is ever deployed from a developer machine** — only via GitHub Actions. `[OPEN]`:
  `azd pipeline config` hasn't been run yet (needs to run once per azd project — twice, since
  root and `DotGlasses.App` are separate projects — and needs the user's own Azure login, so
  Claude shouldn't run it). Deliberately deferred; don't scaffold `azure-dev.yml`-style workflow
  files ahead of that until asked again.

## Repo/public-repo constraints

- This repo is public. `/design` (Claude Design handoff bundle) and local Claude Code settings
  are gitignored and must not be committed or referenced from `README.md`.

## Testing

xUnit. EF Core InMemory provider is acceptable for now (`Infrastructure.Tests`).
`WebApplicationFactory` for `Web.Tests` integration tests.

## Running locally

`dotnet run` on the `DotGlasses.AppHost` project (starts Postgres via container, `Web`, and the
Aspire dashboard).

## `[OPEN]` items — implement simplest placeholder, flag, don't guess

- Real per-user role/claim assignment beyond the three seeded dev accounts (`DevUserSeeder`) is
  still open — no self-service provisioning flow exists yet.
- Application/Contracts/API/UI wiring for the new domain entities (Test/Lead/Sale/OrganisationNode/
  PresetCatalogue/ReferenceDataItem/Customer) — see Domain modelling above. Build screen by screen.
- Offline sync conflict resolution (currently last-write-wins; don't hard-code away a future
  version/ETag column).
- Azure Monitor/Application Insights exporter connection string.
- `azd pipeline config` not run yet — see Deployment section below.
- UI skeleton screens are static placeholder data, not wired to a database — see UI / design
  system section above. In particular the Consultation Form skeleton is missing the lead-match
  confirm popup, the "use test result" carry-over from Test to Sale, and progressive disclosure
  for catalogues with >10 items (short list vs. searchable lookup) — all present in the design
  mockups but not built.
- Field App's `wwwroot/appsettings.json` `ApiBaseUrl` still points at `Web`'s local dev HTTPS
  port — needs updating to the real deployed API origin once that exists.
- Two reference-data seeding assumptions not explicitly discussed on the call — see Domain
  modelling above (FrameColour's "Other" row, non-bifocal LensOptions defaulting to "Clear").

This file should grow as real architectural decisions get made — propose updates here when a
significant decision is agreed, not as a one-time artifact.
