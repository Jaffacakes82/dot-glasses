# 09 — Docs for custom orders and reporting

**What to build:** The repo's documents describe what tickets 01–08 built.

**Blocked by:** 01, 02, 03, 04, 05, 06, 07, 08

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** None — documents only.

## Acceptance criteria

- [ ] `docs/functional-capabilities.md`: the dashboard (filters, tiles' definitions, the lists,
      referrals), Event History (columns, badge, CSV), Custom Orders (the record, "Not yet paid"),
      the Lead form's order tick, converting an ordered Lead, and the Leads list badge.
- [ ] `CLAUDE.md`: a custom order is its own entity and is hierarchy-scoped (the data scoping
      list); the domain model's `Test`/`Lead`/`Sale` bullet and the `SaleAssembly` bullet describe
      an ordered Lead's carry-over; ADR-0008 is referenced.
- [ ] `docs/open-issues.md`: manual Field App checks from tickets 02, 03 and 04 are listed until a
      person has done them.
- [ ] The map's decision lines for tickets 11, 12 and 21 say "shipped" in place of "Not yet
      built", and its "MI that doesn't tally" note is updated with anything found on staging.

## Notes

- Spec: `../spec.md`.
