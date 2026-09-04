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

- **Key Vault secrets**: `Jwt--Key`, `Jwt--Issuer`, `Jwt--Audience` must be set in the real Key
  Vault (`az keyvault secret set` or the portal) after `azd up` provisions staging/production —
  the app reads them via `AddAzureKeyVaultSecrets`, but nothing sets them yet.
- **`AZURE_POSTGRES_AAD_USERNAME`**: a repo/environment variable the CI migration step
  (`deploy.yml`) needs, matching whatever identity `az postgres flexible-server ad-admin create`
  was granted for the deploying principal. That grant itself is a one-time infra/RBAC step. Until
  it's done, the "Apply database migrations" CI step is untested against a real server.
- **Field App API URL placeholders**: `appsettings.Staging.json`/`appsettings.Production.json`
  both carry `ApiBaseUrl: ...REPLACE-AFTER-FIRST-DEPLOY...` — Azure Container Apps only assigns
  the real FQDN's unique suffix at first provision, so this can't be pre-filled. Update both right
  after each environment's first `azd up`.
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
  progressive disclosure for a catalogue with >10 lens options (moot today — both seeded catalogues
  have ≤12).

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
- **`OrganisationAdminService.CreateChildAsync`'s path-segment minting** is read-current-max-then-
  increment with no locking — a small race window exists under concurrent org creation. Accepted
  for an infrequent, admin-only action; would need a real sequence/lock if org creation ever became
  high-throughput.
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

## Real, visible interim gaps (the system tells the user, doesn't hide it)

- **12 of the 16 seeded `LensStrength` reference items have zero configured coatings** (only the 4
  bifocal ones ship pre-configured, → Photochromic). Those ~12 non-bifocal strengths are genuinely
  unsellable on a preset range until DGI configures at least one coating for each, via Preset
  Catalogues' coating-availability grid. The Field App correctly shows "no coatings configured"
  rather than an empty dropdown — this is expected admin follow-up work, not a bug.
- **`FrameColour`'s seeded "Other" row** is an assumption made while seeding reference data, not
  explicitly confirmed against real DGI usage — the original call named exactly 6 fixed colours. The
  reporter revisited this list on 2026-09-03 (ticket 11 — supplied a product image per colour, and
  renamed two of them) and left "Other" in place without comment, which is weak evidence rather than
  confirmation. Still worth an explicit yes/no next time the Reference Data screen is reviewed.
- **`ReferenceDataCategory.LensStrength` exists only as a curated label list.** `PresetCatalogue`/
  `LensOption` build from it as "which items, in what order" — nothing deeper (e.g. a catalogue
  picking N strengths with a per-strength coating override baked into the catalogue itself, rather
  than the separate `LensStrengthCoatingOption` join table). Revisit only if a real design need
  surfaces; don't guess at a richer model speculatively.

---

## Adding to this file

When a phase or fix resolves one of these, delete the bullet rather than marking it done —
`git log`/the PR history is the record of what changed and when. When new work surfaces a genuine
gap worth tracking (not a screen-level "not built yet" fact, which belongs in
`functional-capabilities.md`), add it here under whichever heading fits, with a one-line reason it
is not already fixed.
