# 15 — Domain rejection seam across all services

**What to build:** A business-rule rejection becomes its own kind of failure, handled in one place, so
no screen can forget to catch one and no user sees a raw error page where a sentence was available.
Today rejections are thrown as the same type the persistence layer throws for a missing row, from 23
places, and only five screens catch them. See ADR-0003, including why a result type was considered
and rejected.

**Blocked by:** 01 — Admin portal point fixes (same code, avoids a conflict).

**Status:** ready-for-agent
**Category:** refactor

- [ ] A dedicated exception type represents a business-rule rejection
- [ ] A single filter turns one into a validation response, for both the API and the server-rendered screens
- [ ] Every service that rejects for a business reason throws that type instead of the general-purpose one
- [ ] A missing or out-of-scope row is distinguishable from a business-rule rejection at every catch site
- [ ] The per-controller catch blocks that existed only to do this by hand are removed
- [ ] No screen action can produce a server error page for a condition its service has a message for
- [ ] Messages remain user-facing copy, unchanged in wording
- [ ] Covered by tests at the API for at least one rejection per service
