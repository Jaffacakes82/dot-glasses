# 10 — The Field App's coating choices

**What to build:** The Field App offers only the coatings both chosen lenses come in. A paired coating
is ticked and locked with the note "Comes with <trigger> on this lens". When a lens change removes a
coating that was ticked, it's unticked with a note, so nothing surprises the technician at save time.
A custom prescription offers any active coating.

**Blocked by:** 06, 09

**Status:** ready-for-agent

**Model:** Sonnet 5 — Field App markup over the Rules offered-coatings helper.

**Seam:** manual browser checklist (no Field App test project). Keep `dotnet build` green.

**Don't run alongside:** 09, 11 (consultation form and coating selector).

## Acceptance criteria

- [ ] The coating multi-selector takes the offered coatings and required pairings from the Rules helper.
- [ ] Paired coatings are ticked, locked, and carry "Comes with <trigger> on this lens".
- [ ] A lens change that removes an offered coating unticks it and shows a note.
- [ ] Coating preference (Tests and Leads) is limited to the offered coatings on a lens set.
- [ ] Manual checklist, recorded in Comments when done: offered coatings follow both lenses; locked
      pairing and its note; the removal note; a Sale saves without a server rejection.

## Notes

- Spec: `../spec.md` — "The coating multi-selector", user stories 27–29, 35.
- Skills: `/implement`, then `/code-review`.
