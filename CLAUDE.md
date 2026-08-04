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

**[IN PROGRESS 2026-08-04]** Real domain entities are now settled (CEO follow-up conversation +
`design/design_handoff_dot_glasses_platform/README.md` + same-day decisions on Custom Orders
scope, preset catalogue assignment, Manager RBAC scope, and Kobo-sourced reference data — see
plan `breezy-conjuring-galaxy.md` for full rationale) and are being built in checkpoints so
progress survives a session running out mid-task. Status:
- ✅ Checkpoint 1 — `DotGlasses.Domain/Entities` (`OrganisationNode`, `UserOrgAssignment`,
  `ReferenceDataItem`, `PresetCatalogue`, `PresetCatalogueAssignment`, `LensOption`, `Customer`,
  `Test`, `Lead`, `Sale`) and `DotGlasses.Domain/Enums` (`OrganisationLevel`, `Gender`,
  `TestOutcome`, `LensRangeType`, `FrameCoverage`, `ReferenceDataCategory`) — solution builds.
- ✅ Checkpoint 2 — Infrastructure persistence: EF configs in
  `Persistence/Configurations/*Configuration.cs`, `DbSet`s added to `DotGlassesDbContext`,
  migration `20260804175140_AddDomainEntities` (reference-data + org-tree + preset-catalogue seed
  data via `HasData`, sourced from the Kobo `choices` export per today's decisions — notably: no
  "Classical" lens-set option, hard-case colours are Orange/Green/Other not Kobo's Blue/Pink/
  Purple/Black, coating list is Bradley's 5 not Kobo's 7, photophobia/vision-type/multifocal-type
  dropped as legacy-only). Two assumptions made along the way, not explicitly discussed on the
  call — flag for confirmation once the admin Reference Data / Catalogues screens are wired for
  real: (1) FrameColour got an "Other" fallback row for consistency with every other reference
  list, even though the call named exactly 6 fixed colours; (2) every non-bifocal seeded
  `LensOption` defaults to the "Clear" coating (the call only specified bifocals' forced
  Photochromic coating). Solution builds, `dotnet test` passes (18 tests). Migration not yet
  applied to a running database — see Verification in the plan file for how to do that.
- ✅ Checkpoint 3 — RBAC: `OrgLevelRequirement`/`HierarchyDescendantRequirement` handlers + named
  policies in `AuthorizationPolicies`, wired in `Program.cs`. `ICurrentUserContext` gained
  `OrgLevel` (denormalized onto `ApplicationUser.OrgLevel`, stamped as an `OrgLevel` claim at
  sign-in, same no-DB-round-trip pattern as `HierarchyPath`) so the new handlers depend only on
  `ICurrentUserContext` (Application), never `DotGlassesDbContext` directly — keeps Web's
  Authorization folder inside the Clean Architecture boundary. `[Authorize]` now on all seven
  Admin Portal controllers: `CustomOrdersController`/`ReferenceDataController`/
  `CataloguesController` gated by their new level policies, `HomeController` (with `[AllowAnonymous]`
  kept on its `Error` action)/`OrganisationsController`/`EventHistoryController`/
  `UserDirectoryController` gated by plain `[Authorize]` (real per-row scoping still pending real
  data). `HierarchyDescendantRequirement` is shipped but not yet wired to a controller action —
  no screen operates on a real per-user/per-org resource yet. Solution builds, `dotnet test`
  passes (18 tests, none of which cover the now-gated MVC controllers — no test behaviour change).
- ✅ Checkpoint 4 — `DevUserSeeder` extended: still gated behind `DevSeedOptions` (same as
  before), now seeds three accounts against the seeded org tree — DGI Admin (existing account,
  now also gets `OrgNodeId`/`OrgLevel` set, previously only `HierarchyPath`), a Manager at the
  seeded Kenya `Country` node (`kenya-manager@dotglasses.dev`), and a `User` at the seeded
  RetailPoint (`retailpoint-user@dotglasses.dev`) — fixed dev-only credentials, not configurable
  (see the file for both passwords). Lets `CustomOrdersView` and the other new policies actually
  be exercised end-to-end: DGI Admin and Kenya Manager should reach `/custom-orders`, the
  RetailPoint User should not. Solution builds, `dotnet test` passes (18 tests). Not yet run
  against a live database — that's the final Verification step, after Checkpoint 5.
- ⬜ Checkpoint 5 — remove the now-stale "Classical Optician" placeholder data from
  `CataloguesController`/`OrganisationsController`, replace this whole section with the permanent
  write-up, retire the two `[OPEN]` items below it makes obsolete.

`WidgetExample` remains the architectural reference pattern (audit/soft-delete/hierarchy-scoping/
offline-sync skeleton) and isn't being deleted — real entities exist alongside it, not instead of
it, until the new entities have their own Application/Contracts/API/UI wiring (deliberately out of
scope for this pass — no screen consumes them yet, see the plan file).

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
  save wiring, since Test/Lead/Sale aren't designed entities yet (see Domain modelling above).
  Don't wire these to a real data source without checking the shape against real entities first.
- `HomeController` (Admin Portal Dashboard, the landing route) has **no `[Authorize]`** —
  reachable without logging in. Same for the other new Admin controllers. Deliberately left
  alone pending the RBAC permission matrix decision below; don't silently add authorization
  attributes without checking which policy/role each screen should require.

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

- RBAC permission matrix (roles are Admin/Manager/User; only one example policy is wired). The
  three role names themselves now seed via migration (`RoleSeedConfiguration`, `HasData`), not a
  hosted service — real per-user role/claim assignment is still open.
- **None of the new Admin Portal controllers have `[Authorize]`** (Dashboard/Home, Organisations,
  Event History, User Directory, Catalogues, Custom Orders, Reference Data) — needs gating once
  the RBAC matrix above is decided, since the right policy per screen isn't obvious yet.
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
- Reference Data's "Referral reasons" seed list is a best guess — the real source of truth is
  DOT Glasses' existing Kobo form (not in this repo); ask before finalizing.

This file should grow as real architectural decisions get made — propose updates here when a
significant decision is agreed, not as a one-time artifact.
