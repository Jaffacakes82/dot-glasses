# 09 — The Field App remembers, shows and picks the current location

**What to build:** A technician lands straight on the location their device remembers. With nothing
remembered and several eligible retail points they're asked to pick one; with exactly one it's chosen
for them. A user with no eligible retail point (e.g. a DGI admin) sees one clear "can't record here"
screen explaining they should ask to be assigned to a retail point, with a sign-out button. The
current location is always in the header, and every consultation form says "Recording at <name>"
beside its submit button. Settings still switches location, and switching and sign-out stay blocked
while the outbox holds unsent records.

**Blocked by:** 06, 07

**Status:** ready-for-agent

**Model:** Sonnet 5 — Blazor markup and IndexedDB wiring over the server answers ticket 06 provides.

**Seam:** manual browser checklist (there is no Field App test project). Keep `dotnet build` green.

**Don't run alongside:** Spec B tickets that touch the consultation form (B01, B05, B09, B10).

## Acceptance criteria

- [ ] The device keeps its last current location in IndexedDB beside the cached token and sends it
      at sign-in as the preferred location.
- [ ] The outlet picker placeholder becomes real and appears when the server returns no location and
      several are eligible.
- [ ] The "can't record here" screen appears when there are no eligible locations, with sign-out.
- [ ] The header shows the current location's name; every consultation form shows "Recording at
      <name>" beside its submit button.
- [ ] Settings' location switch uses the new switch call; the outbox block on switching and sign-out
      is unchanged.
- [ ] Manual checklist, recorded in the ticket's Comments when done: remembers per device; auto-picks
      when only one; picker appears; "can't record here" appears; header and "Recording at"; Failed
      records shows the "no longer assigned" message after a lost assignment; switching stays blocked
      with unsent records.

## Notes

- Spec: `../spec.md` — "Field App screens", user stories 22–32. Never call the API directly from a
  page (CLAUDE.md outbox rule).
- Skills: `/implement`, then `/code-review`.
