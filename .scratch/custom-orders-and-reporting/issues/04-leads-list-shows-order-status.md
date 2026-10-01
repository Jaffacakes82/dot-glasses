# 04 — The Field App's Leads list shows an order's status

**What to build:** In the Field App's Leads list, a Lead that ordered its lens shows the order's
status as a badge, so the technician can see when it is ready for pickup.

**Blocked by:** 02

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP for the API; the list by hand.

## Acceptance criteria

- [ ] The Leads list uses the order status `LeadDto` already exposes (ticket 02). No new API work
      is expected; if the list endpoint doesn't return it, fix that here.
- [ ] The list shows a badge with the status wording used on the Custom Orders screen.
- [ ] A Lead with no order shows no badge.
- [ ] Web.Tests: an ordered Lead's status is returned and changes after the order is advanced.
- [ ] Manual checklist recorded in Comments: the badge appears and updates after a refresh.

## Notes

- Spec: `../spec.md` — user story 5.
- The list is online-only today; this doesn't change that.
- Skills: `/tdd`, then `/code-review`.
