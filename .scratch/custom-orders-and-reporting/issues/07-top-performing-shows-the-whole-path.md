# 07 — "Top performing" shows Tests, Leads, Sales and Conversion

**What to build:** Each row in the dashboard's four top-performing lists shows Tests, Leads, Sales
and Conversion. A switch ranks the lists by most Sales or by best Conversion. Conversion is worked
out the way the tiles do it and can't exceed 100%.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `Application.Tests` for the per-key figures; `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 06, 08.

## Acceptance criteria

- [ ] Each row shows four figures: Tests, Leads, Sales and Conversion.
- [ ] Conversion for a key is, of the Tests recorded there, the share that reached a Sale through
      a Lead: the tiles' definition applied to that key. It never exceeds 100%.
- [ ] A switch on the card chooses "Most sales" or "Best conversion", sent as a query-string
      parameter and kept with the other filters.
- [ ] Ranking by conversion leaves out rows with no Tests.
- [ ] The lists stay top five. "No retailer" and the "Unknown" rows behave as before.
- [ ] Tests: a key with more Sales than Tests shows a conversion at or below 100%; a key with
      Sales and no Tests shows 0% and is absent when ranking by conversion; the switch reorders a
      list where the two rankings differ.

## Notes

- Spec: `../spec.md` — user stories 15–17; "Dashboard".
- Whether a Test converted is judged against the full Lead and Sale history, as the tiles do; the
  date range only chooses which Tests are measured.
- Skills: `/tdd`, then `/code-review`.
