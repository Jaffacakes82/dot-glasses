# 14 — Sale-assembly builder used by both write paths

**What to build:** A Sale is assembled in one place rather than two. Both the Field App and the Admin
Portal's Lead→Sale conversion build a Sale creation request today, by hand, field by field — which is
how the referral fields came to be missing from the admin path in the first place. A single builder in
the shared project takes a Lead plus the answers supplied and produces the request, so a field added
in future cannot reach one path and miss the other. This subsumes the point fix made in ticket 01.

**Blocked by:** 12 — Delete the three validators; controllers call the shared module; 01 — Admin portal point fixes.

**Status:** ready-for-agent
**Category:** refactor

- [ ] One builder produces a Sale creation request from a Lead plus supplied answers
- [ ] Both the Field App and the admin conversion form use it
- [ ] Carry-over rules — which values come from the Lead and which from the form — are expressed once
- [ ] Referral answers are carried through the admin path, replacing ticket 01's point fix
- [ ] A Sale converted from a Lead is still attributed to the Lead's own technician and retail point, not the converting admin's
- [ ] No sentinel or placeholder identifier is used for a required field that was not supplied
- [ ] Adding a field to the Sale request without handling it in the builder is caught by a test
