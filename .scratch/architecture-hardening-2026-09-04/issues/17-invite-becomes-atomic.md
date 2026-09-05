# 17 — Inviting a user becomes atomic

**What to build:** An admin inviting a user gets an account that is either fully created or not created
at all. Today the account, its role and its org assignments are three independent writes that each
commit on their own, so a failure part-way through leaves a user with no role, or no location, or
both — a state the User Directory then has to render and an admin has to unpick by hand.

**Blocked by:** 02 — Integration test harness on real Postgres; 15 — Domain rejection seam.

**Status:** ready-for-agent
**Category:** bug

- [x] Creating the account, assigning the role and assigning the org locations commit together or not at all
- [x] A failure at any step leaves no partially-created user behind
- [x] A failure is reported as a business-rule rejection with a usable message, not a raw identity error
- [x] The invitation email and password-reset link are still produced on success, and not on failure
- [x] Rollback is asserted against a real database
- [x] Inviting a user successfully behaves exactly as before
