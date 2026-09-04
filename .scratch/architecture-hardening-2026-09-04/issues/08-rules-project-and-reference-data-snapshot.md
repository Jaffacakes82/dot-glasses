# 08 — Shared rules project and reference-data snapshot

**What to build:** The expand phase. A new shared project is created holding a reference-data
snapshot type — the facts the consultation rules need, loaded once rather than asked for a field at
a time. Nothing validates through it yet, but it earns its keep immediately: it becomes the single
place a reference-data identifier is turned into a display label, replacing seven separate
implementations that had four different fallback strings between them. See ADR-0002, which also
records why caching the snapshot across requests is deliberately deferred.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent
**Category:** refactor

- [ ] A new shared project exists that the Field App and the server may both reference
- [ ] The behavioural contract document's App-reference rule is amended in the same commit that creates it
- [ ] The snapshot carries reference-data items with their active and "Other option" state, preset catalogues, and Coating pairing and exclusion rules
- [ ] A server-side adapter fills it from the database, including retired items
- [ ] A Field App adapter fills it from the existing cached response, with no API change
- [ ] Label resolution for every server-side consumer goes through the snapshot, with one fallback for a missing item
- [ ] A historical record referencing a retired item still renders its label
- [ ] The "Other" free-text override still wins over the stored label wherever it did before
- [ ] The snapshot is loaded once per request
