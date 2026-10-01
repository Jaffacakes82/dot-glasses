# 02 — Order a custom lens from a Lead

**What to build:** A technician recording a Lead with a Custom prescription can tick "Order this
lens from DOT Glasses". The Lead must then hold the complete lens and a full coating set, and an
order is placed with the Lead. The order appears in the Custom Orders queue marked "Not yet paid".

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Opus 5.5 — new rules shared by the device and the server, and a Lead gaining a coating
set.

**Seam:** `DotGlasses.Rules.Tests` for the rules; `DotGlasses.Web.Tests` over HTTP; the Field App by hand.

**Don't run alongside:** Spec D tickets 01, 03 and 04; Spec E ticket 02.

## Acceptance criteria

- [ ] `CreateLeadRequest` gains `OrderFromDotGlasses` and `CoatingRefIds`. A Lead stores its
      coating set the way a Sale does, with a migration. `LeadDto` exposes the set and, when there
      is one, the order's status.
- [ ] Rules in `ConsultationRules.Check(CreateLeadRequest, …)`, each keyed on its request property:
      ordering needs the Custom range; both eyes' power; pupil distance; lens type where there is
      an add; and a non-empty coating set satisfying the exclusions. A Lead that isn't ordering
      must send no coating set and keeps its optional single preference.
- [ ] The custom order entity gains the placing Lead's id. `LeadService` creates the order in the
      same unit of work as the Lead. A repeated create places one order.
- [ ] Field App Lead form: the tick appears only for the Custom range, last in the lens section.
      When ticked, the coating preference radios are replaced by the coating multi-selector. A
      stale tick is suppressed when the range isn't Custom, as on the Sale form.
- [ ] The Custom Orders queue lists the order, reading the lens from the Lead, with a "Not yet
      paid" badge. Advancing its status works.
- [ ] The dashboard's "Custom orders" tile counts it by the date it was placed. "Standard sales"
      is unaffected.
- [ ] Messages follow Spec D's voice.
- [ ] Rule tests for each refusal and for a valid ordering Lead. Web.Tests: a valid ordering Lead
      creates an order; a lens-set Lead with the tick is refused; an incomplete lens or no
      coatings is refused; the queue shows the badge.
- [ ] Manual checklist recorded in Comments: the tick's visibility; the coating selector swap; a
      saved ordering Lead; the same Lead reopened from Failed records.

## Notes

- Spec: `../spec.md` — user stories 1–2, 6–8; "Ordering from a Lead", "Queue, tiles and the Field
  App". Decision: ADR-0008; coatings: ADR-0001.
- `CONTEXT.md` already says an ordering Lead carries a coating set.
- Skills: `/tdd`, then `/code-review`.
