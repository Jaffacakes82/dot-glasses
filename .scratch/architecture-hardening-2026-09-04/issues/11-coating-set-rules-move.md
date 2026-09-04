# 11 — Coating set rules move to the shared module

**What to build:** The final migration batch. The rules governing a Sale's **Coating set** — at least
one entry, no duplicates, every entry active, every entry available for the chosen lens where the
range restricts it, and no two entries excluding one another — are expressed once in the shared
module. A **Coating preference** on a Test or Lead remains a single value, per the ADR-0001 scope
correction. This batch is blocked by the lens range work because coating availability is scoped by
the chosen lens option.

**Blocked by:** 08 — Shared rules project and reference-data snapshot; 10 — Lens range rules move.

**Status:** ready-for-agent
**Category:** refactor

- [ ] A Sale requires at least one Coating in its set, on both the preset and custom branches
- [ ] Duplicate entries in a Coating set are rejected
- [ ] Every entry must reference an active Coating
- [ ] On a preset range, every entry must be configured as available for the chosen lens strength
- [ ] A lens with no configured coatings is reported against the lens, not the Coating set
- [ ] No two entries in a set may exclude one another, checked symmetrically
- [ ] A Coating preference on a Test or Lead is validated as a single optional value
- [ ] Exclusion and availability rules are covered by tests at the shared module's interface
- [ ] Externally observable validation behaviour is identical to before
