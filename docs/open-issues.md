# DOT Glasses — Open Issues

Tracked, non-urgent gaps and deliberate simplifications. This is the list to check before saying
"is X built yet?" or before starting work that might silently depend on one of these.

Screen-level "what's not built" detail lives in
[`functional-capabilities.md`](functional-capabilities.md)'s per-screen sections — this file is
for things that need follow-up *action*, not a full inventory of every absent button. Each item
notes why it's open (blocked on the user, deliberately deferred, or a known accepted risk) so it
doesn't need re-investigating from scratch.

**Current state of play (2026-09-04).** All 8 phases of the 2026-08-09 roadmap are shipped, and the
2026-08-10 admin/app feedback round is closed — 20 of its 22 tickets delivered, the last two dropped
as out of MVP-handover scope. Nothing below is "next up," it's the residue.

What *is* next up lives in the tracker, not here: `.scratch/architecture-hardening-2026-09-04/`
holds a spec and 17 tickets covering the pre-handover architecture work (shared consultation rules,
reference-data snapshot, domain rejection seam, hierarchy path type, and the test coverage those
depend on). Its decisions are recorded in ADRs 0002–0004.

---

## Blocked on the user (needs a real Azure subscription / manual step)

These can't be done from a coding session — see CLAUDE.md's "no infra deployed from a developer
machine" rule.

- **Duplicate organisation paths**: before 2026-09-26, creating an org could re-mint a
  deactivated org's `HierarchyPath` segment, putting two orgs on one path (found live as two
  orgs on `/1/2/`). The `EnforceUniqueOrganisationPaths` migration refuses to apply to a database
  that still holds duplicates, and names them. Repair by hand before deploying it there: keep the
  older node on its path, move the newer node's subtree to fresh segments, and rewrite the
  stamped copies (`AspNetUsers`, `Tests`, `Leads`, `Sales`, `Customers`) in the same
  transaction. Rows stamped with a shared path are ambiguous, and deciding which org each belongs
  to is a judgement call. Affected users must sign in again afterwards, because their token still
  carries the old path. Diagnostic SQL and the full outline are in
  `.scratch/triage-2026-09-26/issues/02-repair-duplicate-org-paths-in-affected-environment.md`.

- **Key Vault secrets**: `Jwt--Key`, `Jwt--Issuer`, `Jwt--Audience` must be set in the real Key
  Vault (`az keyvault secret set` or the portal) after `azd up` provisions staging/production —
  the app reads them via `AddAzureKeyVaultSecrets`, but nothing sets them yet.
- **Prod Postgres grant for the CI migration identity**: nonprod's "Apply database migrations"
  step now authenticates successfully as `msi-dot-glasses` (granted via `az postgres
  flexible-server microsoft-entra-admin create` against `rg-dotglasses-nonprod`'s server,
  2026-09-06 — see CLAUDE.md's Deployment section for what that identity is and why it's separate
  from `web_identity-*`). The equivalent grant against `rg-dotglasses-prod`'s server hasn't been
  done yet, so production's migration step will fail with the same `28P01` until it is.
- **Field App API URL placeholders — resolved 2026-09-09**: `appsettings.Staging.json`/
  `appsettings.Production.json` now point `ApiBaseUrl` at the Admin Portal's real custom domains
  (`nonprod.admin.dotglasses.com` / `admin.dotglasses.com`) instead of the
  `...REPLACE-AFTER-FIRST-DEPLOY...` `azurecontainerapps.io` placeholders — no longer blocked on
  waiting for Container Apps to assign an FQDN suffix, since a stable custom domain exists
  regardless. `Program.cs`'s CORS policy was extended to allow both origins alongside the
  existing local-dev ones.
- **Prod's `admin.dotglasses.com` DNS**: `AppHost.cs` now declares the Admin Portal's custom
  domain + managed certificate for both environments (see CLAUDE.md's Deployment section), but a
  managed certificate can't complete domain-control validation without the domain's DNS (a CNAME
  to the Container App's default FQDN) already in place. nonprod's `nonprod.admin.dotglasses.com`
  already had this from the earlier manual portal setup, so its re-declaration should just work.
  Prod's `admin.dotglasses.com` has never been configured — its DNS needs to exist *before* the
  next `azd up` against `rg-dotglasses-prod` runs, or certificate provisioning may fail and take
  that deploy down with it. Get the Container App's default FQDN first (`az containerapp show
  --name ca-dotglasses-prod --resource-group rg-dotglasses-prod --query
  properties.configuration.ingress.fqdn`) and point the CNAME at that.
- **Field App custom domain DNS — neither environment configured yet**: as of 2026-09-09,
  `field-app.module.bicep` declares `nonprod.app.dotglasses.com` (nonprod) /
  `app.dotglasses.com` (prod) as the Static Web App's custom domain, using the AVM module's
  `customDomains` param (`cname-delegation` validation, since both are subdomains). Unlike
  `admin.dotglasses.com`, **neither of these domains has ever been configured before** — there is
  no prior manual-portal setup to fall back on for nonprod this time. The CNAME (pointed at the
  Static Web App's default `<name>.azurestaticapps.net` hostname — `az staticwebapp show --name
  <name> --resource-group rg-dotglasses-<env> --query defaultHostname`, or read
  `FIELD_APP_URI` from the `field-app` azd project's own outputs after a first plain deploy)
  needs to exist *before* the next `azd up` against **either** `rg-dotglasses-nonprod` or
  `rg-dotglasses-prod` for the `field-app` azd project runs, or the custom-domain resource's
  validation will fail and may take that deploy down with it.
- **ACS custom domain**: `acs.bicep` still provisions the free Azure Managed Domain, not a real
  verified `dotglasses.com`. When that changes, prod and non-prod need **separate subdomains**
  (`prod.dotglasses.com` / `nonprod.dotglasses.com`) — a verified custom domain can only link to
  one Email Communication Service resource at a time, so sharing a root domain across both
  environments isn't an option.
- **Two resources keep Aspire's default (non-CAF) names**: the Container Registry (`env-acr`
  module) and Web's own managed identity (`web-identity` module) — neither has an
  `IResourceBuilder` exposed in `AppHost.cs` to rename via `ConfigureInfrastructure`. Revisit if
  Aspire ever exposes a builder handle for either.
- **Azure Monitor / Application Insights** exporter connection string isn't configured anywhere.
- **A09's Field App manual browser checklist is still unverified by a human.** The location
  picker, the auto-pick-when-one-eligible cases, the "can't record here" screen, the header and
  "Recording at" copy, the Failed-records message after a lost assignment, and the
  switching-stays-blocked-with-unsent-records guard were all implemented and reasoned through by
  code reading, but running the Field App needs the AppHost, Docker and seeded dev secrets, which
  no coding session has attempted. The checklist itself lives in ticket 09's own `## Comments`
  (`.scratch/multi-org-access-and-retail-points/issues/09-field-app-remembers-shows-and-picks-the-location.md`).
- **The Field App's lens section and coating selector are unverified in a browser.** The lens
  choice ("Same lens for both eyes", the right eye limited to the left's lens type, the power line,
  the Custom dropdowns with the axis only under a cylinder, lens type and coating preference as
  radios) and the coating selector (offered coatings following both lenses, locked pairings, the
  removal note, seeding from a Lead or a Failed record) build and follow the shared Rules helpers
  that the tests cover, but the Blazor UI has no test project and no coding session has run it.
  Each ticket's `## Comments` holds a manual checklist
  (`.scratch/lens-power-and-lens-sets/issues/09-field-app-lens-choice.md`, `…/10-field-app-coating-choices.md`).
- **Before go-live: production has no active lens sets until DGI builds them.** The lens-set
  redesign retired every earlier set and created none, so the Field App offers only *Custom
  prescription* at every outlet until an admin builds real lens sets on Lens Sets and assigns them.
  DGI should also review the Coatings & tints list against the online shop's coatings, since each
  lens is ticked for coatings from that list.

## Deliberately deferred (not started, not forgotten)

- **No upload feature for reference-data images.** `ReferenceDataItem.ImageUrl` is a plain
  admin-pasted URL (Frame colours only). The blob storage *infrastructure* to build a real upload
  against already exists (`AppHost`'s `reference-data-images` container, RBAC-wired to Web's
  identity) — building the actual upload UI/API is separate, unstarted application-layer work.
- **No frame-coverage question anywhere.** `Sale.FrameCoverage` is kept on the record but is not
  editable from any screen — the Field App's dropdown was removed at the reviewer's explicit
  request (commit `3fdf9be`, it was reading as "you're only selling eye frames"), and the Admin
  Portal's Lead→Sale form drops its own copy to match, so the two write paths stop disagreeing
  about whether a technician gets asked. Every Sale is therefore `FullFrame`. The column stays
  because removing it is a migration against real data for no benefit; if the question ever comes
  back, it comes back in both places at once.
- **No customer-facing surface.** `Customer` is internal-only — matched by exact name+phone within
  an outlet, never listed, searched, edited, or merged. A phone number typed with and without a
  country code silently becomes two customer records.
- **No Admin-Portal-side consultation form.** Recording a Test/Lead/Sale from scratch is
  Field-App-only; the Admin Portal's only write path onto Test/Lead/Sale is the narrower Lead→Sale
  conversion screen (Phase 4).
- **The Consultation Form is missing**, per the original design mockups: the "use test result"
  Test→Sale carry-over (a technician converting a Test directly into a Sale, skipping Lead), and
  progressive disclosure for a lens set with >10 lenses (moot today — the example lens sets have
  ≤12 and lenses are chosen from a dropdown).

## Known accepted risk (won't fix unless it becomes a real problem)

- **Offline records are attributed to whoever is signed in when they *sync*, not when they were
  created.** `TechnicianUserId`/`HierarchyPath` come from the JWT presented on the POST, not from
  creation time. Client-side mitigation ships (sign-out and location-switch are both blocked while
  the outbox is non-empty), but a token expiring mid-queue still reaches the same bad outcome. The
  real fix is server-side — a signed creation-context token minted at form-open time — and isn't
  done; the request DTOs deliberately omit technician/hierarchy fields so "just accept them from
  the body" is not a safe shortcut.
- **Location switching only works online.** `POST switch-org` re-issues a JWT, which is inherently
  a server round trip — there's no such thing as an offline-issued, server-verifiable JWT. Settings
  and the outlet picker show the same generic "check your connection" message for *any* failure,
  not one specific to being offline. Not fixable client-side; a clearer message is the honest fix,
  not yet done.
- **Offline sync conflict resolution is last-write-wins** (idempotent upsert keyed on the
  client-generated GUID) — no version/ETag column exists. Don't build anything that assumes
  ordering or conflict detection until this is addressed.
- **The Field App's leads client swallows every exception and logs nothing.** All three of its
  lookups — the worklist, the Lead prefill, and the "convert this instead?" match probe — catch
  broadly and return null or an empty list. Failing soft is right for the offline case, but it means
  a deserialisation bug, an expired token and a flat battery are indistinguishable, to the technician
  and to us. Surfaced by the 2026-09-04 architecture review and deliberately not ticketed for the
  handover programme; the honest fix is to log the failure and distinguish offline from broken.
- **No correction path for Tests/Leads/Sales, anywhere, for anyone — including admins.** They stay
  create-once atomic events by design. A mistyped phone number or wrong frame colour is permanent.
  This is a deliberate product constraint, not an oversight; don't build an edit path without
  re-confirming with the user first.
- **The Admin Portal's per-request access recheck (ADR-0006) also runs on static-asset requests**
  (CSS, JS, images served under the authenticated layout) — each one costs the same small
  assignments/role/lockout query as a page request, since the cookie's `OnValidatePrincipal` event
  doesn't distinguish an asset request from a navigation. Correct (an asset request still shouldn't
  outlive a suspension), just more querying than strictly needed. Revisit only if it shows up as a
  real load problem.
- **Two admins concurrently removing the same user's last two assignments could leave them with
  none**, despite the "at least one assignment" rule — the refusal check and the removal aren't
  locked against each other, so two racing requests can each see one assignment left (of two) and
  both proceed. Accepted as unlikely (ticket 03); revisit if it's ever seen in practice.

## Real, visible interim gaps (the system tells the user, doesn't hide it)

- **`FrameColour`'s seeded "Other" row** is an assumption made while seeding reference data, not
  explicitly confirmed against real DGI usage — the original call named exactly 6 fixed colours. The
  reporter revisited this list on 2026-09-03 (ticket 11 — supplied a product image per colour, and
  renamed two of them) and left "Other" in place without comment, which is weak evidence rather than
  confirmation. Still worth an explicit yes/no next time the Reference Data screen is reviewed.
- **A pairing that runs both ways on one lens (A → B and B → A) is allowed by the Add lens
  dialog**, but neither the Field App nor the Admin Portal's lead conversion ever locks either
  coating (`PairedCoatings`) — they'd hold each other ticked forever — so a user who unticks one is
  told by the server rule what is missing. Harmless; if it ever confuses anyone, the honest fix is
  for the dialog to refuse it.
- **Two pairings on one trigger whose paired coatings exclude each other both pass the Add lens
  dialog**, because exclusions are checked against each pairing on its own — so "A → B" and
  "A → C" with B and C excluding each other are both accepted, and A can then never be sold (any
  set holding A needs both B and C). Nothing is lost — the Field App offers A and the server says
  why it's refused — but the dialog should refuse the second pairing.

## Code-level debts (behaviour is right; the shape isn't)

- **Each eye's lens power travels as loose positional parameters** — `sphereLeft, cylinderLeft,
  axisLeft, addLeft, sphereRight, …` through `ConsultationRules`, `LensSetLenses.SameLens`,
  `SaleAnswers.WithLens` and the three services (a Data Clump). `LensPairPowers` already names the
  shape; passing it (or a per-eye value) instead would make a swapped argument a compile error
  rather than a silent bug. Not done in the lens power work to keep the change reviewable.
- **The Lens powers page's validity rules are hand-written prose**, not read from
  `LensPowerRules`. The value lists on the same page come from `LensPowerValues`, so a changed
  bound shows up there, but a changed rule (say, axis no longer required with a cylinder) would
  leave the prose stale until someone edits the view.

---

## Adding to this file

When a phase or fix resolves one of these, delete the bullet rather than marking it done —
`git log`/the PR history is the record of what changed and when. When new work surfaces a genuine
gap worth tracking (not a screen-level "not built yet" fact, which belongs in
`functional-capabilities.md`), add it here under whichever heading fits, with a one-line reason it
is not already fixed.
