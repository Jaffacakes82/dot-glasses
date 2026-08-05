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
- **`Contracts` must not reference `Domain` or `Application`** — it's a pure wire-shape layer, not
  because of a project reference someone forgot to add but because `App` referencing `Contracts`
  must not transitively pull in `Domain`/`Application`. DTOs that need an enum define their own
  copy in `Contracts` (e.g. `Contracts.Common.Gender` next to `Domain.Enums.Gender`) rather than
  referencing the Domain one; map between them in the Application layer. Validators that need a
  DB-backed check (e.g. "does this Guid reference an active reference-data item") can't be
  co-located with their DTO in `Contracts` for the same reason — they live in
  `DotGlasses.Web.Validation.*` instead, referencing `Application` interfaces directly.
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

**Permanent vs transient sync failures (2026-08-05 fix)**: `SyncService.SyncItemAsync` already
marked a 400/401/403 response `OutboxItemStatus.Failed` (terminal — not retryable without user
action) rather than logging-and-retrying like a transient failure (network error, 5xx). The bug
was one layer down: `idbInterop.js`'s `getPending` only excluded `status !== 'Synced'`, so a
`Failed` item was still returned by `ISyncQueueStore.GetPendingAsync()` and `SyncService` kept
re-POSTing (and re-failing) the same permanently-invalid payload on every later sync cycle —
reproduced live via a Lead saved with a required field left empty (13 retries observed on one
stuck item before the fix). Fixed by also excluding `Failed` in `getPending`'s filter, and adding
`ISyncQueueStore.GetFailedAsync()`/`dotGlassesIdb.getFailed` (JS) as the sanctioned way to query
the terminal set separately — `SyncService` never sees `Failed` items again once marked, but
`Home.razor` calls `GetFailedAsync()` to show a distinct "N record(s) couldn't sync — needs
review" banner (entity type + error, no destructive retry/discard action — out of scope, see
`[OPEN]`) so a technician isn't left thinking a permanently-broken record is just "waiting for
signal." `WidgetExamples.razor`'s demo table merges `GetPendingAsync()` + `GetFailedAsync()` to
keep showing every outbox status in its walkthrough, since `GetPendingAsync()` alone no longer
does.

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

Full Application/Contracts/API vertical slices exist for Test, Lead, and Sale (2026-08-04),
following `WidgetExampleService`/`WidgetExampleRepository`/`WidgetExamplesController` as the
structural template but with three deliberate departures:
1. Create requests never accept `HierarchyPath`/`TechnicianUserId` from the client — the Web
   controller stamps both from `ICurrentUserContext` instead of trusting the request body.
2. No public Update endpoint — Test/Lead/Sale are create-once atomic events; server-side linking
   (Test→Lead, Lead→Sale) happens inside the service layer, not via a PUT contract.
3. `IUnitOfWork` (`DotGlasses.Application/Common`, `DotGlassesDbContext` satisfies it directly)
   lets a service batch multiple repository writes into one transaction — needed because
   converting a Test into a Lead must set `Test.ConvertedToLeadId` atomically with creating the
   Lead, and converting a Lead into a Sale must set `Lead.ConvertedFlag`/`SaleId` atomically with
   creating the Sale. `WidgetExampleRepository` is untouched — this is additive, not a retrofit.

Shared plumbing: `IReferenceDataLookupService` (`DotGlasses.Application/ReferenceData` — category
correctness + "Other"-text-required checks, plus `LensOptionBelongsToCatalogueAsync`/
`GetLensOptionCoatingIdAsync` for preset-range consistency) and `ICustomerRepository`
(`DotGlasses.Application/Customers` — exact name+phone find-or-create only, no public API; fuzzy/
suggested-match UX is Field App UI work for later). For a preset `LensRangeType`, `SaleService`
derives `Sale.CoatingRefId` from the chosen left-eye `LensOption`'s own forced coating and ignores
any client-submitted value — only a `Custom` range actually uses the client's `CoatingRefId`
(known simplification if left/right eyes resolve to different coatings — `Sale` has one
`CoatingRefId` column, not per-eye).

The `Test` Application-layer types are named `IVisionTestRepository`/`IVisionTestService`/
`VisionTestService` (not `ITestRepository` etc.) — `ITestRepository` would collide with the
`DotGlasses.Application.Tests` xUnit project's own root namespace. The Domain entity itself is
still `Test`.

No new xUnit tests were added for this pass (a gap, not silently skipped) — verified instead via
`dotnet build`/`dotnet test` per checkpoint plus a full manual end-to-end run against the real
stack: Test → Lead (`SourceTestId`) → Sale (`SourceLeadId`, preset range), confirming
`ConvertedToLeadId`/`ConvertedFlag`/`SaleId` link atomically, the derived coating is correct, and
a second Lead-from-the-same-Test attempt is rejected (400, "already been converted").

**Two bugs found and fixed via that live run, not caught by any existing test:**
- `AddFluentValidationAutoValidation()` (`Program.cs`) ran FluentValidation synchronously as part
  of ASP.NET's model-binding pipeline, which can't invoke the async rules Test/Lead/Sale's
  validators need for DB-backed checks (`AsyncValidatorInvokedSynchronouslyException` on every
  `POST`). Every controller (`AuthController`, `WidgetExamplesController`, and all of
  Test/Lead/Sale's) already called `ValidateAsync` explicitly, so auto-validation was fully
  redundant even before this — removed outright, not worked around.
- `AuditSaveChangesInterceptor` was never actually running through the real HTTP pipeline —
  `CreatedAtUtc`/`CreatedBy` came back unset on every entity, including `WidgetExample`. It was
  registered as `IInterceptor` in DI expecting EF Core's auto-discovery to wire it into
  `DotGlassesDbContext` (resolved via Aspire's pooled `AddNpgsqlDbContext`), but that
  auto-discovery silently doesn't fire for a pooled context. `AuditSaveChangesInterceptorTests`
  never caught this because it constructs the interceptor directly via `AddInterceptors(...)`,
  bypassing DI entirely. **Fixed in `Program.cs`**, not `DotGlassesDbContext.OnConfiguring` —
  EF Core throws at startup ("`'OnConfiguring' cannot be used to modify DbContextOptions when
  DbContext pooling is enabled`") for any attempt to override `OnConfiguring` on a pooled
  context. The correct place is `AddNpgsqlDbContext`'s `configureDbContextOptions` callback,
  building the interceptor from a fresh `HttpContextAccessor()` (safe to construct standalone —
  its `.HttpContext` is backed by a static `AsyncLocal`, not instance state, so one built once at
  startup stays correct for every future request; same reasoning the query filter already uses).
  See `Program.cs`'s persistence section for the full comment.

## Field App UI wiring (ConsultationForm.razor)

`ConsultationForm.razor` (Field App) is wired to the real Test/Lead/Sale API (2026-08-04) —
Admin Portal's equivalent modal is explicitly out of scope (doesn't exist yet, separate larger
task). Also explicitly deferred: the lead-match confirm popup, the "use test result" Test→Sale
carry-over, and progressive disclosure for >10-item catalogues (moot today — both seeded
catalogues have ≤12 `LensOption`s). Full offline caching of reference data is also deferred (see
below).

**New read-only API** (a prerequisite this surfaced, not UI work): `ReferenceDataItemDto`/
`PresetCatalogueDto`/`LensOptionDto` (Contracts, + `Contracts.Common.ReferenceDataCategory`
mirroring `Domain.Enums.ReferenceDataCategory`), `IReferenceDataQueryService`/
`IPresetCatalogueQueryService` (Application), `ReferenceDataController`/
`PresetCataloguesController` (Web, `GET`-only, any authenticated role — distinct from the MVC
`CataloguesController` Admin Portal placeholder).

**App-side**: `IReferenceDataClient` (`DotGlasses.App/ReferenceData`, singleton, fetch-once-per-
session — no IndexedDB caching yet, degrades with a retry message if unreachable, same pattern
`WidgetExamples.razor` uses for its own round trip), and two shared components:
`LensRangeSelector.razor` (the SixLensSet/NineLensSet/Custom picker, used by both Lead and Sale)
and `ReferenceDataDropdown.razor` (a reference-data `<select>` + conditional "Other" free-text
field, used 5×). All three `CreateXRequest`s build for real and enqueue via
`OutboxStore.EnqueueAsync` + `SyncService.SyncPendingAsync()`, exactly `WidgetExamples.razor`'s
pattern. "Continue as Lead" saves the Test first, then navigates to `consultation/lead?
sourceTestId=...&prefillAge=...&prefillGender=...` — age/gender pre-populate on the Lead form.
`SourceLeadId` is never set on a Sale in this pass — no Leads list/"convert to sale" entry point
exists yet for a technician to pick one from; a Sale recorded here is always a fresh walk-in.

**Known rough edge, flagged for later**: `LensRangeType.SixLensSet`/`NineLensSet` aren't
otherwise tied to a specific `PresetCatalogueId` in the domain model — `LensRangeSelector`
matches by catalogue **name** ("6-Lens Set"/"9-Lens Set") since only those two catalogues exist
today; if DGI/Country admins ever create additional named catalogues this needs an explicit
"kind" field on `PresetCatalogue` instead of name-sniffing.

**Verified live end-to-end** (real browser against a real running stack, not just build+test):
signed in as the seeded RetailPoint dev user, recorded a Test with `Outcome: NeedsGlasses`,
clicked "Continue as Lead" (confirmed the Test's `ConvertedToLeadId` linked + age/gender
pre-filled), saved a Lead with a preset 6-Lens Set range, then separately saved a standalone Sale
choosing a **bifocal** left-eye lens paired with a **non-bifocal** right-eye lens — confirmed
`CoatingRefId` landed as "Photochromic" (derived from the left eye, per the documented
known-simplification), `HierarchyPath`/`TechnicianUserId` on every record matched the signed-in
user's own JWT claims (never anything a client could have sent — there's no such field on any
Create form), and the Customer record was correctly reused (not duplicated) across the Lead and
Sale sharing the same name+phone.

**One real bug found and fixed via that live run**: `PresetCatalogueQueryService`'s join against
`OrganisationNodes` was silently caught by the global hierarchy-scoping query filter — a
RetailPoint-level caller's own query only ever sees their own subtree, so the Country-level
ancestor a catalogue was actually assigned to got filtered out, and every preset appeared "not
available" for retail-point users specifically (DGI-level testing never would have caught this,
since DGI's own subtree contains everything). Fixed by resolving org paths via
`IUnscopedReportQueryService` (extended with `GetOrganisationNodePathsUnscopedAsync`) instead of
querying `OrganisationNodes` directly — the sanctioned way to look outside a caller's hierarchy
scope, per the Architecture rules above; this is exactly the kind of ad hoc scoping bug that rule
exists to prevent, and it still slipped through because the *query itself* wasn't obviously
"looking outside scope" until traced through.

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
- Admin Portal (`Web`) screens are still skeletons over static placeholder data (in each
  `Controllers/*Controller.cs`, not a database) — Dashboard, Organisations, Event History, User
  Directory, Preset Catalogues, Custom Orders, Reference Data. Don't wire these up without
  checking each screen's field-level shape against the design README first (that wiring is the
  deliberately deferred next phase — see Domain modelling above). `ConsultationForm.razor` in
  `App` is **no longer a stub** — it saves real Test/Lead/Sale records via the real API (see
  Field App UI wiring above); its `Web` modal equivalent still doesn't exist.
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
- The Field App's `ConsultationForm.razor` is wired to the real API (see Field App UI wiring
  above); the Admin Portal's equivalent modal still doesn't exist. OrganisationNode/
  PresetCatalogue/ReferenceDataItem have no *write* API yet (reference data is DGI-editable per
  the RBAC matrix, but there's nowhere to actually edit it — the Admin Portal's Reference Data/
  Catalogues screens are still placeholder); Customer is internal-only by design. Build the
  Admin Portal screens next, screen by screen, checking field-level shape against the design
  README first.
- No Leads-list/"convert to sale" entry point exists yet — a Sale recorded via
  `ConsultationForm.razor` can never set `SourceLeadId`, so a Lead's `ConvertedFlag`/`SaleId`
  currently only ever get set via the Test→Lead→(same-session)→Sale path, never by converting an
  existing Lead found later. Needs an Event History/Leads screen action once one exists.
- Full offline (IndexedDB) caching of reference data/preset catalogues — `IReferenceDataClient`
  currently needs connectivity to load the first time each session; a technician who's never
  been online since app install can't record anything yet.
- Domain-shape question, not resolved, don't guess: the CEO transcript separately mentions "0 to
  4 PD (0 to 2 for children)" for *preset*-range pupil distance, which reads as a UI shorthand/
  frame-fit bucket rather than literal millimetres, distinct from the real 54–74mm
  inter-pupillary-distance field used for Custom range. `LensRangeSelector` currently only
  exposes the real mm field, for both range types — ask before inventing a second taxonomy.
  See `Lead`/`Sale.PupilDistanceMm`.
- `LensRangeType.SixLensSet`/`NineLensSet` matching a specific `PresetCatalogueId` by catalogue
  **name** (see Field App UI wiring above) — fine while only two catalogues exist, needs an
  explicit "kind" field on `PresetCatalogue` if DGI/Country ever create more.
- Offline sync conflict resolution (currently last-write-wins; don't hard-code away a future
  version/ETag column).
- A permanently-failed outbox item (see Offline sync above) has no in-app retry-after-edit or
  discard action yet — the technician sees it flagged on the home screen but can't currently fix
  the bad field and resubmit, or dismiss it, from the Field App itself.
- Azure Monitor/Application Insights exporter connection string.
- `azd pipeline config` not run yet — see Deployment section below.
- UI skeleton screens are static placeholder data, not wired to a database — see UI / design
  system section above. The Consultation Form is still missing the lead-match confirm popup, the
  "use test result" carry-over from Test to Sale, and progressive disclosure for catalogues with
  >10 items — all present in the design mockups but not built (deliberately deferred, not an
  oversight — see Field App UI wiring above).
- Field App's `wwwroot/appsettings.json` `ApiBaseUrl` still points at `Web`'s local dev HTTPS
  port — needs updating to the real deployed API origin once that exists.
- Two reference-data seeding assumptions not explicitly discussed on the call — see Domain
  modelling above (FrameColour's "Other" row, non-bifocal LensOptions defaulting to "Clear").

This file should grow as real architectural decisions get made — propose updates here when a
significant decision is agreed, not as a one-time artifact.
