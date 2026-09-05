# CLAUDE.md

Behavioural contract for Claude Code working in this repo. Durable architectural rules and
dev/debug guidance only — not a changelog. For what the product currently does, see
[`docs/functional-capabilities.md`](docs/functional-capabilities.md). For known gaps and
follow-up work, see [`docs/open-issues.md`](docs/open-issues.md). Git history and PR descriptions
are the record of *how* things got built; don't restate that here.

## Architecture rules

- Clean Architecture, dependency direction: `Web`/`App` → `Application`/`Contracts`;
  `Application` → `Domain`; `Infrastructure` → `Domain`/`Application` (implements its
  interfaces). Nothing references `Infrastructure` except `Web`'s `Program.cs` (composition
  root) and `AppHost` (orchestration).
- **`DotGlasses.App` may reference `DotGlasses.Contracts` and `DotGlasses.Rules`, and nothing
  else.** If a change seems to need `App` to reference anything further, that's a signal the type
  belongs in `Contracts` (a wire shape) or `Rules` (a rule the device and the server must agree
  on) instead — flag it, don't add the reference.
- **`DotGlasses.Rules` may only ever reference `DotGlasses.Contracts`** — no `Domain`, no
  `Application`, no `Infrastructure`, no EF Core, no ASP.NET. `App` is a Blazor WASM app, so
  anything `Rules` drags in ships to the device; the reference rule above is only worth what this
  one enforces. It holds the consultation rules (pure functions over a request DTO plus a
  reference-data snapshot) and `ReferenceDataSnapshot`, and it is deliberately free of I/O: two
  adapters outside it do the loading — `ReferenceDataSnapshotProvider` (Infrastructure, from the
  database, retired items included) and `ReferenceDataSnapshotAdapter` (App, from the
  IndexedDB-cached API response, active items only). Rules ask "present **and** active", which is
  correct under both fillings. Rule failure keys are request-DTO property names and that is
  load-bearing — `FormErrors`, `ValidationProblemDetails` and `LeadConversionController`'s
  `Form.{PropertyName}` remap all key off it. See ADR-0002.
- **`ReferenceDataSnapshot` is the single `Guid`→label resolver server-side**, fallback `"—"`.
  Don't add a local `ToDictionary(x => x.Id, x => x.Label)` beside it — that's the pattern it
  replaced (seven implementations, four different fallback strings). It is registered scoped and
  memoized per request; it is deliberately **not** cached across requests (Container Apps scales
  to multiple replicas, so an in-memory cache would go stale per-replica on an admin edit — see
  ADR-0002). A service that writes reference data and then re-reads it in the *same* request must
  not use the memoized snapshot; today every Admin Portal write redirects instead.
- **`Contracts` must not reference `Domain` or `Application`** — it's a pure wire-shape layer, not
  because of a project reference someone forgot to add but because `App` referencing `Contracts`
  must not transitively pull in `Domain`/`Application`. DTOs that need an enum define their own
  copy in `Contracts` (e.g. `Contracts.Common.Gender` next to `Domain.Enums.Gender`) rather than
  referencing the Domain one; map between them in the Application layer. Validators that need a
  DB-backed check (e.g. "does this Guid reference an active reference-data item") can't be
  co-located with their DTO in `Contracts` for the same reason — they live in
  `DotGlasses.Web.Validation.*` instead, referencing `Application` interfaces directly.
- No MediatR. Plain application services with interfaces in `Application`, implementations
  alongside — no repository interface for entities only ever queried directly off `DbContext`
  (e.g. `EventHistoryQueryService`, `DashboardQueryService`, `CustomOrderService`,
  `OrganisationAdminService`, `ReferenceDataAdminService`, `PresetCatalogueAdminService`); a
  repository interface exists only where a service genuinely needs `Add`/`Update`/`GetById`
  (`IVisionTestRepository`/`ILeadRepository`/`ISaleRepository`, `ICustomerRepository`).
- Controller-based Web API (not Minimal APIs), versioned from `v1`, Swagger-visible (dev only).
- **A business-rule rejection throws `DomainRuleViolationException`** (`Domain/Common`, the only
  layer `Application`, `Infrastructure` and `Web` can all see — see ADR-0003). Its message is
  user-facing copy, shown verbatim; never a code. Never catch one in a controller:
  `DomainRuleViolationFilter`, registered globally in `Program.cs`, is the single place it becomes
  a response — a 400 `ValidationProblemDetails` (keyed on `""`) for `[ApiController]` actions, and
  for a server-rendered screen a redirect back to the screen the POST came from with the copy in
  TempData, rendered by `Views/Shared/_DomainRuleViolation.cshtml` from `_Layout`. A filter can't
  re-render an arbitrary MVC view (no controller instance on `ExceptionContext`, and each screen
  builds its view model in its own private helper), so POST-redirect-GET is how every screen is
  served alike. **`InvalidOperationException` now means only "missing/out-of-scope row or a bug"**
  — that's what EF throws for `FirstAsync` on an empty sequence, and it is deliberately left to
  surface as a 500: keeping the two types distinct is what makes a rejection recognisable at any
  catch site. Don't reach for a `Result` type; ADR-0003 rejected it with reasoning.
- **A multi-write Identity operation is made atomic with a real transaction, never a compensating
  "delete what I just made" path.** `UserManager`/`RoleManager` call `SaveChanges` internally on
  every operation, so batching them needs two things that happen to hold here: `ApplicationUser`
  lives in `DotGlassesDbContext` (it *is* the `IdentityDbContext`), and
  `AddEntityFrameworkStores<DotGlassesDbContext>` hands `UserStore` the same scoped instance the
  service holds — so an explicit transaction opened on that context covers Identity's writes and
  the service's own alike. Two things to get right: **check every `IdentityResult`** (Identity
  reports refusals as a return value, so an unchecked step is one the transaction commits over —
  that's how an invited user used to end up with no role), and **open the transaction through
  `Database.CreateExecutionStrategy().ExecuteAsync(...)`, not `BeginTransactionAsync` directly**.
  Aspire's `AddNpgsqlDbContext` enables connection retries by default
  (`NpgsqlEntityFrameworkCorePostgreSQLSettings.DisableRetry` is `false`) and a retrying strategy
  refuses a user-initiated transaction — a direct `BeginTransactionAsync` passes every test (the
  harness builds a plain `UseNpgsql` context with no retry strategy) and throws in staging and
  production. Because the strategy replays the whole delegate, build everything the attempt needs
  *inside* it and `ChangeTracker.Clear()` at the top: EF does not revert entity states on
  rollback. `UserAdminService.InviteAsync` is the worked example, pinned by
  `InviteAtomicityTests`. Anything a user-visible operation *emits* (an email, a set-password
  link) is produced **after** the commit — a live invite link for an account the rollback removed
  is worse than the failure it came from.
- Deliberately **not** using `AddFluentValidationAutoValidation()` — it runs FluentValidation
  synchronously inside ASP.NET's model-binding pipeline, which can't invoke the async rules
  several validators need for DB-backed checks (throws
  `AsyncValidatorInvokedSynchronouslyException`). Every controller calls
  `IValidator<T>.ValidateAsync` explicitly instead.

## Data scoping vs RBAC — do not conflate

- **Data scoping** (which rows a user can see) is a global EF Core query filter on
  `IHierarchyScoped` entities (`OrganisationNode`, `Customer`, `Test`, `Lead`, `Sale`,
  `WidgetExample`), keyed off `ICurrentUserContext.HierarchyPathPrefix`. It is role-independent:
  a row is visible if its `HierarchyPath` starts with the caller's own path. Scoping is downward
  only — your own node and everything beneath it, never above or beside you.
  `ReferenceDataItem`/`PresetCatalogue`/`LensOption`/`LensStrengthCoatingOption` are **not**
  hierarchy-scoped — they're a single global library, visible to every authenticated user.
  `ApplicationUser` is an Identity type, outside the automatic filter entirely — any screen
  listing users (User Directory) applies the same prefix rule manually in code.
- **RBAC** (what a user can do with rows they can see) is separate, policy-based
  `IAuthorizationHandler`/`[Authorize(Policy = ...)]` — role-dependent, never touches the
  query filter.
- **Resolving an *ancestor's* name/level** (e.g. "which country is this outlet in?") always needs
  `IUnscopedReportQueryService` — the sanctioned way to look outside a caller's hierarchy scope —
  never a plain scoped query, even for `OrganisationNode` itself: a plain scoped query against
  `OrganisationNode` only returns the caller's own subtree, so it silently can't see the caller's
  own ancestors. This has bitten two different reporting services independently; treat it as a
  standing gotcha, not a one-off (see Common pitfalls below).
- **A materialized path is `HierarchyPath` (`Domain/Common`) in application code, a `string` in the
  database.** The type owns the trailing-slash invariant and names the two containment directions
  so they can't be swapped: `IsSelfOrDescendantOf` (scoping — "which rows can this viewer see")
  and `IsSelfOrAncestorOf` (ancestor resolution — "which country/retailer sits over this row").
  `OrgTreeLookup` (`Application/Reporting`) is where **Retailer** — the nearest `Intermediate`-level
  ancestor, per `CONTEXT.md`, with "no Retailer" reported honestly rather than substituting the
  country — and the missing-name fallbacks are defined; resolve outlet/Retailer/country through it
  rather than re-deriving a prefix match. Persistence deliberately stays `string`: the
  reflection-built global filter in `DotGlassesDbContext` operates on the raw column and must not
  be "tidied" onto the value type — read `docs/adr/0004` before trying. `Contracts` and `App` never
  see the type (`Contracts` may not reference `Domain`; paths are stamped server-side from claims).

## Domain model (current shape)

Real domain entities, in `DotGlasses.Domain/Entities` and `/Enums`:

- **`OrganisationNode`** — arbitrary-depth org hierarchy. `OrganisationLevel`:
  `Dgi` (0) → `Country` (1) → `Intermediate` (2) → `RetailPoint` (3), ordered, only these four
  carry business rules (`Intermediate` covers every reseller/distributor tier via a free-text
  `Kind` label). Tree shape is enforced: DGI's only child level is Country; Country/Intermediate
  may have Intermediate or RetailPoint children; RetailPoint is always a leaf. `HierarchyPath`
  segments are minted as (global max segment across the whole tree) + 1 — globally unique and
  ever-increasing, not per-parent (no locking; see `docs/open-issues.md`). `IsTrainingOrg` nodes
  are excluded from Dashboard aggregates only (not Event History/Custom Orders/User Directory).
- **`Test`/`Lead`/`Sale`** — separate atomic create-once events, no update endpoint by design
  (server-side linking happens inside the service layer instead): a Test converts to a Lead
  (`Test.ConvertedToLeadId`, `Lead.SourceTestId`) and a Lead converts to a Sale
  (`Lead.ConvertedFlag`/`SaleId`, `Sale.SourceLeadId`), both atomic via `IUnitOfWork`. Create
  requests never accept `HierarchyPath`/`TechnicianUserId` from the client — the controller
  stamps both from `ICurrentUserContext`. The Application-layer types for `Test` are named
  `IVisionTestRepository`/`IVisionTestService`/`VisionTestService`, not `ITestRepository` — that
  name would collide with the `DotGlasses.Application.Tests` xUnit project's own root namespace.
  The Domain entity itself is still `Test`.
- **`PresetCatalogue`/`LensOption`** — admin-configurable lens ranges. A catalogue's roster is
  "which curated `LensStrength` reference items are included, in what order" — the actual power/
  bifocal-ness lives in the reference item's own label (e.g. `+2.50`, `+0.00 / +2.50 (Bifocal)`),
  not typed columns on `LensOption`. `PresetCatalogueKind` (`Other`/`SixLensSet`/`NineLensSet`)
  identifies which catalogue drives the Field App's two preset-range buttons — at most one
  catalogue may hold each of `SixLensSet`/`NineLensSet`. `LensStrengthCoatingOption` is the
  many-to-many "this lens strength is sellable in this coating" — a strength with zero configured
  coatings can't be sold on a preset range (see `docs/open-issues.md`).
- **`ReferenceDataItem`** — one generic table backing every admin-managed dropdown, keyed by
  `ReferenceDataCategory` (Reasons not purchased, Referral reasons, Coatings & tints, Frame
  colours, Hard case colours, Occupations, Lens strengths). Retiring an option sets `IsActive =
  false`, never a hard delete — historical `Test`/`Lead`/`Sale` rows may reference it by Id, and
  Event History resolves labels against retired items too. At most one *active* `IsOtherOption`
  item per category (server-enforced), which is what makes a dropdown reveal a free-text field.
- **`Customer`** — internal-only, matched by exact name + phone within an outlet, find-or-create,
  no public API, no fuzzy matching.
- **`WidgetExample`** remains the architectural reference pattern (audit/soft-delete/hierarchy-
  scoping/offline-sync skeleton) alongside the real entities — don't delete it, and don't treat
  its own repository/controller as a template to literally copy for a new *reporting* service
  (see the no-repository-interface rule above).

## RBAC model (current state)

**Two roles: `Admin`/`User`** (`RoleNames`), assignable at any org node, scope = that node +
everything beneath it. `Manager` was removed (collapsed into `Admin` — it had never been
functionally distinct from Admin anywhere).

| Policy | Rule | Gates |
|---|---|---|
| `ReferenceData.Manage` | Admin, DGI level only | Reference Data screen |
| `PresetCatalogue.Manage` | Admin, Country level+ | Preset Catalogues screen |
| `CustomOrders.View` | Any role, Country level+ | Custom Orders screen + its advance-status action |
| `Organisations.ManageInScope` | Admin, resource-based (target org at/below caller) | Every Organisations write action |
| `Users.ManageInScope` | Admin, resource-based (target user at/below caller) | Every User Directory write action |
| `WidgetExample.Create` | Admin (no level/scope check) | Developer sandbox API only |

Backed by `OrgLevelRequirement` (no DB round trip — reads `ICurrentUserContext.OrgLevel`,
denormalized onto `ApplicationUser.OrgLevel`, stamped as a JWT/cookie claim at sign-in) and
`HierarchyDescendantRequirement` (resource-based subtree check, for a controller acting on a
specific target user/org). Dashboard, Organisations, Event History and User Directory carry only
`[Authorize]` — any authenticated user reaches them; what they see is narrowed by data scoping,
not by policy.

The sidebar (`_Layout.cshtml`) hides Preset Catalogues/Custom Orders/Reference Data per-request
via `IAuthorizationService.AuthorizeAsync` against the same three policies their controllers
enforce — nav filtering is real, not decorative, but every controller action still re-checks
server-side regardless (never trust the hidden-button UX alone). A failed policy check redirects
to `/Account/AccessDenied`, a real page — not a bare 404.

## Offline sync (Field App) — outbox pattern

New `App` features that write data go through the outbox pattern (IndexedDB pending-sync table,
client-generated GUID as idempotency key, `ISyncService` draining on reconnect/timer/manual-sync)
— **never call the API directly from a Blazor page/component.**

`SyncItemAsync`/`TrySyncItemAsync` return one of three outcomes, and the distinction is load-
bearing: **Succeeded** (marked Synced, never sent again), **Deferred** (network error/5xx — left
queued, retried indefinitely; this is the normal offline path), **Rejected** (400/401/403 —
marked `Failed`, terminal, excluded from the retry queue, surfaced on `/failed-records` with the
parsed field error so a technician can fix-and-resend or discard). `FormErrors`
(`App/Validation`) keys are the request DTO's own property names, so a server
`ValidationProblemDetails` response maps straight onto the right form field with no translation
table — used for both client-side pre-submit checks and mapping a server rejection back.

The JWT (`AuthTokenStore`) and reference data/preset catalogues (`ReferenceDataClient`) are both
persisted/cached in IndexedDB (write-through on a successful load, fallback to last-cached copy
on failure) — a technician who's been online at least once can keep working, and stay signed in
across a refresh, with no connectivity. First-ever use still needs one online session.

Known accepted risk, not yet fixed: offline records are attributed to whoever is signed in
*when they sync*, not when they were created (`TechnicianUserId`/`HierarchyPath` come from the
JWT on the POST). Client-side mitigation blocks sign-out and location-switch while the outbox is
non-empty; a token expiring mid-queue still slips through. See `docs/open-issues.md` before
attempting a fix — the request DTOs deliberately omit these fields, so "accept them from the
body" is not a safe shortcut.

## UI / design system

- Design tokens (colors, type, spacing) live in `wwwroot/css/dot-glasses.css` in **both** `Web`
  and `App` — hand-ported, kept in sync **by hand**. There's no shared static-asset project
  between a server-rendered MVC app and a WASM app to source one file from. If token values ever
  change, update both.
- Bootstrap is present in both projects (grid, form controls, the native modal JS) — the design
  system layers custom `dg-*` classes/tokens on top rather than replacing it.
- All seven Admin Portal screens (Dashboard, Organisations, Event History, User Directory, Preset
  Catalogues, Custom Orders, Reference Data) and the Field App's consultation forms are wired to
  real data — no controller returns hardcoded placeholder data. The Field App's `Messages` and
  `Outlet select` screens are still static placeholders (Settings' location picker is real; see
  `docs/functional-capabilities.md`).

## Deployment (Azure)

- `Web` deploys to Azure Container Apps; Postgres to Azure Database for PostgreSQL Flexible
  Server (Entra ID auth via managed identity — no connection-string password, `passwordAuth:
  'Disabled'`). Local dev runs Postgres as a container via `.RunAsContainer(...)` with a pinned
  password parameter (`postgres-password`, from AppHost's user secrets) — pinning it (rather than
  letting Aspire auto-generate one) is what keeps the local Postgres data volume's credentials
  stable across AppHost resource-shape changes.
- **`DotGlasses.App` (the PWA) is a *separate* azd project** (`src/DotGlasses.App/azure.yaml`,
  its own `azd up`) sharing the same two resource groups as the root project — not folded into
  the root `azure.yaml`. azd (1.29.0) refuses to mix an Aspire-detected service with a
  hand-declared one in one project, and no Aspire hosting integration for Azure Static Web Apps
  exists. Don't try to fold this into the AppHost model without re-checking that constraint.
  `src/DotGlasses.App/infra/main.bicep` is hand-authored (not `azd infra gen` — that's Aspire-
  manifest-specific and errors on a plain project); the Static Web App module uses the public AVM
  registry (`br/public:avm/res/web/static-site:0.3.0`), its own narrower region allow-list
  (`westus2`/`centralus`/`eastus2`/`westeurope`/`eastasia` — doesn't include South Africa, so it
  stays on its Bicep-default `westeurope` even though the shared resource group uses
  `southafricanorth`), and `provider: 'Custom'` + `'azd-service-name': 'field-app'` (must match
  `azure.yaml`'s service name exactly — both load-bearing, not decorative).
- `/infra` (root) is **generated** via `azd infra gen --force` from the AppHost model after any
  AppHost resource change — treat it as regenerable output, never hand-edit the Bicep directly.
- **CAF naming convention** (Cloud Adoption Framework), one subscription, two resource groups:
  `rg-dotglasses-nonprod` / `rg-dotglasses-prod`. Every resource name is built in `AppHost.cs` via
  `ConfigureInfrastructure` — `EnvToken()` pulls the env token (`nonprod`/`prod`) out of
  `resourceGroup().name` at Bicep-evaluation time via a hand-built `substring()`
  `FunctionCallExpression` (the installed `Azure.Provisioning` version has no
  `Substring`/skip wrapper on `BicepFunction`, confirmed via reflection — only `Take()` exists).
  Globally-unique resource types (Storage, Key Vault, the two ACS resources) append a
  `uniqueString()` hash.
- **Key Vault (Phase 8) and ACS both have no local emulator**, so their AppHost declarations are
  gated behind `builder.ExecutionContext.IsPublishMode` — plain `dotnet run` never touches
  either, and `Program.cs`/`AddInfrastructure` conditionally register the real Key
  Vault-backed config / `AzureEmailSender` only when AppHost actually wired the corresponding
  connection string/env vars, falling back to the dev-only `appsettings.Development.json` JWT
  key / `LoggingEmailSender` otherwise. Key Vault *does* have a real Aspire hosting integration
  (`AddAzureKeyVault` + `WithReference`, RBAC wired automatically); ACS does not, so it's added
  via `AddBicepTemplate("acs", "acs.bicep")` (hand-authored) instead.
- **No infra is ever deployed from a developer machine** — only via GitHub Actions
  (`.github/workflows/deploy.yml`), OIDC federated credentials (`azd auth login
  --federated-credential-provider github`, no stored client secret). Auto-deploys to a `staging`
  GitHub Environment on every successful CI run on `main`, then to `production` gated by a
  required-reviewers rule. A CI step runs `dotnet ef database update` against a short-lived
  Postgres AAD token after `azd up` — this is the *only* place migrations get applied to a real
  environment (`Program.cs`'s auto-migrate-on-boot is `IsDevelopment()`-gated and never runs
  against a real database).
- **Field App per-environment config**: `wwwroot/appsettings.{Environment}.json` (Staging/
  Production, alongside the dev-only `appsettings.json`), selected at build time via the
  `WasmApplicationEnvironmentName` MSBuild property — `deploy.yml` sets it as a job-level env var,
  which flows to azd's internal `dotnet publish` and MSBuild picks up automatically (there's no
  first-class azd/`azure.yaml` field to inject an MSBuild property for a `staticwebapp`-host
  service). Blazor WASM has no secure client-side storage regardless of delivery mechanism — a
  genuine secret can never live in the Field App directly, only proxied through `Web`'s backend.
- This repo is **public** — `/design` (Claude Design handoff bundle) and local Claude Code
  settings are gitignored; never commit them or reference them from `README.md`.

## Common pitfalls (debugging guidance)

Recurring bug patterns worth checking for *before* they bite again, each found live at least
once in this codebase:

- **A `bool` C# expression bound as the entire value of a non-checkbox/disabled/readonly HTML
  attribute** (e.g. `<input value="@someBoolExpr">`) renders as an HTML boolean attribute
  regardless of the attribute's actual name — Razor emits `value="value"` when true and omits the
  attribute entirely when false, **never** the string `"True"`/`"False"`. Model binding silently
  receives the wrong value or nothing. Fix: `.ToString()` or a ternary to a real string. Bitten
  two separate screens (Organisations' flag toggles, Preset Catalogues' coating-availability
  grid) — treat any bare-bool-bound attribute as a standing red flag in review.
- **A `DateOnly`/`DateTime` value placed into a URL** (`asp-route-*`, a query string) via plain
  Razor interpolation calls `.ToString()` with the request's culture, which can render day/month
  in a different order than the model binder parses it back — silently swapping day and month
  and returning zero results. Every such binding needs an explicit culture-invariant
  `.ToString("yyyy-MM-dd")`, not just the `<input type="date">` site that happens to share the
  same underlying value.
- **Resolving an ancestor's name/level for any `IHierarchyScoped` entity** — including
  `OrganisationNode` resolving its own ancestors — needs `IUnscopedReportQueryService`, never a
  plain scoped query. The standard hierarchy filter only returns the caller's own subtree
  downward; a plain query silently can't see anything above the caller, which shows up as
  "Unknown country"/"Unknown outlet" for any caller below the level being resolved. Caught twice
  independently (Dashboard, Event History) before being treated as a standing rule.
- **Un-hiding a soft-deleted (or otherwise globally-filtered) entity needs an explicit
  `IgnoreQueryFilters()` path at *every* point in the chain** — both the authorization check's
  target-lookup and the actual mutation's entity-fetch, not just one. Fixing only one still fails
  ("target not found" / "sequence contains no elements") because the other query silently applies
  the same filter that's hiding the very row being un-hidden.
- **Local dev: Docker Desktop's engine can be down while the CLI still responds to some
  commands.** If the Aspire AppHost logs "Container runtime 'docker' was found but appears to be
  unhealthy" and then hangs with no further output, run `docker info` — if it fails to reach the
  daemon (no `Server:` section), Docker Desktop's engine isn't actually running even though the
  process/CLI exist. Launch `Docker Desktop.exe`, poll `docker info` until it succeeds, then
  (re)start the AppHost.
- **Pushing a branch that touches `.github/workflows/*.yml` needs the `workflow` GitHub OAuth
  scope** on whatever credential `git push` uses. A `gh`-issued credential without it gets a
  server-side rejection ("refusing to allow an OAuth App to create or update workflow ... without
  workflow scope") no matter how many times you retry — it's not a local config issue. Fix:
  `gh auth refresh -h github.com -s workflow`, which needs the user to complete an interactive
  device-code flow in their own browser (relay the printed one-time code/URL; the command itself
  can be started from a session, but the authorization step cannot).

## Testing

xUnit. Integration tests run against a **real containerised Postgres** via Testcontainers, not
the EF Core InMemory provider — InMemory implements no transactions (so atomicity is untestable
under it) and does not reproduce the SQL string-matching semantics the hierarchy filter depends
on. `Infrastructure.Tests` shares one container per assembly and applies the real migrations;
`Web.Tests` points `WebApplicationFactory` at its own container. The two assemblies deliberately
use different state-isolation strategies (fresh database per test vs a shared one) because the
Web host bakes its connection string in at build time — each file says so where it matters.

**`Application.Tests` must stay dependency-free** — pure rule and service tests, hand-written
dictionary-backed fakes, no container, no database. No mocking library is referenced by any
project, deliberately; don't add one.

## Running locally

`dotnet run` on the `DotGlasses.AppHost` project (starts Postgres via container, `Web`, and the
Aspire dashboard). Dev-only seeded accounts (`DevUserSeeder`) require `DevSeed:AdminPassword` /
`DevSeed:KenyaManagerPassword` / `DevSeed:RetailPointUserPassword` set via `dotnet user-secrets
set` from `src/DotGlasses.Web` — unset secrets simply skip seeding that account, they don't
error. For the Field App standalone dev server: `dotnet run` on `DotGlasses.App`.

## Agent skills

### Issue tracker

Issues and specs live as markdown files under `.scratch/<feature-slug>/`. See
`docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`,
`ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
