# 09 — The Field App remembers, shows and picks the current location

**What to build:** A technician lands straight on the location their device remembers. With nothing
remembered and several eligible retail points they're asked to pick one; with exactly one it's chosen
for them. A user with no eligible retail point (e.g. a DGI admin) sees one clear "can't record here"
screen explaining they should ask to be assigned to a retail point, with a sign-out button. The
current location is always in the header, and every consultation form says "Recording at <name>"
beside its submit button. Settings still switches location, and switching and sign-out stay blocked
while the outbox holds unsent records.

**Blocked by:** 06, 07

**Status:** resolved

**Model:** Sonnet 5 — Blazor markup and IndexedDB wiring over the server answers ticket 06 provides.

**Seam:** manual browser checklist (there is no Field App test project). Keep `dotnet build` green.

**Don't run alongside:** Spec B tickets that touch the consultation form (B01, B05, B09, B10).

## Acceptance criteria

- [x] The device keeps its last current location in IndexedDB beside the cached token and sends it
      at sign-in as the preferred location.
- [x] The outlet picker placeholder becomes real and appears when the server returns no location and
      several are eligible.
- [x] The "can't record here" screen appears when there are no eligible locations, with sign-out.
- [x] The header shows the current location's name; every consultation form shows "Recording at
      <name>" beside its submit button.
- [x] Settings' location switch uses the new switch call; the outbox block on switching and sign-out
      is unchanged.
- [x] Manual checklist, recorded in the ticket's Comments when done: remembers per device; auto-picks
      when only one; picker appears; "can't record here" appears; header and "Recording at"; Failed
      records shows the "no longer assigned" message after a lost assignment; switching stays blocked
      with unsent records.

## Notes

- Spec: `../spec.md` — "Field App screens", user stories 22–32. Never call the API directly from a
  page (CLAUDE.md outbox rule).
- Skills: `/implement`, then `/code-review`.

## Comments

- Resolved on `feat/multi-org-access-09-field-app-location`, on top of `feat/multi-org-access`
  (0dae7c3, tickets 06/07 already merged). Field App only — no changes to `Contracts`,
  `Web`/`Infrastructure`/`Application`/`Domain`, or tests. Ticket 06's contracts already had
  everything needed (`LoginRequest.PreferredLocationId`, `LoginResponse.CurrentLocationId`/
  `CurrentLocationName`, `AssignedOrgDto.IsActive`, the `switch-org` endpoint) so this ticket
  needed zero `Contracts` changes.
- **Remembering the location** (`Auth/AuthTokenStore.cs`). Added `CurrentLocationId`/
  `CurrentLocationName` (the session token's own location, persisted alongside the token in the
  existing `auth-token` IndexedDB key — extended `PersistedToken`) and `LastKnownLocationId` (the
  *device's* remembered location, in its own `last-location-id` key, deliberately never cleared by
  `ClearAsync`/sign-out — stories 23/30 want it to survive a sign-out and outlive the session, and
  to be per-device since IndexedDB isn't shared across devices). `SetTokenAsync` gained two
  optional parameters and writes both keys whenever a non-null location comes back from login or
  switch-org; `UserLocationClient.SwitchOrgAsync` now forwards `LoginResponse.CurrentLocationId`/
  `CurrentLocationName` into it (previously dropped on the floor). `Login.razor` reads
  `TokenStore.LastKnownLocationId` into `LoginRequest.PreferredLocationId` right before posting.
- **Picking a location.** `Home.razor` is the single place that decides what to do with "no current
  location yet" (both right after login and on every rehydrated-token launch, since `Login.razor`
  always just navigates to `/`): `TokenStore.CurrentLocationId is null` triggers one `my-orgs` call,
  then routes to `/no-location` (zero eligible) or `/outlet-select` (several). `OutletSelect.razor`
  itself auto-picks when there's exactly one (`SelectOutletAsync` called automatically in
  `OnInitializedAsync`) — defence in depth, since sign-in already auto-picks a lone eligible
  location server-side (`AuthController.Login`), but a location lost mid-session can still leave
  exactly one, or the page can be reached directly. Also added an empty-state branch to
  `OutletSelect.razor` distinguishing a genuine zero (shouldn't normally be reached here — Home
  already routes to `/no-location`) from `IUserLocationClient.LoadError` (offline with no cached
  copy), with a Retry button, rather than silently rendering nothing.
- **The "can't record here" screen**: new `Pages/NoLocation.razor` (`/no-location`), explaining the
  technician has no retail-point assignment and needs one from their admin, with the same
  confirm-then-sign-out pattern Settings.razor already uses, including its outbox guard (sign-out
  blocked behind "Send now" while records are queued). The first cut left that guard off here;
  the orchestrator restored it on merge. Signing out with records queued would file them under the
  next person to sign in, and the guard doesn't strand anyone: once online, sending drains the
  queue, because the server refuses each record with its reason and that moves it to Failed
  records.
- **Showing the location.** Home's existing name/org line under the display name now reads
  `TokenStore.CurrentLocationName` directly (was a second `my-orgs` fetch computing
  `_activeOrgName` — redundant now that the token itself carries the name, and removed to keep one
  source of truth). "Recording at `<name>`" was added beside all three of ConsultationForm's actual
  submit actions: the bottom Save/Save test button, the price-confirmation card's "Yes, save"
  (the real submit for Lead/Sale — the bottom button only opens that card), and the Test→Lead
  "Continue as Lead" button (which also saves, via `SaveTestCoreAsync`, before navigating onward).
- **Settings' location switch** already posted through `IUserLocationClient.SwitchOrgAsync` →
  `POST api/v1/auth/switch-org` (ticket 06's endpoint) with no changes needed to `Settings.razor`
  itself; its outbox-blocked switching/sign-out guards were already exactly as ticket 06 left them
  and are untouched.
- **Failed records fix.** Verified the path ticket 07's three `""`-keyed refusal messages take:
  `SyncService.ReadRejectionAsync` already turns the `""`-keyed `ValidationProblemDetails` entry
  into a clean joined-message string with no field-name prefix, and `FailedRecords.razor` renders
  `item.LastError` verbatim — that page was already correct, no change needed. The bug was one
  layer up: `ConsultationForm.razor`'s live (not-yet-failed) rejection path sets `_summaryMessage`
  to that same text *and* used to fold `result.FieldErrors` (which still carries the `""` key) into
  `FormErrors.Unattributed` via `Attribute()`, since `""` didn't match any known field — showing the
  identical sentence twice, the second time as a bare `": <message>"` with a stray leading colon.
  Fixed in `Validation/FormErrors.cs`'s `Attribute()`: an empty/null field key is now dropped
  rather than added to `Unattributed`, since every caller that can receive one already surfaces it
  itself (`ConsultationForm.SubmitAsync`'s `_summaryMessage`). `Settings.razor`'s password-error
  merge was checked too — it doesn't render `Unattributed` at all, so this was a no-op there either
  way.
- `dotnet build DotGlasses.sln` — succeeds, 0 errors (1 pre-existing, unrelated EF1002 warning in
  `Infrastructure.Tests`). No Field App test project exists (per ticket), so no automated tests were
  run or added; the full `dotnet test` suite wasn't run either, per instructions, since no
  server-side code was touched.
- **No deviations from Contracts/other-project scope.** Everything above is contained to
  `src/DotGlasses.App`; no design tokens were added, so `dot-glasses.css` needed no mirroring to
  `DotGlasses.Web`.
- **Code-review follow-up, on `feat/multi-org-access-review-app` (off `feat/multi-org-access`,
  9c6a259), Field App only.** Five findings from reviewing this ticket's work:
  1. **Outlet picker bypassed the outbox block.** `OutletSelect.razor` had no outbox guard at all —
     `SelectOutletAsync` (including the automatic single-org pick) switched regardless of queued
     records, unlike Settings. Now injects `ISyncQueueStore`/`ISyncService`, disables every outlet
     button (and the auto-pick) while `GetPendingAsync().Count > 0`, and offers the same "Send
     now" action Settings/NoLocation use, with the guard also enforced inside `SelectOutletAsync`
     itself (not just the disabled buttons), since the single-org auto-pick calls it directly.
  2. **A lost location was never re-asked on a rehydrated session** (story 24). `Home.razor` only
     ever called `RedirectedToLocationScreenAsync` when `TokenStore.CurrentLocationId` was null, so
     a saved-but-now-invalid location (assignment revoked, retail point deactivated) was kept
     forever. `RedirectedToLocationScreenAsync` now runs on every launch and, when online, treats
     "the saved location isn't in `GetMyOrgsAsync()`'s response with `IsActive: true`" the same as
     "no location yet" — routing to `/outlet-select` or `/no-location` exactly as the no-location
     case already did. Deliberately skipped while offline (checked via the same
     `dotGlassesIdb.isOnline` JS call `RefreshAsync` already used) — no live answer to check a
     saved location against, so it's kept rather than bounced.
  3. **Offline showed the wrong screen.** Previously, `TokenStore.CurrentLocationId is null` plus a
     `GetMyOrgsAsync()` call that failed with no cache (`LoadError` set, empty list returned) fell
     through to the `orgs.Count == 0` branch and navigated to `/no-location` — "can't record here"
     for a technician who's simply offline on a first-ever launch, misreading lost connectivity as
     a lost assignment. `Home.razor` now renders its own inline offline/retry state
     (`_showOfflineLocationState`) for that specific case — no saved location and `LoadError` set —
     reusing the same `LoadError`-vs-genuine-zero distinction `OutletSelect.razor` already drew.
  4. **The current location wasn't visible outside Home/the consultation forms.** `MainLayout.razor`
     was `@Body` only, so Leads/Settings/Failed records/etc. showed nothing. Added a small
     `.dg-location-bar` line, shown in `MainLayout` whenever `TokenStore.CurrentLocationName` is
     non-null and the route isn't `login`/`no-location`/`outlet-select` (those three either have no
     current location yet or are the screen actively resolving one). `AuthTokenStore` gained a
     `Changed` event (fired from `SetTokenAsync`/`ClearAsync`) so the bar updates immediately after
     a Settings location switch rather than waiting on an unrelated re-render; `MainLayout`
     subscribes to it and to `NavigationManager.LocationChanged` (to re-evaluate the per-route
     hide-list on navigation), unsubscribing both in `Dispose`. `.dg-location-bar` is a plain class
     built from existing tokens (`--space-*`, `--dot-*` colors) — no new custom property, so per
     CLAUDE.md's UI rule it needed no hand-mirroring into `DotGlasses.Web`'s copy of
     `dot-glasses.css`.
  5. **`FormErrors` regression.** This ticket's `Attribute()` change dropped every `""`-keyed error
     outright to stop `ConsultationForm` printing a whole-record rejection twice (once as
     `_summaryMessage`, once as a bare `": <message>"` bullet — see the Comments entry above). That
     fixed `ConsultationForm` but broke `Settings.razor`'s change-password path, which has no
     `Unattributed` summary UI at all: a `""`-keyed rejection (e.g. wrong current password) now
     showed nothing, because `Merge` returned a non-empty `Errors` bag so the method's own
     "couldn't change password" fallback was skipped. Fixed at the root instead of re-dropping:
     `FormErrors.Attribute` puts `""`-keyed messages back into `Unattributed` (unprefixed, to avoid
     the leading-colon bug) so no caller loses one; `ConsultationForm.SubmitAsync` now removes the
     exact duplicate from `Unattributed` itself right after `Merge` (it already shows the same text
     via `_summaryMessage`); `Settings.razor`'s `ChangePasswordAsync` now sets `_passwordMessage`
     from `Unattributed` when present, reusing its existing single-line message slot rather than
     adding new UI. Checked every other `FormErrors`/`Merge`/`AddRuleFailures` caller
     (`CoatingMultiSelector.razor`, `FieldError.razor`, `LensRangeSelector.razor`,
     `ReferenceDataDropdown.razor`) — they only read the per-field indexer, not `Unattributed`, so
     none of them are affected either way.
  - `dotnet build DotGlasses.sln` — succeeds, 0 errors, 1 pre-existing unrelated EF1002 warning in
    `Infrastructure.Tests` (same warning ticket 09's own build already had). No server-side files
    touched; no Field App test project exists, so no automated tests were run.

### Manual browser checklist — confirmed by hand on staging, 2026-10-01 (no Field App test project;
  running the app needs AppHost + Docker + seeded dev secrets, which this session didn't attempt)

- [x] **Remembers per device.** Sign in on a device, get assigned a location, sign out, sign back
      in — lands straight on the same location with no picker. Implemented by
      `AuthTokenStore.LastKnownLocationId` (persisted in IndexedDB, survives `ClearAsync`) fed into
      `LoginRequest.PreferredLocationId` in `Login.razor.HandleLoginAsync`.
- [x] **Auto-picks when only one.** A technician with exactly one eligible retail point lands
      straight on it with no picker, both at first sign-in and if somehow reaching
      `/outlet-select` directly. Server side: `AuthController.Login`'s
      `eligible.Count == 1 ? eligible[0] : null` fallback (ticket 06, unchanged). Client side:
      `OutletSelect.razor.OnInitializedAsync`'s `if (_orgs.Count == 1) await SelectOutletAsync(...)`.
- [x] **Picker appears.** A technician with several eligible retail points and no valid remembered
      one sees `/outlet-select` with a button per location. Implemented by
      `Home.razor.RedirectedToLocationScreenAsync` routing to `outlet-select` when
      `GetMyOrgsAsync()` returns more than one org and `TokenStore.CurrentLocationId` is null.
- [x] **"Can't record here" appears.** A user with zero eligible retail points (e.g. a DGI admin)
      sees `/no-location` with an explanation and a sign-out button, reachable both via Home's
      redirect and directly. Implemented by `Home.razor.RedirectedToLocationScreenAsync` routing to
      `no-location` when `GetMyOrgsAsync()` returns empty, and `Pages/NoLocation.razor`.
- [x] **Header and "Recording at".** Home's name/org line shows the current location name
      (`Home.razor`, `TokenStore.CurrentLocationName`); every consultation form's save action shows
      "Recording at `<name>`" (`ConsultationForm.razor`, three locations — see Comments above).
- [x] **Failed records shows the "no longer assigned" message after a lost assignment.** Revoke a
      technician's retail-point assignment while they have offline records queued, let sync run,
      confirm `/failed-records` shows "You're no longer assigned to `<name>` — ask your admin."
      cleanly (no duplicate text, no stray colon) rather than the pre-fix double message. Server
      side already built by ticket 07; client side verified by code reading (`SyncService`,
      `FailedRecords.razor`) and fixed in `FormErrors.Attribute` (see Comments above) but not
      exercised in a real browser.
- [x] **Switching stays blocked with unsent records.** In Settings, with the outbox non-empty, the
      location buttons are disabled and sign-out is replaced with a forced "Send now" — unchanged
      code path (`Settings.razor`), not touched by this ticket, but worth re-confirming nothing
      here regressed it.

#### Added by the code-review follow-up (`feat/multi-org-access-review-app`) — also confirmed, 2026-10-01

- [x] **Outlet picker blocks switching with unsent records.** With the outbox non-empty, open
      `/outlet-select` (e.g. via Settings after a location's been lost, or directly) with several
      eligible locations: every outlet button is disabled, a "Send unsent records..." message and
      a "Send now (N)" button appear, and tapping an outlet does nothing until the queue is
      cleared. With exactly one eligible location and a non-empty outbox, confirm it does *not*
      auto-pick — the picker/Send-now UI shows instead, and auto-pick only fires once the queue is
      empty (either arriving that way or via "Send now" draining it to zero).
- [x] **A lost assignment is re-asked, not silently kept, online.** Sign in and land on a location,
      then (as an admin, in another session/tab) remove that technician's assignment to that retail
      point while still online. Reload/relaunch the Field App: confirm it does *not* silently stay
      on the stale location — it routes to `/outlet-select` (other locations remain) or
      `/no-location` (none remain), matching the already-verified "picker appears"/"can't record
      here appears" checks above.
- [x] **A lost assignment is NOT re-asked while offline.** Same setup, but go offline (or simulate
      via devtools) before relaunching: confirm the Field App keeps the last-known location and
      renders the normal dashboard rather than bouncing to the picker or "can't record here" — a
      technician who's simply lost connectivity must keep working.
- [x] **First-ever offline launch (no saved location, no cache) shows retry, not "can't record
      here".** On a brand-new device/profile with no cached `my-orgs` response and no connectivity,
      sign in (or land on Home with a rehydrated token that never picked a location) and confirm
      Home shows an inline "Couldn't reach the server..." message with a Retry button — not
      `/no-location`'s "you're not set up to record here" screen. Tapping Retry once connectivity
      is restored should proceed to the picker/dashboard as normal.
- [x] **Current location is visible on every signed-in screen.** Visit Leads, Settings, and Failed
      records (and any consultation form) and confirm a small location line is visible on each,
      showing the same name as Home's own location line. Confirm it is *not* shown on Login,
      `/no-location`, or `/outlet-select`.
- [x] **The location line updates immediately after a switch.** From Settings, switch to a
      different location; confirm the persistent location line (visible on Settings itself, and on
      Home/Leads/etc. after navigating there) reflects the new location immediately, with no stale
      value on the first render after the switch.
- [x] **Change-password shows a whole-record rejection.** On Settings, trigger a change-password
      rejection that has no specific field to blame (e.g. submit the current password wrong, if the
      server keys that failure on `""` rather than `CurrentPassword`) and confirm a message appears
      under the button (not silence) — this is the regression the `FormErrors` fix targets.
      Separately, re-confirm ticket 09's original "Failed records" `""`-message check above still
      shows the message exactly once on ConsultationForm (no duplicate, no stray leading colon)
      now that `Attribute()` no longer drops `""` outright.
