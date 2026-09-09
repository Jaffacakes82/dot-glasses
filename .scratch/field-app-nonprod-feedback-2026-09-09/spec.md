# Field App — Non-Prod Deployment Feedback — 2026-09-09

Source: manual testing of the deployed Field App (`nonprod.app.dotglasses.com`) in the
non-production Azure environment, reported by Joe 2026-09-09, immediately after the login/JWT
Key Vault fix landed. Nine points reported; two resolved to direct answers rather than tickets
(see Notes), leaving seven, split one issue per point per this repo's issue-tracker convention
(`docs/agents/issue-tracker.md`).

**Triage complete as of 2026-09-09.** All seven are reproducible or directly groundable against
the current codebase (no reporter follow-up needed), so all seven are fully specified and
`ready-for-agent`.

## Active issues — `ready-for-agent` (7)

01. Home screen's four action tiles and every screen's back-arrow link render with a browser-default
    underline
02. `client-logs` ships every framework-level Information log (not just the app's own warnings/
    errors) to the server — 2,400 events in minutes, plausibly self-amplifying
03. No application logs reach the Container App's log stream in nonprod at all — Serilog has no
    sink configured
04. Settings screen: three fake "Coming soon" Toolkit placeholders, and no real Change Password
    action
05. Home screen shows "Signed in / Field agent" — never the technician's actual name or their
    active org
06. "Did the customer share contact details?" Yes/No toggle never shows which option is currently
    selected
07. "Did this person buy a hard case? (click for yes)" reads as clicking text rather than using an
    obvious checkbox control

No cross-dependencies — all seven can be picked up independently and in any order.

## Notes for whoever picks these up

- **"How are messages set in the Field App?"** — answered directly, not ticketed. `Messages.razor`
  is a hardcoded `List<Announcement>` literal in the component's `@code` block
  (`src/DotGlasses.App/Pages/Messages.razor:27-31`) — there is no admin-side authoring UI and no
  API behind it. This matches the already-documented gap in `docs/functional-capabilities.md`
  ("Messages... still static placeholders"); nothing new here, so no ticket was opened. If real
  messaging is wanted, that's new scope to spec separately, not a bug fix.
- **"Settings should let me select my organisation scope if it doesn't already"** — it already does.
  `Settings.razor`'s "Active location" section (lines 19-42) lists every org the technician is
  assigned to via `IUserLocationClient.GetMyOrgsAsync()` and lets them switch via
  `SwitchLocationAsync`/`POST switch-org`. No ticket opened for this half of the report; ticket 04
  below covers only the "coming soon" Toolkit cleanup and the missing Change Password action from
  the same report.
- Tickets 01 and (the "back arrow" half of) the original report share one root cause and one fix
  location (`src/DotGlasses.App/wwwroot/css/dot-glasses.css`) — kept as a single ticket rather than
  two, same grouping call as the Admin Portal round's ticket 03.
- Ticket 03 (no logs reach the Container App at all) is almost certainly why the JWT-signing-key
  exception that broke login earlier today never showed up when checking container logs during
  that investigation — same root cause, different symptom, caught here as its own reportable
  finding rather than folded into the login fix.
- Tickets 02 and 03 are both logging-pipeline issues but touch opposite ends (Field App client vs.
  Web backend) and different files — kept separate.
