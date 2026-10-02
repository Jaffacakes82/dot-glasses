# 04 — The Field App's Leads list shows an order's status

**What to build:** In the Field App's Leads list, a Lead that ordered its lens shows the order's
status as a badge, so the technician can see when it is ready for pickup.

**Blocked by:** 02

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP for the API; the list by hand.

## Acceptance criteria

- [x] The Leads list uses the order status `LeadDto` already exposes (ticket 02). No new API work
      is expected; if the list endpoint doesn't return it, fix that here.
- [x] The list shows a badge with the status wording used on the Custom Orders screen.
- [x] A Lead with no order shows no badge.
- [x] Web.Tests: an ordered Lead's status is returned and changes after the order is advanced.
- [x] Manual checklist recorded in Comments: the badge appears and updates after a refresh.

## Notes

- Spec: `../spec.md` — user story 5.
- The list is online-only today; this doesn't change that.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-02 — built.** No API change beyond ticket 02: `GET api/v1/leads/open` and `/{id}` return `CustomOrderStatus`,
read in one batch per list. `Leads.razor` shows "Lens ordered · <status>" using `CustomOrderStatus.Label()` (the Custom
Orders screen's wording, defined once in `Contracts`), highlighted at Ready for Pickup. A Lead with no order shows nothing.

Tests: `CustomOrderFlowTests` (status returned, changes after advancing, absent with no order), `LeadServiceTests`.

**Manual checklist:**
- [x] The badge appears on an ordered Lead ("LENS ORDERED · SUBMITTED") and not on another Lead in the same list — local
      browser, 2026-10-02.
- [ ] The badge changes after the order is advanced and the screen is reopened. **Not done by hand**; covered over HTTP.
