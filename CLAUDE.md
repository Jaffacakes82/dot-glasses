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

One assumption made while seeding, not explicitly discussed on the call — confirm once the
Reference Data admin screen is checked against real DGI usage: `FrameColour` has an "Other"
fallback row for consistency with every other reference list, even though the call named exactly
6 fixed colours. (The other original assumption here — every non-bifocal seeded `LensOption`
defaulting to the "Clear" coating — is superseded: coating-per-lens-strength is now a real,
admin-configurable many-to-many, seeded deliberately empty for non-bifocals. See the Preset
Catalogues admin wiring section below.)

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
correctness + "Other"-text-required checks, plus `LensOptionBelongsToCatalogueAsync` for
preset-range consistency) and `ICustomerRepository` (`DotGlasses.Application/Customers` — exact
name+phone find-or-create only, no public API; fuzzy/suggested-match UX is Field App UI work for
later). **Coating resolution changed 2026-08-05** — see the Preset Catalogues admin wiring section
below: `SaleService` no longer derives `Sale.CoatingRefId` for preset ranges; the client always
submits it and the validator checks it's legal (still one `CoatingRefId` column, not per-eye —
same known simplification, now expressed as "left eye's configured coating set" rather than "left
eye's single forced coating").

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

## Admin Portal wiring (Reference Data screen)

First of the seven Admin Portal screens wired to real data (2026-08-05) — Dashboard,
Organisations, Event History, User Directory, Preset Catalogues, Custom Orders still
placeholder. Chosen first because it's foundational (every other admin screen and the Field App
itself depends on reference data existing) and `AuthorizationPolicies.ReferenceDataManage`
(Admin-at-DGI only) was already enforced on the controller, just unused.

**New write path**: `IReferenceDataAdminService` (Application) /
`ReferenceDataAdminService` (Infrastructure, queries `DotGlassesDbContext.ReferenceDataItems`
directly — no repository interface for this entity, matching `PresetCatalogueQueryService`) —
`ListAllAsync` (every item incl. retired), `CreateAsync` (slugifies `Code` from the label,
`SortOrder` = max+1 in category), `DeactivateAsync`/`ReactivateAsync`. Deliberately separate from
`IReferenceDataQueryService`, which the Field App also depends on (active items only) — same
segregation precedent as the MVC `CataloguesController` vs. API `PresetCataloguesController`
split. Uses `Domain.Enums.ReferenceDataCategory` directly throughout (Application →
Infrastructure → the MVC controller/view, no mirrored enum) since `DotGlasses.Web` has no
restriction on referencing `Domain` — that restriction is specific to `DotGlasses.App`.

**Removing an option retires it** (`IsActive = false`), never a hard delete — historical
`Test`/`Lead`/`Sale` rows may still reference it by Id. The screen shows a collapsed "Retired"
sub-list per category with a "Restore" action, since without one a mis-click permanently hides an
option with zero in-app recovery.

**Three additions beyond wiring the existing 6 categories** (scoped via user decision before
implementation, 2026-08-05):
1. **`ReferenceDataCategory.LensStrength`** (7th category, no migration needed — plain label list
   like every other category, e.g. "+2.50", "+0.00 / +2.50 Bifocal"). This pass only adds the
   category so values can be curated; `PresetCatalogue`/`LensOption` are **not** rewired to build
   from it yet — that raises its own design question (does a catalogue pick N strengths + a
   per-strength coating override? does `LensOption` keep its typed SphericalPower/IsBifocal/
   AddPower fields or gain a FK to this list?) and is flagged in `[OPEN]`, not guessed at.
2. **`ReferenceDataItem.ImageUrl`** (new nullable column) — the CEO wants a photo next to each
   Frame colour option. Generic on the entity but only surfaced in the Create form (and shown as
   a thumbnail) for Frame colour; admin pastes a URL, since no blob storage exists yet to build
   real upload — flagged in `[OPEN]`. `Contracts.ReferenceData.ReferenceDataItemDto` also carries
   `ImageUrl` now for read-API consistency, though the Field App doesn't consume it yet.
3. **`IsOtherOption` is now settable from the Create form** (a checkbox), not just baked into
   seed data — guarded server-side (`IReferenceDataAdminService.HasActiveOtherOptionAsync`, used
   by `CreateReferenceDataItemRequestValidator`'s `CustomAsync` rule in
   `Web.Validation.ReferenceData`) so at most one *active* Other-flagged item can exist per
   category; consuming dropdowns (`ReferenceDataDropdown.razor`) key off this flag to reveal a
   free-text field, so two would be ambiguous. Verified live: a second attempt in the same
   category was rejected with the checkbox's server-enforced guard, even after forcing the
   client-side `disabled` attribute off via devtools.

**MVC pattern established here** (first real write-capable MVC controller besides
`AccountController`): `[HttpPost][ValidateAntiForgeryToken]` actions, Post-Redirect-Get, FluentValidation
via an explicit `IValidator<T>.ValidateAsync` call + `ValidationResult.AddToModelState(ModelState)`
(not `AddFluentValidationAutoValidation`, removed earlier this session — see Test/Lead/Sale API
above) for the one DB-backed rule; a single page-level `asp-validation-summary="All"` banner on
failure rather than per-card error placement, since one shared `CreateReferenceDataItemRequest`
posts from seven identical per-category forms on one page — acceptable simplification for a first
admin screen, not something to over-engineer.

**Verified live**: signed in as the seeded DGI Admin, added a Lens strength value, added a Frame
colour with an image URL (thumbnail rendered), added and then rejected a second "Other" in the
same category, retired then restored an option — each round-tripped through Postgres. Then signed
into the Field App as the retail-point user and confirmed the newly-added Frame colour appeared
live in `ConsultationForm.razor`'s Sale form — proves the two screens share the same underlying
data, not just similar UIs.

## Admin Portal wiring (Organisations screen)

Second Admin Portal screen wired (2026-08-05, same day as Reference Data) — real org-tree
rendering + write actions, replacing `OrganisationsController`'s hardcoded 8-node placeholder
tree. Scoped the same way Reference Data was: the design mockup's full action set (Add child
node, training-org/custom-orders toggles, Assign users, Create/assign preset catalogue) minus
"Assign users" (User Directory's job, still placeholder, `ManageUsersInScope` equally unwired)
and the two preset-catalogue actions (`PresetCatalogue` has no write API yet — Preset Catalogues'
own future screen).

**First real use of resource-based RBAC**: `HierarchyDescendantRequirement`/
`AuthorizationPolicies.ManageOrgInScope` existed since the RBAC pass but were never wired to a
controller (their own doc comments said so explicitly). `OrganisationsController` now calls
`IAuthorizationService.AuthorizeAsync(User, targetNode.HierarchyPath, ManageOrgInScope)` — for
"Add child" the resource is the *parent* being added to; for the two flag toggles it's the node
itself (same node in both cases, since you always operate on whichever node is currently
selected). Buttons/forms are hidden server-side for a user who'd fail the check (computed once
per request into `OrganisationsIndexViewModel.CanManage`), and every POST action re-checks it
server-side regardless — never trust the hidden-button UX alone.

**Reading needs no special handling** — `OrganisationNode` implements `IHierarchyScoped`, so a
plain scoped query already returns exactly "the caller's own node + everything below it"; a DGI
Admin sees the whole tree, a Manager sees only their own subtree (their node becomes the
*displayed* root even though it has a real `ParentId` pointing above them — that ancestor is
simply filtered out of their result set, which the controller's root-detection already handles:
whichever node has no `ParentId`, or a `ParentId` not present in the caller's own result set, is
the root for display purposes).

**Minting a new `HierarchyPath` *does* need `IUnscopedReportQueryService`**: existing paths
(`/1/`, `/1/2/`, `/1/2/3/`) are small ever-increasing integers assigned in creation order across
the whole tree, not per-parent — the existing `GetOrganisationNodePathsUnscopedAsync` (added for
the `PresetCatalogueQueryService` fix earlier this session) was reused as-is, no new method
needed. **Known simplification, accepted for now**: `OrganisationAdminService.CreateChildAsync`
computes the next segment as (global max parsed across every path) + 1, read-then-increment with
no locking — a small race window exists under concurrent creates. Acceptable for an infrequent,
admin-only action; would need a real sequence/lock if org creation ever became high-throughput.

**New `IOrganisationAdminService`** (Application) / `OrganisationAdminService` (Infrastructure,
queries `DbContext.OrganisationNodes` directly, no repository interface for this entity) —
`ListAsync`, `IsValidChildLevel` (Dgi's only child level is Country; Country/Intermediate's is
Intermediate or RetailPoint, admin's choice; RetailPoint has none — enforced both in the service
and in `CreateChildOrganisationRequestValidator`'s `CustomAsync` rule, and the "Add child
node"/Level-select UI is hidden/constrained accordingly), `CreateChildAsync`,
`SetTrainingOrgFlagAsync`, `SetCanHandleCustomOrdersAsync` (rejects if the target isn't
Country-level).

**One real Razor bug found and fixed via the live pass**: the two flag-toggle forms originally
bound a raw `bool` C# expression directly to an `<input value="...">` attribute
(`value="@(!Model.Selected.IsTrainingOrg)"`). Razor treats *any* attribute bound to a `bool`-typed
expression as an HTML boolean attribute (like `disabled`/`checked`) regardless of the attribute's
actual name — it renders `value="value"` (attribute name repeated) when true and omits the
attribute entirely when false, never the string "True"/"False" a naive reader would expect. The
toggle POSTs were silently binding `value` to `false` every time as a result (model binding fell
back to the parameter's default). Fixed by explicitly calling `.ToString()` on the expression
(`value="@((!Model.Selected.IsTrainingOrg).ToString())"`) so Razor sees a `string`, not a `bool`.
Caught by actually clicking the toggle live and checking Postgres, not by build/test — worth
remembering for any future boolean value bound into a non-boolean-semantic HTML attribute.

**Verified live**: as the seeded DGI Admin, added an Intermediate child under Kenya ("Mombasa
Retail Group", `HierarchyPath` correctly minted as `/1/2/5/`), toggled its training-org flag
(after the bug fix above, confirmed in Postgres and the UI). Then signed in as the seeded Kenya
Manager and confirmed their tree shows Kenya as the root with DGI itself not visible at all
(only Kenya + its own subtree, which does include the DGI-Admin-created Mombasa Retail Group,
correctly — Manager can see everything below their own node), and that they can still create
their own child (a RetailPoint "Nakuru Central" under Kenya, path `/1/2/6/` — correctly
continuing the *global* segment counter from Mombasa's 5, proving the unscoped max-segment lookup
works regardless of which caller's request triggered it) — proving both the read-scoping and the
resource-based write RBAC actually restrict, not just that the DGI-Admin happy path works.

## Admin Portal wiring (Event History screen)

Third Admin Portal screen wired (2026-08-05, same day as Reference Data and Organisations) —
pure read/reporting, no write actions, over `Test`/`Lead`/`Sale` (built earlier this session).
Picked over the four remaining placeholder screens (Dashboard, User Directory, Preset Catalogues,
Custom Orders) specifically because it needed no new domain modelling — Dashboard's "retail-point
type distribution" and Custom Orders' fulfilment-status tracking both need concepts that don't
exist anywhere in the domain yet, and User Directory means touching real account/password
creation. Unusually, `Views/EventHistory/Index.cshtml`/`Web/Models/EventHistoryModels.cs` already
matched the real 4-tab design (`design/admin/event-history.jsx`) almost exactly, including a
`PhoneMasked` field already anticipating masking — only `EventHistoryController` still returned
hardcoded rows, so this pass is almost entirely Application/Infrastructure.

**Reading needs no special handling** — same insight as Organisations: `Test`/`Lead`/`Sale`/
`Customer`/`OrganisationNode` all implement `IHierarchyScoped`, so a plain scoped query already
returns exactly what a viewer is allowed to see. This is also literally what "Event History...
scoped to the viewer's role + org" (RBAC permission matrix, below) turns out to mean — a `User`
is always assigned at RetailPoint level, so their own `HierarchyPathPrefix` already *is* just
that outlet, no extra role-based filtering needed on top of the automatic hierarchy filter.

**New `IEventHistoryQueryService`** (Application) / `EventHistoryQueryService` (Infrastructure,
queries `DbContext` directly for `Test`/`Lead`/`Sale`/`Customer` rather than through
`IVisionTestRepository`/`ILeadRepository`/`ISaleRepository`, which exist for the write side's
`Add`/`Update`/`GetById` needs, not bulk reads) — `ListSalesAsync`, `ListTestsAsync`,
`ListLeadsAsync(searchByName)`, `ListReferralsAsync` (`Test` rows where `Outcome == Referred` — a
filtered view of the same data `ListTestsAsync` shows unfiltered, not a separate entity; verified
live that a single Referred test genuinely appears in both).

**Outlet/Country resolution**: fetches every `OrganisationNode` visible to the caller once per
request (same small-scoped-dataset approach `OrganisationsController` uses for tree building),
exact-matches `HierarchyPath` for the outlet, prefix-matches the nearest `Country`-level ancestor.
Falls back to "Unknown outlet"/"Unknown country" rather than throwing on an unresolvable path —
confirmed live and genuinely useful: a handful of Sale/Test rows created directly against the API
earlier this session (by the DGI Admin account itself, not through the Field App, so stamped with
DGI's own `HierarchyPath` rather than a real RetailPoint's) render as "Unknown country" instead of
crashing the whole page.

**Reference-data label resolution** (referral reason, reason-not-purchased) uses
`IReferenceDataAdminService.ListAllAsync()` (added for the Reference Data screen — every item,
including retired), not the Field-App-facing `IReferenceDataQueryService.ListActiveAsync()` — a
historical event can reference a since-retired item, which `ListActiveAsync` would silently fail
to resolve. `IsOtherOption` items resolve to their row's own `...OtherText` instead of the generic
"Other" label.

**Phone masking**: `EventHistoryQueryService.MaskPhone` keeps the first 4 / last 3 characters,
redacts the middle with a fixed run of `•` — the design mockup's sample data hardcodes an
already-masked string per row rather than a real masking function to copy, and that exact format
assumes a specific number length/shape (Kenyan), so this is a deliberately more general scheme
rather than a literal port. Leads' "Logged" column gets a small relative-time helper (minutes/
hours/days ago, falling back to an absolute date past a week); Sales/Tests/Referrals show an
absolute local timestamp. Both live in `EventHistoryController` (formatting is a Web/display
concern), not `EventHistoryQueryService` (which returns raw `DateTimeOffset`).

**Verified live**: as the seeded DGI Admin, confirmed all four tabs render real data with
correctly resolved outlet/country names and reference-data labels (including the "Unknown
country" fallback on genuinely orphaned rows from earlier this session, and a `0001-01-01`
`CreatedAtUtc` on one Test predating the `AuditSaveChangesInterceptor` fix documented above —
both pre-existing historical artifacts surfaced correctly, not new bugs); recorded a fresh
Referred test via the Field App and confirmed it appeared in both Tests and Referrals; confirmed
Leads search filters by name while preserving the active tab in the URL, and that phone masking
renders. Then signed in as the seeded Kenya Manager and confirmed the Tests tab shows only their
4 Kenya-subtree rows — the DGI-stamped orphan rows visible to the Admin are correctly invisible
to them, proving the same automatic hierarchy scoping Organisations relies on applies here too.

## Admin Portal wiring (User Directory screen)

Fourth Admin Portal screen wired (2026-08-05, same day as the other three) — the actual
account-provisioning gap, since nothing beyond the three `DevUserSeeder` dev accounts could sign
in before this. First real consumer of `AuthorizationPolicies.ManageUsersInScope`/
`HierarchyDescendantRequirement`, which had sat unwired since the RBAC pass specifically for this.

**Invite-link flow, not admin-set passwords** — decided with the user before implementing. ASP.NET
Identity's `AddDefaultTokenProviders()` was already registered (`Program.cs`), so
`UserManager.GeneratePasswordResetTokenAsync`/`ResetPasswordAsync` work with no extra setup; the
only real gap was delivery (no `IEmailSender`, no SMTP/mail config anywhere in this codebase).
`IUserAdminService.InviteAsync` creates the `ApplicationUser` with **no password at all**
(`PasswordHash` stays null — this is what "Invited" status means, derived, not a stored column),
generates a real reset token, and the Web layer builds a `/Account/SetPassword?userId=&token=`
link. Since there's no real email sending yet, that link is shown **once** on the Admin Portal
page after Invite/Reset (`TempData["SetPasswordLink"]`) for the admin to relay manually. The user
visits the link, sets their own password on a real anonymous `AccountController.SetPassword`
page (`UserManager.ResetPasswordAsync` — the same "forgot password" API works fine against a
user who never had a password to begin with), which also flips `EmailConfirmed = true` — that
transition (PasswordHash null → set) is what moves a user from Invited to Active. `IEmailSender`
(`Application/Notifications`) is a one-method stub (`LoggingEmailSender`, Infrastructure) that
just logs — real Azure Communication Services delivery is explicit `[OPEN]` work the user will do
themselves; nothing about the token mechanics needs to change when that lands, only what happens
after `InviteAsync` returns the link in the Web controller.

**Multi-org assignment is real now** — `UserOrgAssignment` ("which org nodes a user can switch
between") existed since the domain-modelling pass but had no writer or reader anywhere. The
Invite form's org checkbox list (sourced from `IOrganisationAdminService.ListAsync()`, already
scoped to the caller) can select several; every selection becomes a `UserOrgAssignment` row, and
the *first* one is also stamped as the "primary" org onto `ApplicationUser.OrgNodeId`/
`HierarchyPath`/`OrgLevel` (and therefore the JWT/cookie claims) — there's still no "switch
active location" UI anywhere to make a more elaborate primary-selection UX meaningful yet.

**`ApplicationUser` is not covered by the automatic hierarchy-scoping query filter** — that
filter only walks Domain entities implementing `IHierarchyScoped`; `ApplicationUser :
IdentityUser<Guid>` is an Identity/Infrastructure type, never in scope for it.
`UserAdminService.ListAsync` filters manually (`.Where(u => u.HierarchyPath.StartsWith(prefix))`)
using the same `ICurrentUserContext.HierarchyPathPrefix` the automatic filter itself is built on
— worth remembering for any future screen that lists `ApplicationUser` rows directly.

**Two small schema additions beyond what was originally scoped**: `ApplicationUser.LastLoginUtc`
(stamped on *both* sign-in paths — `AccountController.Login` for the Admin Portal and the API's
`AuthController.Login` for the Field App, since a `User`-role account almost never touches the
Admin Portal, so only stamping the cookie path would leave this permanently null for most real
users) and `ApplicationUser.FullName` (nullable — the three seeded dev accounts predate it and
fall back to displaying their username/email).

**Suspend/Unsuspend uses Identity's own lockout mechanism**
(`SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)` / `(user, null)`), not a parallel
`IsActive` flag — "Suspended" status is derived from `LockoutEnd` being in the future, same
derived-not-stored approach as "Invited"/"Active".

**Verified live, the full loop**: as the seeded DGI Admin, invited a new Manager assigned to two
org nodes (Kenya + a RetailPoint under it) — confirmed in Postgres that the primary org stamped
correctly (first-selected, matching DOM/submission order) and both `UserOrgAssignment` rows were
created. Opened the shown link, set a password, and signed in as the new user — confirmed
`LastLoginUtc` stamped and status flipped Invited → Active in the directory. As the DGI Admin,
Suspended that user and confirmed sign-in was then rejected (generic "Invalid username or
password," not a leak that the account exists but is locked); Unsuspended and confirmed sign-in
worked again. Throughout, signed in as the new Manager and confirmed she could see herself plus
other Kenya-subtree users but not the DGI-level admin — the same manual-prefix-filter symmetry
Organisations' automatic filter already proved, just exercised through the non-`IHierarchyScoped`
path this screen needed.

## Admin Portal wiring (Preset Catalogues screen) — and a coating-model rework

Fifth Admin Portal screen wired (2026-08-05) — but bigger than a screen wiring: scoping it against
the design mockup (`design/admin/screens-b.jsx`'s `Catalogues`, just three text fields) surfaced
that the user wanted real lens-by-lens catalogue building, which meant reworking how lens
strength/coating work at the domain level, confirmed via a screenshot of the exact 6-lens/9-lens
option lists before implementing.

**`LensOption` reshaped**: dropped `SphericalPower`/`IsBifocal`/`AddPower`/`CoatingId` (typed
columns per row), gained `LensStrengthRefId` (FK to `ReferenceDataItem`, `Category =
LensStrength`) — a catalogue's lens roster is now "which curated `LensStrength` items are
included, in what order," with the actual power/bifocal-ness living in the reference item's own
`Label` (e.g. `"+0.00 / +2.50 (Bifocal)"`) rather than duplicated as typed columns. 16 distinct
`LensStrength` items seeded (12 standard + 4 bifocal, overlapping between the two catalogues where
the screenshot's lists overlap) — exact values came from the user's screenshot, not guessed.

**New `LensStrengthCoatingOption`** (`LensStrengthRefId`, `CoatingRefId`, `CreatedAtUtc`) — a
many-to-many "this lens strength is available in this coating," editable from this screen (not
Reference Data — keeps Reference Data's cards uniform across categories; this relationship is
really about how catalogues work). **Only the 4 bifocal strengths are seeded with a coating**
(→ Photochromic, the one fact actually known from the CEO call) — all 12 non-bifocal strengths
ship with zero configured coatings, a deliberate, visible interim gap (flagged in `[OPEN]`), not
an oversight — DGI populates the real matrix themselves via this same screen.

**Coating resolution flipped from derived to validated**: `SaleService` previously derived
`Sale.CoatingRefId` for preset ranges from the chosen lens's single forced `CoatingId`, ignoring
any client value. That derivation is gone — for both preset and custom ranges the client now
submits the coating it wants, and `IReferenceDataLookupService.IsCoatingAvailableForLensOptionAsync`
(new, DB-backed) checks it's legal: for Custom, still "any active Coating item"; for a preset
range, "one of the coatings configured as available for the chosen left-eye `LensOption`'s
`LensStrengthRefId`." A lens strength with zero configured coatings can't pass validation yet —
`LensRangeSelector.razor` shows this plainly ("No coatings are configured for this lens yet...")
rather than a dead-end empty dropdown. `CreateLeadRequestValidator`'s equivalent check
(`CoatingPreferenceRefId`) stays optional overall (a Lead can carry no product preference) but is
still validated against the available set whenever a preset lens **is** chosen.

**Custom-range fields gained real constraints** (previously only null-presence checks, confirmed
by reading both validators before this pass — a genuine gap being filled, not duplicated
validation): Sphere -10 to 10, Cylinder -6 to 0.25, Near-vision add 0 to 3 (all 0.25 increments,
both server-side in `CreateSaleRequestValidator`/`CreateLeadRequestValidator` and client-side as
generated-range `<select>`s in `LensRangeSelector.razor`), Axis 0–180 whole degrees (plain number
input — a 181-option dropdown isn't better UX), Pupil distance 54–74mm (range check already
existed; added the 1mm-increment check + a generated `<select>` client-side). Verified live by
forcing an out-of-range/off-increment Sphere value via devtools past the constrained dropdown —
correctly rejected 400 server-side, nothing landed in Postgres.

**New `IPresetCatalogueAdminService`** (Application) / `PresetCatalogueAdminService`
(Infrastructure, queries `DbContext` directly, no repository interface for these entities) —
catalogue CRUD, `AddLensOptionAsync`/`RemoveLensOptionAsync` (hard remove — no historical
Test/Lead/Sale can reference a `LensOption` that was never actually chosen on a real transaction,
so nothing needs preserving, unlike Reference Data's soft-retire), `AssignCatalogueToOrgAsync`
(idempotent no-op if already assigned), `ListAvailableCoatingsAsync`/`AddAvailableCoatingAsync`/
`RemoveAvailableCoatingAsync` for the new join table. Reuses
`AuthorizationPolicies.PresetCatalogueManage` (Admin/Manager, Country level+ — already existed, no
new policy needed) and stamps `OwningOrgNodeId` from the caller's own `ICurrentUserContext.OrgNodeId`
server-side (never client-submitted), matching every other Create-request pattern in this codebase.

**Same Razor boolean-attribute bug as Organisations, second occurrence** — `<input type="hidden"
name="Available" value="@(!available.Contains(coating.Id))" />` in the coating-availability
toggle grid. Razor renders a bare-`bool`-typed attribute expression as an HTML boolean attribute
(`value="value"` when true, omitted when false) regardless of the attribute's actual semantic
name — model binding therefore received the literal string `"value"` or nothing, never
`"True"`/`"False"`, so every toggle click silently no-opped. Fixed the same way as before:
`value="@(!available.Contains(coating.Id) ? "true" : "false")"` so Razor sees a `string`. Caught
by actually clicking a toggle live and checking the outbox/network, not by build/test — this is
now the second time this exact class of bug has bitten a plain-`<input value="@boolExpr">` in this
codebase (see Organisations above); worth treating "a `bool` C# expression is the entire value of
a non-checked/disabled/readonly HTML attribute" as a standing red flag when reviewing new Razor.

**Verified live end-to-end**: as the seeded DGI Admin, confirmed both seeded catalogues render
their correct 20-lens rosters with resolved labels (not raw GUIDs) and the coating-availability
grid shows exactly the 4 seeded bifocal→Photochromic rows checked, everything else unchecked;
toggled Clear on for `+2.50` and confirmed the DB updated. Assigned the 6-Lens Set catalogue to an
Intermediate org ("Kangemi Vision Centre"). Then signed into the Field App as the RetailPoint user
one level below that org (proving assignment cascades down the hierarchy, not just exact-match) —
confirmed the `+2.50` lens option now shows a working Coating picker with exactly the one
configured option, while an unconfigured strength (`+1.25`) shows the "not configured yet"
message instead of an empty dropdown; recorded a real preset-range Sale choosing `+2.50`/Clear and
confirmed it persisted with the correct `CoatingRefId`; recorded a real Custom-range Sale using
the new constrained dropdowns and confirmed it persisted with the correct Sphere/AddPower/Axis/PD
values. A stray manually-created "+2.50" `LensStrength` item (leftover test data from earlier
Reference Data screen verification this session, not a seeding bug) was found and retired during
this pass — worth remembering that this Postgres data volume persists real state across every
`AppHost` restart in this session, including ad hoc test rows.

## Admin Portal wiring (Custom Orders screen)

Sixth Admin Portal screen wired (2026-08-05) — only Dashboard remains placeholder. Picked next
because, unlike Dashboard's unspecified "retail-point type distribution," the domain shape was
resolvable from what already existed: `Sale.OrderFromDotGlasses` (Custom range, outlet has no
stock) was already the exact signal for "this needs fulfilment," just with nowhere to track
progress — confirmed two decisions with the user before implementing (status storage: a column on
`Sale`, not a separate entity; advance-status RBAC: same `CustomOrdersView` policy as viewing, not
a stricter one) rather than guessing either.

**New `Sale.FulfilmentStatus`** (`Domain.Enums.FulfilmentStatus?` — `Submitted`/`InLab`/
`ReadyForPickup`/`Fulfilled`) — null unless `OrderFromDotGlasses` was true at creation, in which
case `SaleService.CreateAsync` stamps it `Submitted`. Deliberately on the same row rather than a
separate entity: `Sale`'s own doc comment already said "a custom order counts as a completed Sale
immediately," and the design mockup only ever shows a flat single-status queue (no per-change
history) — a separate entity would have been solving a problem nobody asked for.

**New `ICustomOrderService`** (Application) / `CustomOrderService` (Infrastructure, queries
`DbContext` directly, no repository interface — matches `EventHistoryQueryService`) —
`ListAsync` (`Sale` rows where `FulfilmentStatus IS NOT NULL`, resolved to outlet name via the
same `HierarchyPath`-keyed lookup `EventHistoryQueryService` uses, customer name, and a formatted
`"OD {right} / OS {left}"` prescription string built from the Custom sphere/cylinder/add-power
fields) and `AdvanceStatusAsync` (linear, forward-only — throws if the Sale isn't a custom order
or is already `Fulfilled`, never lets a caller set an arbitrary status). Hierarchy scoping is
automatic (`Sale`/`Customer`/`OrganisationNode` all implement `IHierarchyScoped`), so — same
insight as Event History/Organisations — a Country-level caller's `ListAsync` only ever returns
their own subtree's custom orders with no extra filtering needed; `AdvanceStatus` reuses
`AuthorizationPolicies.CustomOrdersView` (any role, Country level+) rather than a separate write
policy, per the user's decision.

**Design mockup's copy was stale, corrected rather than copied**: `design/admin/screens-b.jsx`'s
`CustomOrders` component claims "Field users cannot submit custom orders directly; only a
retailer or outlet manager/admin can, via this portal" — no longer true since the Field App UI
wiring pass (see above) already lets any RetailPoint technician submit a Custom Sale with
`OrderFromDotGlasses` directly from `ConsultationForm.razor`. `Views/CustomOrders/Index.cshtml`'s
copy now describes that real path instead of the outdated design assumption.

**Verified live end-to-end**: as the seeded RetailPoint user, recorded a real Custom-range Sale
with "Order this lens from DOT Glasses" checked — confirmed `FulfilmentStatus` landed `Submitted`
in Postgres. As the seeded DGI Admin, confirmed it appeared in the Custom Orders queue with the
correct customer, outlet (resolved from `HierarchyPath`, not a raw GUID), and prescription string,
then advanced it through the full `Submitted → In Lab → Ready for Pickup → Fulfilled` flow — the
advance button correctly disappears once `Fulfilled`. Confirmed RBAC three ways in one pass: the
DGI Admin and the seeded Kenya Manager (Country level) both saw the page and this order (it's in
Kenya's subtree); the seeded RetailPoint user was redirected to `AccessDenied` outright, matching
`AuthorizationPolicies.CustomOrdersView`'s "DGI/Country only, hidden entirely below that" gate.

## Admin Portal wiring (Dashboard screen) — the last placeholder screen

Seventh and final Admin Portal screen wired (2026-08-05) — all seven now read/write real data.
Confirmed two scope decisions with the user before implementing, since CLAUDE.md had explicitly
flagged Dashboard's "retail-point type distribution" as an unresolved domain question (no
`RetailPointType` concept exists anywhere — `OrganisationNode.Kind` is free-text with only
"Retailer"/"Standalone" ever seeded, and the design mockup's `Physical`/`Mobile Agent`/`Outreach`
was fictional sample data never confirmed with the user): (1) drop the retail-point-type tile
entirely for v1 rather than invent a taxonomy; (2) ship the "Top performing" tables as a fixed
unfiltered top-5-by-sales list, not the mockup's live country/retailer/type filters + sales-vs-
conversion sort toggle (today's seed data is too sparse for filters to show anything meaningful
anyway). Also corrected two placeholder/mockup naming mismatches while wiring for real: "Custom
lenses" → **Custom orders** (matches the Custom Orders screen's own terminology now that it's
real) and "Top agents" → **Top technicians** (matches the mockup).

**New `IDashboardQueryService`** (Application) / `DashboardQueryService` (Infrastructure, queries
`DbContext` directly, no repository interface — matches `EventHistoryQueryService`/
`CustomOrderService`) — a single `GetAsync` computing: stat tiles (pending Leads = `!ConvertedFlag`;
custom orders = `Sale.FulfilmentStatus != null`, matching the Custom Orders screen's own
definition exactly; standard sales = everything else); test-to-sale and needed-to-sale conversion
% (walks `Test.ConvertedToLeadId` → `Lead.SaleId`, since there's no direct Test→Sale link);
referrals logged (`Test.Outcome == Referred`); a 6-bucket rolling-7-day conversion-% trend;
gender split (from `Test.Gender` — the broadest top-of-funnel population, a reasonable default
where the mockup didn't specify a source); and top-5-by-sales-volume outlet/retailer/country/
technician lists, each paired with its own conversion % (that key's own Sales ÷ that key's own
Tests). "Retailer" = nearest Intermediate-level ancestor by longest matching `HierarchyPath`
prefix — the design mockup's "retailer" tier, matching how `OrganisationSeedConfiguration` already
nests a retail point under an Intermediate node. `OrganisationNode.IsTrainingOrg` rows are
explicitly excluded from every aggregate (per that field's own doc comment: "excluded from MI
dashboards/reporting via an explicit query condition, not a global filter") — the only place in
the codebase so far that actually needed to honor this already-decided rule.

**No Dashboard-specific RBAC policy** — `HomeController` keeps its plain `[Authorize]` (any
authenticated role). This matches the RBAC permission matrix's own already-decided rule ("User: at
a Retail Point, Field App access + read-only MI for that outlet only") — a `User`-role account is
*supposed* to see Dashboard MI, just automatically scoped to their own subtree, same mechanism as
every other reporting screen.

**One real bug found and fixed, shared with Event History**: org name resolution (outlet/
retailer/country) was silently broken for any caller below Country level. Both
`DashboardQueryService` and the pre-existing `EventHistoryQueryService` built their outlet/country
lookup from a *plain scoped* `OrganisationNodes` query — but `OrganisationNode` implements
`IHierarchyScoped` too, so the standard hierarchy filter only ever returns a caller's own subtree,
never their ancestors. A RetailPoint-level caller (a leaf node with no descendants) therefore saw
*only their own single org node* from that query — their Country/Intermediate ancestors were
invisible to them, so `Country()`/`Retailer()` resolution silently fell back to "Unknown country"/
"Unknown retailer" for every row, even their own. This had never been caught because Event
History's own live verification (see its section above) was only ever done by the DGI Admin and
the Kenya Manager (Country level) — both of whom sit at or above the ancestor they were resolving,
so the bug was invisible from their vantage point. Caught this time because Dashboard's own live
verification was deliberately run as the RetailPoint user too. Fixed by extending
`IUnscopedReportQueryService` — the sanctioned way to look outside a caller's hierarchy scope, per
the Architecture rules above — with a new `GetOrganisationNodesUnscopedAsync` (a superset of the
existing path-only `GetOrganisationNodePathsUnscopedAsync`, also carrying `Name`/`Level`/
`IsTrainingOrg`) and switching both `DashboardQueryService` and `EventHistoryQueryService` to it.
Worth remembering for any future reporting service: resolving an *ancestor's* name/level always
needs the unscoped lookup, even when the entity being reported on (Sale/Test/Lead) is correctly
auto-scoped — the org-tree lookup is a second, separate scoping concern.

**Verified live**: as the seeded DGI Admin, confirmed all six stat tiles, the conversion trend
(non-zero only in the current week's bucket, correctly reflecting this session's real but
recent-only data), gender split, and all four top-N lists render real, non-fabricated numbers.
Then signed in as the seeded RetailPoint user and confirmed a strictly scoped-down view (fewer
tests/sales, no DGI-root orphan rows) — and, post-fix, correct "Kangemi Vision Centre"/"Kenya"
names in Top Retailers/Top Countries instead of "Unknown". Re-checked Event History under the same
user afterward and confirmed its Country column now also resolves correctly.

## Assign users to Organisations

Resolved the `[OPEN]` gap flagged when Organisations first shipped ("'Assign users' action isn't
built"). Reuses `UserOrgAssignment` (already real since User Directory's invite flow) and
`AuthorizationPolicies.ManageOrgInScope` (already wired to every other Organisations action) —
this is additive to both, not new plumbing.

**Deliberately reuses `ManageOrgInScope`, not `ManageUsersInScope`**: the action being gated is
"can this caller manage *this org node*," not "can this caller manage *this user*" — the target
user could be anyone in the caller's own hierarchy scope, but what's actually being changed is
the org's membership list. Keeps every Organisations action (`CreateChild`, the two flag toggles,
now `AssignUser`) authorized against the same policy/resource pair, rather than introducing a
second check with different semantics on the same screen.
`ManageUsersInScope`/`HierarchyDescendantRequirement` stay unwired, still reserved for when User
Directory gets a matching "assign to org" action from the user's side.

**`IUserAdminService.AssignUserToOrgAsync(userId, orgNodeId)`** (Infrastructure) — idempotent
no-op if the pair already exists (same precedent as `PresetCatalogueAdminService.
AssignCatalogueToOrgAsync`), never touches the user's primary org
(`OrgNodeId`/`HierarchyPath`/`OrgLevel`) — still no "switch active location" UI to make changing
which org drives a multi-org user's JWT/cookie claims meaningful (unchanged `[OPEN]` item).

**`OrganisationsIndexViewModel` gains `AssignableUsers`** (every user in the caller's own scope,
via `IUserAdminService.ListAsync()`, already prefix-filtered) **and `SelectedAssignedUserNames`**
(cross-referenced by matching `UserAdminRow.OrgNames` against the selected node's `Name` — a
name-based match, not an Id-based one, since `IUserAdminService` only exposes each user's assigned
org *names* today, not `OrgNodeId`s; acceptable since org names are unique in this dataset, but
worth revisiting if that stops being true). The "Assign users" button/modal only render when
`AssignableUsers.Count > 0`, same defensive pattern as `CanManage` hiding the other action buttons.

**Verified live**: as the seeded DGI Admin, assigned Grace Njoroge (an existing Manager) to
"Mombasa Retail Group" — confirmed a new `UserOrgAssignment` row in Postgres and the node's detail
panel listing her under "Assigned users." Re-submitted the identical assignment and confirmed no
duplicate row (the no-op guard). Then signed in as the seeded Kenya Manager: assigning her to
"Nakuru Central" (within Kenya's own subtree) succeeded normally, while attempting to assign her
to the DGI root node — outside the Kenya Manager's scope, and not even present in their own scoped
`ListAsync()` result — correctly hit `CanManageAsync`'s Forbid() (surfaced as a redirect to
`/Account/AccessDenied` under cookie auth, not a raw 403 — same behaviour every other
`ManageOrgInScope`-gated action already has).

## Event History pagination

Resolved the `[OPEN]` gap flagged when Event History first shipped ("no pagination — unlike
Reference Data/Organisations' naturally small, bounded lists, Test/Lead/Sale volume genuinely
grows over time"). `IEventHistoryQueryService`'s four list methods now take `(page, pageSize)`
and return a `PagedResult<T>` (`Items`, `TotalCount`, `Page`, `PageSize`, computed `TotalPages`);
`EventHistoryController` fixes `PageSize = 25` and passes through a `page` query-string param
(1-based, clamped to ≥1). Paging is pushed to the database via `Skip`/`Take` after `CountAsync`,
not loaded-then-sliced in memory — the point of doing this at all was to stop loading the whole
table, so an in-memory `Skip`/`Take` would have defeated it.

**Leads' search filter had to move to a DB-level subquery** — it previously ran in memory,
*after* mapping every Lead to a `LeadEventRow` (so it could match on the resolved `Customer.
FullName`). Filtering after paging would have made "page 2" mean something different depending on
how many of page 1's rows the search happened to exclude, so the filter now runs first, as a
`Customer`-scoped subquery (`dbContext.Customers.Where(c => c.FullName.Contains(searchByName))
.Select(c => c.Id)`, then `Leads.Where(l => matchingCustomerIds.Contains(l.CustomerId))`) that EF
Core translates into a single correlated SQL query — count and page both happen against the
already-filtered set.

The view shows a simple Previous/Next pager with "Page X of Y (Z total)" — no page-number jump
list, matching this session's established "simplest real thing" bar for admin-only screens with
no evidence yet of needing more. Switching tabs (the pill links) doesn't carry the `page` query
param, so it correctly resets to page 1 rather than landing on a phantom "page 3" of a different
tab's data.

**Verified live**: created 25 extra Sale rows via direct API calls (28 total, crossing the
25-per-page boundary) — confirmed the Sales tab's page 1 shows exactly 25 rows with a working
`Next` link and no `Previous` link, page 2 shows the remaining 3 with `Previous` and no `Next`.
Confirmed the Leads tab's search (`?search=Jane`) correctly narrows to just the matching rows
before any paging math applies.

## Preset-range pupil distance shorthand

Resolved a domain-shape question that had sat in `[OPEN]` since the CEO transcript first surfaced
it: the "0 to 4 PD (0 to 2 for children)" line is a coarse shorthand/frame-fit bucket used **only
for a preset range** (`SixLensSet`/`NineLensSet`) — confirmed by the user (2026-08-05), distinct
from the real 54–74mm inter-pupillary-distance field, which stays exactly what it always was and
now applies to Custom range only.

**New `Sale.PresetPupilDistanceBucket`/`Lead.PresetPupilDistanceBucket`** (nullable `int`, 0–4,
capped at 0–2 when `ChildrensFrame`) — a separate column, not an overload of the existing
`PupilDistanceMm`. Both fields are now mutually exclusive and range-checked by `LensRangeType` in
`CreateSaleRequestValidator`/`CreateLeadRequestValidator`: a preset range requires
`PresetPupilDistanceBucket` (0–4/0–2) and rejects `PupilDistanceMm`; Custom requires
`PupilDistanceMm` (54–74mm, unchanged) and rejects `PresetPupilDistanceBucket`. For a Lead's
`LensRangeType == null` case (no product preference at all), both must be empty, same as every
other lens-range field. Unlike Sale (always required for its chosen range type),
`PresetPupilDistanceBucket` on a Lead is optional even for a preset range — matches the existing
`CoatingPreferenceRefId` precedent ("a Lead can carry no product preference at all"), just
range-checked if the technician does provide one.

`LensRangeSelector.razor` shows the 0–4 (or 0–2) bucket `<select>` for a preset range instead of
the mm picker; `Model.ChildrensFrame`'s own checkbox (rendered separately, lower in the same
component) clamps a stale out-of-range bucket value back to null if toggling it drops the max
from 4 to 2 — otherwise a previously-picked "3" or "4" would linger, unselectable, in the picker.

**Verified via direct API calls, not the WASM UI** — the Field App browser session had
accumulated an extreme volume of stale `ClientLogBatch` outbox junk across this session's many
live-verification passes (thousands of rows, all pre-existing test artifacts, unrelated to this
feature), to the point that `Home.razor`'s own failed-item widget triggered a real
`OutOfMemoryException` deserializing them via JS interop on every login redirect — a genuine
browser-storage-hygiene issue worth fixing standalone, not a bug in this feature. `LensRangeSelector.razor`'s
own picker was confirmed rendering correctly (0–4 options) before the crash; the full
accept/reject matrix (valid preset bucket, out-of-range bucket, children's-frame 0–2 cap, and the
`PupilDistanceMm`-must-be-empty-for-preset rule) was verified directly against the running API
instead, matching exactly what a real client would submit.

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
  JS — originally decorative in `UserDirectory`, now driving both Organisations' "Add child
  node" dialog and User Directory's own real "Invite platform user" form) — the design system
  layers custom `dg-*` classes/tokens on top rather than replacing it.
- **All seven Admin Portal screens are now wired to the real database** (Reference Data,
  Organisations, Event History, User Directory, Preset Catalogues, Custom Orders, and Dashboard —
  all 2026-08-05, see the Admin Portal wiring sections above) — none of the `Controllers/
  *Controller.cs` files return hardcoded placeholder data anymore. `ConsultationForm.razor` in
  `App` is **no longer a stub** either — it saves real Test/Lead/Sale records via the real API
  (see Field App UI wiring above); its `Web` modal equivalent still doesn't exist (see `[OPEN]`).
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
  resource changes rather than hand-editing the Bicep. `azd` itself isn't on PATH in a fresh
  shell despite being winget-installed (`C:\Users\Joe\AppData\Local\Programs\Azure Dev CLI\
  azd.exe`) — prepend that directory to PATH rather than assuming `azd` is missing.
- **Resources follow the Azure CAF naming convention** (2026-08-06, [Cloud Adoption Framework
  resource naming](https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-
  best-practices/resource-naming) — user-specified, one subscription with two resource groups:
  `rg-dotglasses-nonprod` / `rg-dotglasses-prod`). The azd environment itself is named
  `dotglasses-nonprod`/`dotglasses-prod` (main.bicep's own `rg-${environmentName}` gives the
  resource-group name for free from that — see `deploy.yml`'s `AZURE_ENV_NAME`). Every other
  resource name is a CAF-pattern string built in `AppHost.cs` via `ConfigureInfrastructure`
  (`.PublishAsAzureContainerApp` for the "web" container app specifically) — e.g.
  `pgsql-dotglasses-nonprod-<hash>`, `ca-dotglasses-nonprod`, `cae-dotglasses-nonprod`. The env
  token (`nonprod`/`prod`) is pulled out of `resourceGroup().name` at Bicep-evaluation time
  (`EnvToken()`, stripping the fixed `rg-dotglasses-` prefix) rather than threaded down as a new
  parameter from main.bicep — works unmodified in any resource-group-scoped module with zero
  extra plumbing, and survives `azd infra gen --force` regeneration since the expression lives in
  this C# file, not generated Bicep. Globally-unique resource types (Storage, the two
  Communication Services resources) append a short `uniqueString()` hash after the CAF name —
  CAF's own examples don't solve global uniqueness either, and a plain deterministic name risks
  colliding with someone else's resource anywhere on Azure; Storage additionally uses a shorter
  `dg` workload token (not `dotglasses`) to stay under the 24-char/no-hyphen storage-account limit.
  **`BicepFunction` has no `Substring`/skip wrapper in the installed Azure.Provisioning version**
  (confirmed via reflection against the actually-resolved 1.5.0, not just the lower version
  floor some packages declare — only `Take()`, first-N-characters, exists) — `EnvToken()` builds
  the `substring(...)` call by hand via `FunctionCallExpression`, the same escape hatch
  Azure.Provisioning uses internally to implement its own `BicepFunction.*` wrappers; verified by
  regenerating `/infra` and confirming the emitted Bicep actually contains
  `substring(resourceGroup().name, 14)`, not by trusting the C# compiled.
  **Two resources deliberately keep Aspire's default name, not CAF** — the Container Registry
  (`env-acr` module) and Web's own compute identity (`web-identity` module) are each a *separate*
  Bicep module/provisioning construct with no `IResourceBuilder` exposed for either in
  `AppHost.cs`, unlike the container-apps-environment's own internal AcrPull identity (`env_mi`,
  reachable and renamed to `id-dotglasses-cae-<env>` since it's emitted into the *same*
  `env.module.bicep`). Flagged in `[OPEN]`, not silently left — worth another look if Aspire ever
  exposes a builder handle for either.
  **The Field App shares these same two resource groups** (decided with the user 2026-08-06,
  see the next section) rather than getting its own pair — the Admin Portal and Field App are two
  facets of one product, not two independent ones.
- **Blob Storage and Azure Communication Services are now real Aspire-managed resources**
  (2026-08-05), resolving the `[OPEN]` "no blob storage exists yet"/"ACS needs to be
  configurable via Aspire, or we need to flesh out the Bicep for it" gaps directly per the CEO's
  instruction. Storage uses the native `Aspire.Hosting.Azure.Storage` package
  (`builder.AddAzureStorage("storage").RunAsEmulator()` + `.AddBlobContainer("reference-data-
  images")`, `RunAsEmulator` gives local dev a real Azurite container) — the blob container itself
  is provisioned and RBAC-wired to Web's managed identity (`allowSharedKeyAccess: false`, a
  `StorageBlobDataContributor` role assignment, no connection-string secret), but nothing in the
  app actually uploads to it yet — `ReferenceDataItem.ImageUrl` is still a plain admin-pasted URL;
  wiring a real upload feature is separate application-layer work, out of scope for this pass.
  ACS has **no** Aspire hosting integration (confirmed via `dotnet package search`, official and
  CommunityToolkit) — added via `builder.AddBicepTemplate("acs", "acs.bicep")` instead
  (`src/DotGlasses.AppHost/acs.bicep`, hand-authored: a Communication Service + an Email
  Communication Service with a free Azure-managed domain, no custom-domain DNS verification
  attempted since that's an interactive portal step) so it still participates in the regenerable
  `azd infra gen` pipeline rather than becoming loose hand-maintained Bicep. **Gated behind
  `builder.ExecutionContext.IsPublishMode`** — unlike `AddAzurePostgresFlexibleServer`/
  `AddAzureStorage`, a raw `AddBicepTemplate` resource has no `RunAsContainer`/`RunAsEmulator`
  local-dev escape hatch, so without this guard plain `dotnet run` would try to actually provision
  it against a real Azure subscription on every local start and hang waiting for `az login`
  credentials that don't exist in dev — caught live (Web never started; the `DotGlasses.AppHost.exe`
  process was running but had no child Web process, and curl against `localhost:7117` just hung)
  before shipping, not by build/test. `IEmailSender` stays `LoggingEmailSender` — swapping in a
  real ACS-backed sender is still explicit `[OPEN]` work the user is doing themselves; only the
  infra needed to exist for that swap to be possible.
- **The Field App's `/infra` now exists too** (2026-08-06) — resolving the gap flagged when the
  root project's infra was first built. `azd infra gen` doesn't apply here (it's specifically the
  Aspire-manifest→Bicep synthesis path, confirmed by running it from `src/DotGlasses.App` and
  getting "this project does not contain any infrastructure to generate") — this is a plain,
  non-Aspire azd project, so `src/DotGlasses.App/infra/main.bicep`/`main.parameters.json` are
  hand-authored, mirroring the shape of the root project's own (generated) `main.bicep` closely
  enough to stay recognizable. **Shares `rg-dotglasses-nonprod`/`rg-dotglasses-prod` with the
  root project** rather than getting its own pair (decided with the user 2026-08-06: the Admin
  Portal and Field App are two facets of one product, not two independent ones) — the *only*
  thing that makes this work is both azd projects' environments being named identically
  (`dotglasses-nonprod`/`dotglasses-prod`, confirmed in both `main.bicep`'s comments and
  `deploy.yml`'s `AZURE_ENV_NAME` values); resource-group creation is a plain idempotent upsert,
  so it's safe for both projects' independent deployments to declare it, in either order or in
  parallel, matching exactly how the root project's own generated `main.bicep` already does it.
  The Static Web App resource itself
  (`src/DotGlasses.App/infra/field-app/field-app.module.bicep`) uses the public AVM registry
  module (`br/public:avm/res/web/static-site:0.3.0`) rather than a hand-rolled
  `Microsoft.Web/staticSites` resource, matching azd's own quickstart templates
  (`todo-nodejs-mongo-swa-func`) rather than guessing at the resource's property shape —
  `provider: 'Custom'` (content pushed externally via `azd deploy`'s Static Web Apps CLI
  integration, not Azure's own GitHub-repo-linked build) and the `'azd-service-name': 'field-app'`
  tag (must match `azure.yaml`'s service name exactly — confirmed via Microsoft's own azd
  troubleshooting reference, not assumed) are both load-bearing, not decorative. **Static Web
  Apps has its own, much narrower region list** (`westus2`/`centralus`/`eastus2`/`westeurope`/
  `eastasia` — confirmed via the same troubleshooting reference) than Postgres/Storage/Container
  Apps, so the module takes its *own* `location` parameter (`@allowed(...)`-constrained, default
  `westeurope`) rather than reusing the shared resource group's `location` — reusing it would
  risk an outright `LocationNotAvailableForResourceType` deployment failure the moment the shared
  region (whatever the user sets `AZURE_LOCATION` to for Postgres/Storage/Container Apps) isn't
  one of those five. CAF naming follows the same `EnvToken()`-from-`resourceGroup().name` pattern
  as the root project's `AppHost.cs`, hand-written directly in Bicep this time (`substring()` is a
  native Bicep function — only the C#-side `Azure.Provisioning` SDK wrapper lacked it) rather than
  needing the `FunctionCallExpression` escape hatch: `stapp-dotglasses-app-<env>-<hash>`.
  **Verified**: `az bicep build` compiles clean end-to-end, including resolving the AVM registry
  reference over the network — not just that the file parses. Did *not* run `azd provision
  --preview` (started to, then killed it after confirming it just prints "You must be logged into
  Azure perform this action" rather than hanging — actually provisioning, even a preview/what-if,
  calls the real ARM API against a real subscription, which is exactly the "no infra touched from
  a developer machine, only credentials Claude must not create" line this repo already draws).
- **No infra is ever deployed from a developer machine** — only via GitHub Actions
  (`.github/workflows/deploy.yml`, 2026-08-05). Auto-deploys both azd projects (root `Web`/
  AppHost and the separate `DotGlasses.App` project) to a `staging` GitHub Environment on every
  successful `App` workflow run on `main` (a `workflow_run` trigger, not a second push trigger —
  avoids re-running build/test twice per push while still only ever deploying commits that passed
  CI), then to a `production` GitHub Environment gated by a required-reviewers protection rule.
  Uses OIDC federated credentials (`azd auth login --federated-credential-provider github`, no
  stored client secret) — the exact pattern azd's own generated pipeline uses, so a later
  `azd pipeline config` run stays compatible with this file's shape rather than fighting it.
  Neither the `staging` nor `production` Azure environment exists yet (confirmed with the user
  2026-08-05) — this workflow is wired up ahead of that the same way `infra.yml` was originally
  wired up ahead of `/infra` existing, so deployment becomes automatic the moment the manual setup
  below is done, without another CI change.
  **`[OPEN]` — exact manual steps the user needs to run themselves** (needs their own Azure
  login; Claude must not run any of this):
  1. `azd auth login` once, locally.
  2. `azd pipeline config` from the repo root (root azd project — `Web`/AppHost/Postgres/Storage/
     ACS), **and again from `src/DotGlasses.App`** (the separate Field App azd project) — each
     run needs to target *both* a `staging` and a `production` azd environment/GitHub Environment
     pair; azd will prompt to create each environment (subscription, location) and will create
     matching GitHub Environments + `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID`
     federated-credential variables if they don't already exist — the exact variable names this
     workflow reads. **When prompted for the azd environment name, use exactly `dotglasses-nonprod`
     and `dotglasses-prod`, for *both* azd projects** — that's what makes the Field App's
     resources land in the same two resource groups as Web/AppHost rather than getting its own
     (see the Resource naming convention bullet above and `src/DotGlasses.App/infra/main.bicep`'s
     comment). `deploy.yml` itself already hardcodes `AZURE_ENV_NAME` per job rather than reading
     a GitHub variable, so this mainly matters for the local environment `azd pipeline config`
     creates/links while wiring up the federated credentials — keep it consistent with the
     workflow's own values regardless.
  3. In GitHub repo Settings → Environments → `production`, add a required-reviewers protection
     rule. This is the actual manual-approval gate — `environment: production` in the workflow
     YAML only *targets* that gate, it can't create the reviewer requirement itself.
  4. Set an `AZURE_LOCATION` variable on both environments (e.g. `uksouth`) — read by the
     workflow, not set by `azd pipeline config` automatically.
  5. `main.bicep`'s `postgres_password` parameter is a **pre-existing quirk, not something this
     pass introduced**: Aspire's manifest→Bicep generation always emits a top-level parameter for
     every `AddParameter(..., secret: true)` call in `AppHost.cs` (there's only the one,
     `postgres-password`, used solely by local dev's `.RunAsContainer(...)`), regardless of
     whether any actual Azure module consumes it — and none does, since the real Postgres
     Flexible Server module uses `activeDirectoryAuth: 'Enabled'` / `passwordAuth: 'Disabled'`
     (Entra ID only, confirmed by reading `postgres.module.bicep` directly). `azd provision` will
     still refuse to run without *some* value for it. Set any placeholder string as a secret azd
     environment value (`azd env set postgres_password <anything> --secret`) for both
     environments — it's genuinely never read by the deployed resource.

## Field App config per environment

Resolved 2026-08-06 — the user asked how to manage config for a static web app without keeping
secrets in `appsettings.json`. **The Field App has no genuine secret today** — `ApiBaseUrl` is
the only config value, and it's not sensitive (any user's browser network tab reveals it anyway).
The general principle still matters for later: Blazor WASM has no secure client-side storage —
anything under `wwwroot`, including a file fetched at runtime, is fully visible to any user via
devtools, regardless of delivery mechanism. A real secret (a third-party API key, say) would have
to be proxied through `Web`'s own backend, never held by the Field App directly.

**Chosen approach: build-time `appsettings.{Environment}.json` selection**, not the
deploy-time-injected-`config.json` alternative originally proposed (decided against — simpler,
standard ASP.NET Core convention, at the cost of needing a rebuild if the API's URL ever
changes). `wwwroot/appsettings.Staging.json`/`appsettings.Production.json` now exist alongside
the original `appsettings.json` (which now backs local dev only); .NET's layered configuration
loads them automatically once the app's environment is set — no `Program.cs` changes needed,
`builder.Configuration["ApiBaseUrl"]` already picks up whichever file won.

**Standalone Blazor WebAssembly has no server to send an environment header once deployed as
static files** — confirmed via the official Blazor environments doc, .NET 10/11 uses the
`<WasmApplicationEnvironmentName>` MSBuild property (the older `Blazor-Environment` HTTP-header
mechanism documented for 8.0/9.0 doesn't apply here). There's no first-class azd/azure.yaml field
to inject an MSBuild property into a `staticwebapp`-host service's build (confirmed by reading
azd's own schema reference — `env:` on a service definition is explicitly unsupported for
`staticwebapp`, only `appservice`/`containerapp`/`azure.ai.agent`) — instead, `deploy.yml` sets
`WasmApplicationEnvironmentName: Staging`/`Production` as a plain job-level environment variable,
which flows down to azd's own internal `dotnet publish` call as an inherited process environment
variable, which MSBuild then picks up automatically as a property (standard MSBuild behavior —
any environment variable becomes an available property unless something more specific overrides
it). **Verified locally, not just assumed from the docs**: ran `WasmApplicationEnvironmentName=
Staging dotnet publish` directly and grepped the published `_framework/dotnet.*.js` runtime
bundle for confirmation — found `"applicationEnvironment": "Staging"` baked in literally, proving
the env-var-to-MSBuild-property path actually works for this SDK/target, not just that the build
succeeded.

**Both new files carry a `REPLACE-AFTER-FIRST-DEPLOY` placeholder `ApiBaseUrl`** — Azure Container
Apps only assigns the real FQDN's unique domain suffix at first provision, so the real staging/
production API URLs genuinely can't be known or pre-filled before that first `azd up` for each
environment (flagged in `[OPEN]`, not guessed at).

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

- Real per-user provisioning beyond the three seeded `DevUserSeeder` accounts now exists (User
  Directory's invite flow, see Admin Portal wiring above) — but real email delivery doesn't. The
  set-password link is shown once in the Admin Portal UI for manual relay; `IEmailSender` is a
  logging-only stub (`LoggingEmailSender`, Infrastructure). **Needed before any of this is usable
  outside a dev session**: a real Azure Communication Services `IEmailSender` implementation —
  the user is setting this up themselves; the token/link mechanics don't need to change at all
  when it lands, only what `UserDirectoryController` does with the link after `InviteAsync`
  returns it.
- The Field App's `ConsultationForm.razor` is wired to the real API (see Field App UI wiring
  above); the Admin Portal's equivalent modal still doesn't exist. All seven Admin Portal screens
  are wired to real data now (see the Admin Portal wiring sections above) — nothing left to scope
  screen-by-screen. Customer is internal-only by design. There is still no "retail-point type"
  concept anywhere in the domain (Dashboard's distribution-by-type tile was deliberately dropped
  rather than guessed at — see that section above); revisit only if the user actually asks for it,
  with a real taxonomy decision, not by reverse-engineering the design mockup's fictional
  `Physical`/`Mobile Agent`/`Outreach` categories.
- **Org name/level resolution for a caller below Country level needs `IUnscopedReportQueryService`,
  not a plain `OrganisationNodes` query** (2026-08-05 fix, see the Dashboard admin wiring section
  above) — `OrganisationNode` is itself `IHierarchyScoped`, so a plain query only ever returns the
  caller's own subtree, never their ancestors. Both `DashboardQueryService` and
  `EventHistoryQueryService` now go through `GetOrganisationNodesUnscopedAsync`; watch for the
  same mistake in any future reporting service that resolves an outlet's retailer/country name.
- **~12 of the 16 seeded `LensStrength` reference items have zero configured coatings** (see the
  Preset Catalogues admin wiring section above) — only the 4 bifocal strengths ship pre-configured
  (→ Photochromic). Those ~12 non-bifocal lens strengths are genuinely unsellable on a preset
  range until DGI configures at least one coating for each via the Preset Catalogues screen; this
  is a real, visible interim gap, not a bug — the Field App correctly refuses to offer a coating
  picker for them rather than silently allowing an unconfigured sale.
- User Directory has no "switch active location" UI anywhere yet, so multi-org assignment via
  `UserOrgAssignment` (now real, see Admin Portal wiring above) only ever sets the *first*
  selected org as primary/active — a user assigned to several locations can't currently change
  which one drives their JWT/cookie claims after the fact.
- Organisations has no delete/deactivate action — the design mockup doesn't show one, so none was
  added (not an oversight). `AuthorizationPolicies.ManageUsersInScope`/
  `HierarchyDescendantRequirement` are still unwired (see the Assign users to Organisations
  section above) — reserved for a future User-Directory-side "assign to org" action.
- Organisations' `SelectedAssignedUserNames` resolves "who's assigned to this node" by matching
  `UserAdminRow.OrgNames` (strings) against the selected node's `Name`, since `IUserAdminService`
  doesn't expose `OrgNodeId`s per assignment today — correct only because org names happen to be
  unique. Revisit with a real Id-based lookup if that stops holding (e.g. two orgs sharing a
  display name).
- `OrganisationAdminService.CreateChildAsync`'s new-`HierarchyPath`-segment minting is
  read-current-max-then-increment with no locking — a small race window exists under concurrent
  creates. Acceptable for now (infrequent, admin-only action); revisit if org creation ever
  becomes high-throughput.
- Frame colour images are a plain admin-pasted URL (`ReferenceDataItem.ImageUrl`) — no real
  upload feature exists yet. The blob storage *infrastructure* to build one against now does
  (`AppHost`'s `reference-data-images` container, see the Deployment section above) — building
  the actual upload UI/API is separate application-layer work, still open.
- No Leads-list/"convert to sale" entry point exists yet — a Sale recorded via
  `ConsultationForm.razor` can never set `SourceLeadId`, so a Lead's `ConvertedFlag`/`SaleId`
  currently only ever get set via the Test→Lead→(same-session)→Sale path, never by converting an
  existing Lead found later. Needs an Event History/Leads screen action once one exists.
- Full offline (IndexedDB) caching of reference data/preset catalogues — `IReferenceDataClient`
  currently needs connectivity to load the first time each session; a technician who's never
  been online since app install can't record anything yet.
- **Browser storage hygiene**: the Field App dev-testing browser session accumulated thousands of
  stale `ClientLogBatch` outbox rows across this session's many live-verification passes, to the
  point that `Home.razor`'s failed-item widget threw a real `OutOfMemoryException` deserializing
  them via JS interop on every login redirect (see the PD-shorthand section above, where this was
  hit). This is dev-only IndexedDB bloat, not a production concern per se, but it's the same
  underlying gap as the outbox-retry/discard `[OPEN]` item below — a technician's device could
  plausibly accumulate enough failed items over real field use to hit the same wall. Worth
  addressing alongside that item, not on its own.
- `LensRangeType.SixLensSet`/`NineLensSet` matching a specific `PresetCatalogueId` by catalogue
  **name** (see Field App UI wiring above) — fine while only two catalogues exist, needs an
  explicit "kind" field on `PresetCatalogue` if DGI/Country ever create more.
- Offline sync conflict resolution (currently last-write-wins; don't hard-code away a future
  version/ETag column).
- A permanently-failed outbox item (see Offline sync above) has no in-app retry-after-edit or
  discard action yet — the technician sees it flagged on the home screen but can't currently fix
  the bad field and resubmit, or dismiss it, from the Field App itself.
- Azure Monitor/Application Insights exporter connection string.
- `azd pipeline config` not run yet (needs to run twice — once per azd project — plus a
  `production` GitHub Environment reviewer rule and a placeholder `postgres_password` env value
  on both environments) — see the numbered manual-steps list in the Deployment section above.
- The Container Registry (`env-acr`) and Web's own compute identity (`web-identity`) still get
  Aspire's default `envacr<hash>`/`web_identity-<hash>` names rather than the CAF pattern every
  other resource now follows — see the Resource naming convention bullet above for why (no
  `IResourceBuilder` exposed for either in `AppHost.cs`).
- UI skeleton screens are static placeholder data, not wired to a database — see UI / design
  system section above. The Consultation Form is still missing the lead-match confirm popup, the
  "use test result" carry-over from Test to Sale, and progressive disclosure for catalogues with
  >10 items — all present in the design mockups but not built (deliberately deferred, not an
  oversight — see Field App UI wiring above).
- Field App's `wwwroot/appsettings.json` `ApiBaseUrl` still points at `Web`'s local dev HTTPS
  port (unaffected — that file backs local dev only now, see the Field App config-per-environment
  section above). `appsettings.Staging.json`/`appsettings.Production.json` both carry a
  `REPLACE-AFTER-FIRST-DEPLOY` placeholder `ApiBaseUrl` — Azure Container Apps only assigns the
  real FQDN's unique suffix at first provision, it can't be predicted from the CAF resource name
  ahead of time. Update both placeholders to the real Container App URLs right after the first
  `azd up` for each environment.
- One reference-data seeding assumption not explicitly discussed on the call — see Domain
  modelling above (FrameColour's "Other" row).

This file should grow as real architectural decisions get made — propose updates here when a
significant decision is agreed, not as a one-time artifact.
