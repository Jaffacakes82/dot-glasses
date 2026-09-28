# 02 — Lens power values and validity live in Rules, and custom prescriptions follow them

**What to build:** The shop's allowed lens power values are defined once, in Rules, and the server
enforces them. A custom prescription with a positive cylinder, a cylinder below -6.00, a sphere
outside ±10.00 or an off-step value is refused; axis is required when cylinder isn't 0 and refused
when it is; an add of 0.00 counts as no add; lens type is required only when an eye has an add and
must be empty otherwise; Other needs its text. The Field App's and Admin Portal's existing custom
dropdowns read their values from the same definition.

**Blocked by:** 01

**Status:** resolved

**Model:** Opus 5.5 — the Rules rework; these helpers become the single source for every validator
and screen.

**Seam:** `DotGlasses.Rules.Tests` (main); `DotGlasses.Web.Tests` for one create-endpoint rejection
per rule family.

**Don't run alongside:** 03 (both edit `ConsultationRules`/`ReferenceDataSnapshot`).

## Acceptance criteria

- [x] One Rules definition of the allowed values: sphere -10.00 to +10.00 in 0.25 steps, listed in the
      shop's order (0.00 first); cylinder 0.00 then -6.00 to -0.25 in 0.25 steps; axis 0–180 whole
      degrees; add 0.00 to 3.00 in 0.25 steps; pupil distance 54–74 mm unchanged. It owns the display
      format (`+` on positive values, two decimals). Nothing else lists these values.
- [x] A lens power validity helper: sphere required; blank cylinder means 0.00; axis required when
      cylinder isn't 0 and empty when it is; blank or 0.00 add normalised to no add.
- [x] Lens type rules: one per pair; required when either eye has an add above 0; empty otherwise
      (single vision); Other needs its text.
- [x] `ConsultationRules` applies these to the custom branch. Failure keys are the request property
      names; scalar messages keep their existing wording where they already exist.
- [x] The Field App's and Admin Portal's existing custom dropdowns read values from the Rules
      definition (no redesign — that's tickets 09 and 11).
- [x] Rules.Tests cover each case in the spec's Testing Decisions for allowed values, axis and lens type.

## Notes

- Spec: `../spec.md` — "Rules" (allowed values, lens power validity, lens type). Decision: ADR-0007.
  Shop values: branch `research/custom-lens-option-ranges` (reference only, never merge).
- Rules may reference only Contracts (CLAUDE.md).
- Prior art: `ConsultationRulesTests`, `ConsultationValidationApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

- **Rules API for later tickets** (namespace `DotGlasses.Rules.LensPowers`):
  - `LensPowerValues` — the one definition. Lists in the shop's order: `Sphere`, `Cylinder`
    (both 0.00 first), `Axis`, `Add`, `PupilDistanceMm` (`IReadOnlyList<decimal>`). Arithmetic
    checks: `SphereRange`, `CylinderRange`, `AxisRange`, `AddRange`, `PupilDistanceMmRange`
    (`AllowedRange(Min, Max, Step)` with `Allows(decimal)` and `Values()`). Display:
    `FormatPower(decimal)` → `+2.50` / `0.00` / `-1.25`, culture-invariant.
  - `LensPowerRules` — `Check(sphere, cylinder, axis, add, LensPowerNames)` (sphere required +
    values), `CheckValues(...)` (everything but the sphere requirement — the consultation branch
    uses this because its missing-sphere message is pair-level and keyed on `LensRangeType`),
    `LensType(hasAdd, lensTypeRefId, otherText, snapshot, refIdKey, otherTextKey)`,
    `HasAdd(add)` (> 0), `HasCylinder(cylinder)` (blank = 0), `NormaliseAdd(add)` (0.00 → null).
    `LensPowerNames(Sphere, Cylinder, Axis, Add)` supplies both the failure keys and the names in
    the messages, so ticket 07's dialog can pass its own field names. `LensType` only asks the
    snapshot about LensType items, so a write-path validator (ticket 07, which must not use the
    memoised snapshot) can hand it a literal snapshot built from `IReferenceDataLookupService`.
- **Messages.** Existing wording kept, with the bounds now read from the definition: sphere and add
  are character-for-character unchanged; cylinder keeps the same sentence with the new bounds
  ("CylinderLeft must be between -6 and 0 in 0.25 increments."); the axis range, lens type and pupil
  distance messages are unchanged. New: "AxisLeft is required when CylinderLeft isn't 0.00 — choose
  an axis from 0 to 180." and "AxisLeft must be empty when CylinderLeft is 0.00 — an axis only
  applies to a cylinder." The axis/cylinder agreement is only asked of a cylinder that is itself
  allowed, so a wrong cylinder produces one message, not two.
- **Deviations / judgement calls:**
  - An add of 0.00 is *treated* as no add by the rules but still *stored* as sent — no write-path
    normalisation (no migration or service change in this ticket). Ticket 04's matching should
    compare through `NormaliseAdd`/`HasCylinder`.
  - Custom-branch failures now come out per eye (left eye's four fields, then right's) rather than
    per field type. Only visible in `RuleResult` order; the HTTP body order is ModelState's.
  - Field App (`LensRangeSelector`): value lists, format and the PD/axis bounds read from
    `LensPowerValues`; zero now shows as `0.00` (was `+0.00`). Beyond reading the values, three
    small changes the new rules forced: the lens type prompt uses `HasAdd` (0.00 no longer asks);
    the axis box shows only while that eye has a cylinder (or already holds an axis, e.g. a Failed
    record) and is cleared when the cylinder goes back to 0.00; and `FieldError`s were added under
    both cylinders and both adds — they had none, so a server rejection keyed there had no control
    to render against.
  - Admin Portal (`LeadConversion/Convert.cshtml`): its custom fields were number inputs, not
    dropdowns; sphere, cylinder, axis and add are now selects fed from `LensPowerValues` (same grid,
    same field names). PD stays a number input with its bounds from the definition.
  - **Outbox:** a record queued before release with a now-refused value (positive cylinder, axis
    without a cylinder, lens type with a 0.00 add) is Rejected at sync and lands on Failed records
    keyed on `CylinderX`/`AxisX`/`LensTypeRefId`; the Field App now renders each of those against
    its control, and keeps the axis/lens type controls visible while they hold a value so the error
    has somewhere to sit.
  - Existing tests updated to valid values, none deleted: `Custom_AxisBoundaries` gains a cylinder,
    `Custom_AddPowerHasItsOwnNarrowerRange` sets no lens type at add 0.00, and the two
    `ConsultationValidationApiTests` range cases gain `CylinderRight` so `AxisRight = 180.5` is still
    a range failure.
- **Stale docs for B12:**
  - `CLAUDE.md`, the `DotGlasses.Rules` bullet: Rules now also holds `LensPowers`
    (`LensPowerValues`, `LensPowerRules`) — the allowed values and lens power validity.
  - `docs/functional-capabilities.md` §"Custom range" (≈ lines 801–811): cylinder is described as
    "−6.00 to +0.25" (was already wrong — the code offered −10 to +10); now 0.00 then −6.00 to −0.25;
    lists are in the shop's order with 0.00 first and zero shown as `0.00`; the axis box appears
    only with a cylinder and is refused without one; an add of 0.00 doesn't ask for a lens type.
    The Admin Portal's lead conversion custom fields are now selects, not number inputs.
- **Tests:** full `dotnet test DotGlasses.sln` — 559 passed, 0 failed (Rules 305, Application 96,
  Infrastructure 55, Web 103); baseline 475 + 80 Rules (`LensPowers/LensPowerValuesTests`,
  `LensPowers/LensPowerRulesTests`, new `ConsultationRulesTests` cases) + 4 Web
  (`ConsultationValidationApiTests`: positive cylinder, axis vs cylinder, lens type with a 0.00 add,
  and a shop-style prescription with a 0.00 add stored).
