# 04 — Access-control policy tests

**What to build:** Every authorization policy is proven to deny what it should. This is the one area
where a regression is a data-exposure incident rather than a wrong number on a screen, which is why
it sits above the coverage bar while reporting sits below it.

**Blocked by:** 02 — Integration test harness on real Postgres.

**Status:** ready-for-agent
**Category:** chore

- [ ] Reference-data management is allowed only for an Admin at the top of the org hierarchy
- [ ] Preset catalogue management is allowed only for an Admin at Country level or above
- [ ] The Custom Orders screen and its advance action are allowed only at Country level or above, for any role
- [ ] An organisation write action is denied when the target org sits outside the caller's own subtree
- [ ] A user-directory write action is denied when the target user sits outside the caller's own subtree
- [ ] A denied policy check reaches the access-denied page rather than a bare not-found
- [ ] Each test exercises a caller both inside and outside the target's subtree
