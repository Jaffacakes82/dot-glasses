# 06 — Coatings follow both lenses

**What to build:** The server enforces coatings per lens, not per label or per left eye. On a lens set,
a Sale's coatings must be ones both chosen lenses come in, and every coating paired with a trigger
coating (from either lens's pairings) must be present too. Global exclusions still apply. On a custom
prescription any active coating is allowed, subject to exclusions, with no pairings. A Test's or Lead's
coating preference, when both lenses come from a lens set, must be one both lenses come in.

**Blocked by:** 05

**Status:** ready-for-agent

**Model:** Opus 5.5 — the coating rules in the shared consultation rules.

**Seam:** `DotGlasses.Rules.Tests` (main); `DotGlasses.Web.Tests` for the create-endpoint rejections.

**Don't run alongside:** 05, 11 (`ConsultationRules`). 09 is safe in parallel (it doesn't touch the
server rules or the coating selector).

## Acceptance criteria

- [ ] Lens set: coatings outside the two lenses' intersection are refused; a trigger without its paired
      coating is refused, whichever lens the pairing is on; exclusions enforced.
- [ ] Custom prescription: any active coating, subject to exclusions; no pairing enforcement.
- [ ] At least one coating on a Sale, as today.
- [ ] Coating preference on a Test or Lead is limited to the intersection on a lens set.
- [ ] Uses the ticket 04 offered-coatings helper — no second implementation.
- [ ] Rules.Tests cover each case above. Web.Tests: a create is rejected for a coating outside the
      intersection and for a missing paired coating.
- [ ] Server-only: the Field App's coating selector is updated in ticket 10 and Admin Portal lead
      conversion in ticket 11.

## Notes

- Spec: `../spec.md` — "The coating rules", user stories 27, 35, 40. Decision: ADR-0007 "Coatings".
- Prior art: `ConsultationRulesTests`, `ConsultationValidationApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
