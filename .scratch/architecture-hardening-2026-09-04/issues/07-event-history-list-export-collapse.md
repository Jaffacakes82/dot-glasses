# 07 — Event History list and export collapse

**What to build:** An admin's CSV export contains exactly the rows the screen was showing. Today the
list and the export are separate methods per screen tab — eight in total — and nothing structurally
stops them drifting apart. They become one query per tab, with paging optional, so the export is the
same query unpaged rather than a second one that happens to agree.

**Blocked by:** 02 — Integration test harness on real Postgres.

**Status:** ready-for-agent
**Category:** refactor

- [ ] The query interface exposes four methods, one per screen tab, with paging optional
- [ ] Export and on-screen list for a given tab are provably the same query
- [ ] An unrecognised tab is handled the same way by the screen and by the export
- [ ] Filtering, searching, ordering and hierarchy scoping are unchanged for every tab
- [ ] A user still cannot export rows they could not see on screen
- [ ] Tests cover each tab's filters against real SQL, including case-insensitive name search
