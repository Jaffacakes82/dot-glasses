# 06 — Dashboard filters for Country and Retailer

**What to build:** The dashboard gets Country and Retailer dropdowns beside the date range. A
choice narrows every figure on the page, and each dropdown offers only what the viewer's scope
contains.

**Blocked by:** None

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 07, 08 (all three change `DashboardQueryService`).

## Acceptance criteria

- [x] Country and Retailer dropdowns beside the date range, sent as query-string parameters.
- [x] Options come from the viewer's scope. Choosing a country narrows the Retailer options to
      that country. "No retailer" is offered where retail points hang directly off a country.
- [x] A choice narrows the six tiles, Referrals logged, the trend, the gender split and the four
      lists, by hierarchy path through `OrgTreeLookup`.
- [x] The drill-down links to Event History and Custom Orders keep working. They carry the date
      range as today; the country and retailer are not carried, since those screens have no such
      filter, and a line under the tiles says so when a filter is active.
- [x] A value outside the viewer's scope yields no rows, never someone else's data.
- [x] Training organisations stay excluded.
- [x] Web.Tests cover: a DGI admin filtering to one country; a country admin offered only their
      own country and its retailers; a retailer filter; an out-of-scope value.

## Notes

- Spec: `../spec.md` — user stories 13–14; "Dashboard".
- The trend stays the real last six weeks, unaffected by the date range, but does follow the
  country and retailer.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-02 — built.** Country and Retailer dropdowns as `country` and `retailer` query parameters (`retailer=none` for
"No retailer"). Options come from the organisations the caller can see; a caller below Country level is still offered their
country, resolved upward. Choosing a country narrows the Retailer options. `DashboardFilter.Includes` narrows every figure by
path through `OrgTreeLookup`; the trend follows it. An unknown or out-of-scope value matches nothing. The tiles' links carry
the dates only, and `#drilldownNote` says so while an organisation filter is on.

Tests: `DashboardCalculatorTests`, `DashboardScreenTests`.
