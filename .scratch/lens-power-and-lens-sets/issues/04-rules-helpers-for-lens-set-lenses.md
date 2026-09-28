# 04 — Rules helpers for lens-set lenses

**What to build:** Three pure helpers in Rules that the Field App, the Admin Portal and the server all
share, so every screen lists, matches and offers coatings the same way:

1. **The fixed display order** of a set's lenses: single vision by sphere, then Bifocal, Progressive
   and Other, each by add, then by sphere.
2. **Matching** a lens power plus lens type to a lens in a set (for conversion seeding and Failed-record
   correction); no match is a normal answer.
3. **Offered coatings and required pairings** for a chosen pair of lenses: the coatings both lenses come
   in, and for every trigger coating from either lens's pairings, the coating it requires.

**Blocked by:** 03 (the lens-set DTO shape)

**Status:** ready-for-agent

**Model:** Opus 5.5 — the Rules rework; these define behaviour every client depends on.

**Seam:** `DotGlasses.Rules.Tests`.

**Don't run alongside:** 05 and 06 if they are editing the same Rules files (this ticket should add new
files, not edit `ConsultationRules`).

## Acceptance criteria

- [ ] The order helper sorts per the rule above; Rules.Tests pin it with a mixed set.
- [ ] The matching helper returns the lens with the same power and lens type, or none. Rules.Tests
      cover an exact match, no match, and two lenses that differ only by lens type.
- [ ] The offered-coatings helper returns the intersection of the two lenses' coatings and the
      required pairings from either lens. Rules.Tests cover both.
- [ ] Nothing outside Rules re-implements these (later tickets call them).

## Notes

- Spec: `../spec.md` — "New pure helpers in Rules". Rules may reference only Contracts.
- Prior art: `ConsultationRulesTests`, `ReferenceDataSnapshotTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
