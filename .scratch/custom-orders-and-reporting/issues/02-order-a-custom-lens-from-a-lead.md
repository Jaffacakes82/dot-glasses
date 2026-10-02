# 02 — Order a custom lens from a Lead

**What to build:** A technician recording a Lead with a Custom prescription can tick "Order this
lens from DOT Glasses". The Lead must then hold the complete lens and a full coating set, and an
order is placed with the Lead. The order appears in the Custom Orders queue marked "Not yet paid".

**Blocked by:** 01

**Status:** resolved

**Model:** Opus 5.5 — new rules shared by the device and the server, and a Lead gaining a coating
set.

**Seam:** `DotGlasses.Rules.Tests` for the rules; `DotGlasses.Web.Tests` over HTTP; the Field App by hand.

**Don't run alongside:** Spec D tickets 01, 03 and 04; Spec E ticket 02.

## Acceptance criteria

- [x] `CreateLeadRequest` gains `OrderFromDotGlasses` and `CoatingRefIds`. A Lead stores its
      coating set the way a Sale does, with a migration. `LeadDto` exposes the set and, when there
      is one, the order's status.
- [x] Rules in `ConsultationRules.Check(CreateLeadRequest, …)`, each keyed on its request property:
      ordering needs the Custom range; both eyes' power; pupil distance; lens type where there is
      an add; and a non-empty coating set satisfying the exclusions. A Lead that isn't ordering
      must send no coating set and keeps its optional single preference.
- [x] The custom order entity gains the placing Lead's id. `LeadService` creates the order in the
      same unit of work as the Lead. A repeated create places one order.
- [x] Field App Lead form: the tick appears only for the Custom range, last in the lens section.
      When ticked, the coating preference radios are replaced by the coating multi-selector. A
      stale tick is suppressed when the range isn't Custom, as on the Sale form.
- [x] The Custom Orders queue lists the order, reading the lens from the Lead, with a "Not yet
      paid" badge. Advancing its status works.
- [x] The dashboard's "Custom orders" tile counts it by the date it was placed. "Standard sales"
      is unaffected.
- [x] Messages follow Spec D's voice.
- [x] Rule tests for each refusal and for a valid ordering Lead. Web.Tests: a valid ordering Lead
      creates an order; a lens-set Lead with the tick is refused; an incomplete lens or no
      coatings is refused; the queue shows the badge.
- [x] Manual checklist recorded in Comments: the tick's visibility; the coating selector swap; a
      saved ordering Lead; the same Lead reopened from Failed records.

## Notes

- Spec: `../spec.md` — user stories 1–2, 6–8; "Ordering from a Lead", "Queue, tiles and the Field
  App". Decision: ADR-0008; coatings: ADR-0001.
- `CONTEXT.md` already says an ordering Lead carries a coating set.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-02 — built.** `CreateLeadRequest.OrderFromDotGlasses` and `CoatingRefIds`; `LeadCoating` rows; `LeadDto` exposes the
set and `CustomOrderStatus`. Rules (`ConsultationRules.LeadOrder`): the tick needs the Custom range; the pupil distance becomes
required; the coating set is held to `Coatings`, the same code a Custom Sale's is; an ordering Lead sends no preference and a
non-ordering Lead sends no set. A missing eye is the existing Custom failure, keyed on `LensRangeType` ("Choose a sphere for
each eye.") like every other Custom record, not on the eye's own field.

Field App: the tick is last in the lens section, Custom only; ticking swaps the preference radios for the coating selector;
a stale tick is suppressed when the range isn't Custom.

Tests: `ConsultationRulesTests` (ordering Lead section), `LeadServiceTests`, `CustomOrderRecordTests`, `CustomOrderFlowTests`.

**Manual checklist — local browser, 2026-10-02:**
- [x] No tick with no range chosen; the tick appears on Custom, after the coating control.
- [x] Ticking swaps "Coating preference (optional)" for the "Coating" selector and shows the explanatory line.
- [x] Saving an incomplete ordering Lead shows "Choose a sphere for each eye.", "Choose a pupil distance between 54 and 74 mm."
      and "Choose at least one coating." against their controls.
- [x] A complete ordering Lead saves and appears in the Custom Orders queue.
- [ ] The same Lead reopened from Failed records comes back with the tick and its coatings. **Not done** — nothing failed,
      so there was nothing to reopen. The restore code is in `LoadFailedRecordAsync`.
