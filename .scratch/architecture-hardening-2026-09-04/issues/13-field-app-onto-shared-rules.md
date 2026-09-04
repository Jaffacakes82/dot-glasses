# 13 — Field App onto the shared rules

**What to build:** The payoff for a technician. The Field App stops carrying its own copy of the
consultation rules and calls the shared module instead, so the device and the server become incapable
of disagreeing. A mistake is caught while the customer is still present, in the same words the server
would have used, and the rules the client previously did not enforce at all — free-text length limits,
power increments, the sellable pupil-distance range — start being caught before a record is queued.

Note this ticket is a horizontal slice by deliberate choice: the form runs every rule topic through a
single method, so migrating it topic by topic would build and tear down the same mess four times.
Agreed as a trade, with the cost being that the client stays on its own copy until this lands.

**Blocked by:** 12 — Delete the three validators; controllers call the shared module.

**Status:** ready-for-agent
**Category:** refactor

- [ ] The consultation form's own rule implementations are deleted and replaced by calls to the shared module
- [ ] The snapshot the form passes in is built from the already-cached reference data, and works with no connectivity
- [ ] Every rule the server enforces is enforced on the device before a record is queued
- [ ] Free-text length limits, power increments and the pupil-distance range are now caught client-side
- [ ] Client and server messages for the same rule are identical
- [ ] A failure is rendered against the control that produced it, for every field the form renders
- [ ] The known-field list no longer omits a key the form has a control for
- [ ] A record that would be refused by the server can no longer be queued while online or offline
- [ ] Manually verified in the running app, offline and online — there is no automated test for this wiring by agreed scope
