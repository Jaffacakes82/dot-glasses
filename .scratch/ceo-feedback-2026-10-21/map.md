# Map — CEO feedback, 21 Oct 2026

Label: wayfinder:map

## Destination

Every item in the CEO's 21 Oct feedback doc ("Admin & Distribution App Feedback", screenshots
taken 21–22 Oct on staging and production) is either built, specified as `ready-for-agent`
tickets, or ruled out of scope.

## Notes

- **Source** is kept outside the repo: it holds personal details (names, email addresses), and the
  repo is public. Tickets summarise it; never paste names, emails or screenshots in here.
- Triaged 2026-10-30. Each ticket was checked against the code at `97b5ae2`; where a ticket says
  "confirmed", the file and line it cites showed the behaviour.
- `Status:` uses the five labels in `docs/agents/triage-labels.md`. A `needs-triage` ticket is a
  grilling: work it with `/grilling` and `/domain-modeling` before any build.
- The CEO's own priorities: items marked "Day 2" or "if it's easy" are low priority (tickets 08,
  and the out-of-scope items below).

## Suggested order

1. **Fix first:** [16 — offline refresh gets stuck](issues/16-offline-refresh-stuck.md) (offline
   use is the Field App's core promise), then
   [07 — lower-ranking admins removing higher-ranking ones](issues/07-lower-admin-removes-higher.md)
   (an access hole, needs a short grilling first).
2. **Quick builds** (`ready-for-agent`, built from [Spec G](../feedback-fixes-2026-10-21/spec.md)):
   15, 17, 18 then 14 (blocked by 18), 11, 06 then 08 (blocked by 06), then 01.
3. **Grilling, as one session:** 03 → 05 (who can invite and assign lens sets, and where). 04
   follows once 03 settles what each level can do.
4. **Grilling, separately:** 13 (Lead coatings), 10 (full data export), 09 (custom orders at
   lower levels — may become Day 2).
5. **Waiting on DGI:** 02 and 12.

## Tickets

| # | Ticket | Status |
|---|---|---|
| 01 | [Dashboard gender split counts Sales](issues/01-gender-split-from-sales.md) | ready-for-agent |
| 02 | ["Dashboard not yet enabled"](issues/02-dashboard-not-enabled.md) | needs-info |
| 03 | [What a DGI-level User can do](issues/03-what-a-dgi-user-can-do.md) | needs-triage |
| 04 | [Page explanations that fit the reader's level](issues/04-explanations-per-level.md) | needs-triage |
| 05 | [Lens sets on the Organisations screen](issues/05-lens-sets-on-organisations.md) | needs-triage |
| 06 | [Hide "Outside your scope" in the User Directory](issues/06-hide-outside-scope-chips.md) | ready-for-agent |
| 07 | [A lower-ranking admin can remove a higher-ranking one](issues/07-lower-admin-removes-higher.md) | needs-triage |
| 08 | [Collapsible organisation list per user](issues/08-collapsible-org-list.md) | ready-for-agent |
| 09 | [Custom orders seen and edited at lower levels](issues/09-custom-orders-at-lower-levels.md) | needs-triage |
| 10 | [Export every record's data](issues/10-full-data-export.md) | needs-triage |
| 11 | [Organisations screen on a phone](issues/11-organisations-mobile-layout.md) | ready-for-agent |
| 12 | [Field App's installed name](issues/12-field-app-installed-name.md) | needs-info |
| 13 | [Lead coatings: several, with pairings](issues/13-lead-coatings-multi-select.md) | needs-triage |
| 14 | [Coatings straight after the lens](issues/14-coatings-after-lens.md) | ready-for-agent |
| 15 | [No pre-chosen answers on the forms](issues/15-remove-default-answers.md) | ready-for-agent |
| 16 | [Refreshing offline gets stuck on "You're offline"](issues/16-offline-refresh-stuck.md) | ready-for-agent |
| 17 | [Discard prompt shows the site address](issues/17-in-app-discard-dialog.md) | ready-for-agent |
| 18 | [Tick the only coating automatically](issues/18-auto-tick-only-coating.md) | ready-for-agent |

## Out of scope

- **A "Manager" tier, or admin-defined roles with per-capability permissions.** The CEO marks
  this Day 2. Ticket 03 decides what the existing User role can do in the meantime. (`Manager`
  was removed once already for never differing from Admin; a new one must differ by design.)

## Decisions so far

<!-- one line per resolved ticket: [title](issues/NN-slug.md) — gist -->

**Specified, 2026-10-30, not yet built:** the `ready-for-agent` tickets 01, 06, 08, 11 and 14–18
are grouped into [Spec G — Fixes from the 21 October feedback](../feedback-fixes-2026-10-21/spec.md).
The spec settles two things the tickets left open: the Field App's coating reconcile function
moves into `Rules` so the auto-tick has one tested home, and the Field App changes are checked by
hand on staging rather than through a new test project.

**Questions sent to DGI:** everything else open in this round (02, 03, 04, 05, 07, 09, 10, 12 and
13) is in one questionnaire for the CEO, kept outside the repo. Each answer that comes back moves
its ticket on.
