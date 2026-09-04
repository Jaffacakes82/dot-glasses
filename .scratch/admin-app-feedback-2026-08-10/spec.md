# Admin & Distribution App Feedback — 2026-08-10

Source: `260811 Dot Admin & App Feedback.docx` (dated 10th Aug 2026, reviewed 2026-08-13).
Raw feedback session covering the Field Distribution App and Admin Portal, split into one
issue per distinct point.

Grouping note: closely-related sentences in the source doc (e.g. every sentence about the
hard-case checkbox, or every sentence about lens-coating compatibility) were combined into a
single issue rather than filed one per sentence. Items explicitly marked "Day 2"/"Day 3" in the
source were kept together as backlog notes rather than split into many premature tickets.

**Triage complete as of 2026-09-03.** Of the original 33 feedback points, 22 are fully specified
and `ready-for-agent`. The other 11 were closed during triage/grilling (7 removed outright as
resolved, not-a-bug, or explicitly dropped by the reporter; 4 parked as `wontfix` after grilling
surfaced either a firm reprioritisation or a real architecture conflict not worth carrying
forward right now). See "Closed issues" below for the full history — original files still exist
in git history if any need to be re-raised.

**Published as tickets (2026-09-03), via `/to-tickets`.** The 22 active issues below were
renumbered `01`–`22` in dependency order and now live as one file per ticket under `issues/`
(each carries an explicit `Blocked by` line per the local ticket template). The list below keeps
the original triage numbers for historical traceability; the mapping to each ticket's new number
is in brackets.

## Active issues — `ready-for-agent` (20) → published as tickets 01–22

01. [→ 01] Colour contrast — dark green background
03. [→ 02] Field App: back-button navigation
04. [→ 03] Field App: block submission with blank required fields
05. [→ 04] Field App: remove input placeholder text
06. [→ 05] Lens-coating compatibility rules (preset vs custom, default pairings, mutual exclusions, anti-glare option, compulsory)
07. [→ 06] Rebrand casing: "DOT" → "Dot Glasses"
08. [→ 07] Referral question redesign
09. [→ 08] Show lens-needed test result whenever glasses are needed, not just on lead
10. [→ 09] Record-test page should route to Lead page when contact details are captured
11. [→ 10] Lead: make PD optional
12. [→ 11] Frame colour options — image icons matching e-commerce (assets provided), remove frame-type dropdown, relabel Purple→Pink
13. [→ 12] Hard case checkbox — wording and layout
32. [→ 13] Reference data: add Lens Type category (Bifocal / Progressive / Other)
14. [→ 14] Custom lens: ask lens type when two powers are present (blocked by ticket 13)
15. [→ 15] Custom lens: spherical/cylindrical range incomplete (should be +10 to -10)
16. [→ 16] Custom lens: coating option missing entirely
21. [→ 19] Admin Portal: data export (event history, organisations, custom orders)
23. [→ 20] Admin Portal: where users manage their own login/account settings
28. [→ 21] Custom orders: retailer → retail point → customer grouping/hierarchy
31. [→ 22] Org hierarchy terminology: rename "child node" language

Ticket 14 is blocked by ticket 13 (needs the Lens Type category to exist). Otherwise no
cross-dependencies among this list.

## Closed issues (13)

Kept as a log, not individual files — each entry is the one-line reason it closed. Full grilling
transcripts/settled designs for the two that went through a real session (24, 30) are in this
message's conversation history and this file's own prior versions (`git log -- spec.md`); recover
from there if either needs to be re-raised.

**Removed — resolved, not-a-bug, or dropped by the reporter (6):**
- **02** Disabled/placeholder buttons — reporter: ignore, couldn't recreate.
- **20** Dashboard figures not filtering — demo-environment artifact, not a real issue.
- **22** "Log a consultation" not working — not a regression (never shipped under that name);
  reporter's actual ask (Admin-Portal consultation entry point) folded into the old issue-33
  backlog note before that was closed too.
- **26** Custom orders country toggle — leave as-is, no per-country flag exists today anyway.
- **27** Retail point "full refraction" checkbox — don't reverse the existing per-transaction
  lens-range design.
- **29** RBAC role clarification — `Manager` removal stands, not wanted back currently.

**`wontfix` — grilled or reprioritised, not carried forward (7):**
- **17** (ticket 17) Settings toolkit: eye disease chart + tele-optometrist link — closed
  2026-09-04, not carried into the MVP handover scope.
- **18** (ticket 18) Documents should open in-app, not in a browser — closed 2026-09-04, not
  carried into the MVP handover scope.
- **19** Admin Portal mobile view — effort check found no real mobile-responsive groundwork
  exists today; not the quick win the reporter was open to.
- **24** 2FA — fully grilled and decision-complete (Admin Portal only, TOTP-only, mandatory,
  recovery codes + admin reset), but parked: priority is shipping real functionality to end
  users for genuine feedback, not speccing further security work on a demo.
- **25** Dashboard date range filter — already implemented (Phase 7, commit `b3dec48`, two days
  after this feedback was written).
- **30** Lens option builder UI — grilling surfaced a real conflict between the proposed
  direct-entry builder and an already-shipped architecture change (`LensOption` no longer
  carries typed spherical/add columns), plus a second undiscovered blocker (issue 32). Parked
  rather than resolved.
- **33** Day 2/3 backlog (CRM reminders, booking integration, occupations dropdown,
  admin-editable dioptre range, phone-number length rules, retail point type/grouping, push
  messaging, cross-matching, photo digitisation) — explicitly deprioritised bundle, closed rather
  than held open.
