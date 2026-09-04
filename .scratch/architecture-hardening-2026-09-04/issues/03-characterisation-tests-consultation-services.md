# 03 — Characterisation tests for the consultation services

**What to build:** The behaviour of recording a Test, a Lead and a Sale is pinned by tests before any
of it moves. This is the net the rules refactor is carried out under, and it needs nothing that does
not already exist — the three services depend only on interfaces, and a dictionary-backed fake
repository is already established prior art.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent
**Category:** chore

- [ ] Converting a Test to a Lead links both records in both directions
- [ ] Converting a Lead to a Sale links both records and marks the Lead converted
- [ ] Replaying a create with an identifier that already exists returns the existing record and does not duplicate it
- [ ] A Customer is matched by exact name and phone within the retail point, and created only when no match exists
- [ ] A Sale routed for fulfilment is given an initial fulfilment status; one that is not routed has none
- [ ] Coating set entries are de-duplicated on create
- [ ] A conversion whose source record is not visible to the caller is covered by a test that documents today's silent behaviour, ready to be inverted by ticket 16
- [ ] No test touches a database
