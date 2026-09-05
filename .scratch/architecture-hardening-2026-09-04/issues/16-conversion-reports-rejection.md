# 16 — Conversion reports a rejection instead of half-completing

**What to build:** A conversion whose source record the caller cannot see stops succeeding quietly.
Today converting a Test to a Lead, or a Lead to a Sale, guards on whether the source record was found
— and the hierarchy scoping filter hides anything outside the caller's own subtree, so the new record
is created, the back-link is never written, and the caller is told it worked. This inverts that: the
conversion is refused, with a message, and nothing is written.

**Blocked by:** 15 — Domain rejection seam; 03 — Characterisation tests for the consultation services.

**Status:** ready-for-agent
**Category:** bug

- [x] Creating a Lead that names a source Test the caller cannot see is refused, and no Lead is created
- [x] Creating a Sale that names a source Lead the caller cannot see is refused, and no Sale is created
- [x] The refusal is reported as a business-rule rejection, reaching the caller as a validation failure
- [x] A conversion whose source is visible behaves exactly as before
- [x] The characterisation test written in ticket 03 to document the old silent behaviour is inverted
- [x] Both conversions still commit the new record and the source record's back-link in one transaction
- [x] Atomicity is asserted against a real database, not assumed
