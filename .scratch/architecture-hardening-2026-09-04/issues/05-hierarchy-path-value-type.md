# 05 — `HierarchyPath` value type and org lookup module

**What to build:** The org hierarchy path stops being a bare string. A value type owns the
trailing-slash invariant that keeps a sibling path from matching a prefix it does not belong to, and
exposes the ancestor and descendant questions as separately named operations so a caller has to say
which one it is asking. A companion lookup module answers "which outlet, which Retailer, which
country" once, replacing two near-identical private implementations. See ADR-0004 — persistence
deliberately keeps the plain string column and the global query filter is not changed.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent
**Category:** refactor

- [ ] A value type represents a hierarchy path and rejects one that does not satisfy the invariant
- [ ] Ancestor and descendant containment are separate, named operations that cannot be confused
- [ ] A path is not treated as a descendant of a prefix it merely shares leading characters with
- [ ] The lookup module resolves outlet name, Retailer and country from a path
- [ ] Retailer is the nearest `Intermediate`-level ancestor, per the glossary; a retail point with none is reported as having none
- [ ] Missing-name fallbacks are defined once rather than copied per call site
- [ ] The type and the lookup module are tested directly, with no database
- [ ] The global query filter and the persisted column shape are unchanged
