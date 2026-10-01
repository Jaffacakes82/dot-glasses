# 19 — A default or remembered lens set

Type: grilling
Status: resolved
Blocked by: 02, 06

## Question

Should an org have a default lens set, or should the Field App remember the last one used per
user or retail point?

## Context

- Feedback doc: "Setting default lens set - or at field app level remember the last lens set for
  that user or retail point."
- Triage-2026-09-26 Q8 decided no preselected lens range; this revisits it.
- From tickets 01 and 02: each device remembers its own current location, which is always a retail
  point the user is directly assigned to. A remembered lens set could follow the same pattern.

## Answer

Decided by grilling, 2026-10-01: **no change.** Nothing is built from this ticket.

- The Sale form keeps starting on "Select a lens range…", and the technician must choose. The
  2026-09-26 decision (ADR-0005: a new Sale has no default lens range) stands.
- **An admin-set default per organisation** was not wanted: it adds a screen and upkeep for
  little gain.
- **Remembering the last lens range used** was judged not worth building.
- **Starting on the first lens set in the list** was considered and rejected. The list is
  alphabetical, so the starting set would be decided by its name. Where two sets share a lens
  label, a technician could pick the lens without noticing the set is wrong, and the Sale would
  be recorded against the wrong set with nothing to flag it.
- Tests and Leads keep starting on "No preference yet", and the Admin Portal's conversion screen
  keeps starting empty.
