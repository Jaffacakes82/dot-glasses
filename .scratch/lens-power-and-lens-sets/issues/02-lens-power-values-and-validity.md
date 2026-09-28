# 02 — Lens power values and validity live in Rules, and custom prescriptions follow them

**What to build:** The shop's allowed lens power values are defined once, in Rules, and the server
enforces them. A custom prescription with a positive cylinder, a cylinder below -6.00, a sphere
outside ±10.00 or an off-step value is refused; axis is required when cylinder isn't 0 and refused
when it is; an add of 0.00 counts as no add; lens type is required only when an eye has an add and
must be empty otherwise; Other needs its text. The Field App's and Admin Portal's existing custom
dropdowns read their values from the same definition.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Opus 5.5 — the Rules rework; these helpers become the single source for every validator
and screen.

**Seam:** `DotGlasses.Rules.Tests` (main); `DotGlasses.Web.Tests` for one create-endpoint rejection
per rule family.

**Don't run alongside:** 03 (both edit `ConsultationRules`/`ReferenceDataSnapshot`).

## Acceptance criteria

- [ ] One Rules definition of the allowed values: sphere -10.00 to +10.00 in 0.25 steps, listed in the
      shop's order (0.00 first); cylinder 0.00 then -6.00 to -0.25 in 0.25 steps; axis 0–180 whole
      degrees; add 0.00 to 3.00 in 0.25 steps; pupil distance 54–74 mm unchanged. It owns the display
      format (`+` on positive values, two decimals). Nothing else lists these values.
- [ ] A lens power validity helper: sphere required; blank cylinder means 0.00; axis required when
      cylinder isn't 0 and empty when it is; blank or 0.00 add normalised to no add.
- [ ] Lens type rules: one per pair; required when either eye has an add above 0; empty otherwise
      (single vision); Other needs its text.
- [ ] `ConsultationRules` applies these to the custom branch. Failure keys are the request property
      names; scalar messages keep their existing wording where they already exist.
- [ ] The Field App's and Admin Portal's existing custom dropdowns read values from the Rules
      definition (no redesign — that's tickets 09 and 11).
- [ ] Rules.Tests cover each case in the spec's Testing Decisions for allowed values, axis and lens type.

## Notes

- Spec: `../spec.md` — "Rules" (allowed values, lens power validity, lens type). Decision: ADR-0007.
  Shop values: branch `research/custom-lens-option-ranges` (reference only, never merge).
- Rules may reference only Contracts (CLAUDE.md).
- Prior art: `ConsultationRulesTests`, `ConsultationValidationApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
