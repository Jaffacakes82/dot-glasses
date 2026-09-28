# 10 — How the Field App captures a lens after the redesign

Type: grilling
Status: open
Blocked by: 06, 07

## Question

How do the lens-set and custom-lens paths on the Field App's consultation forms look once lenses
come from one database with per-lens coatings?

## Context

- Feedback doc, Field App: coatings and lens types as checkboxes only, consistently; custom lens's
  "order the lens" checkbox after the coating; don't ask lens type when add is +0.00; children's
  frame checkbox before pupil distance.
- Feedback doc: a lens set not available at the retail point shouldn't appear in the list
  (already done by the current build; see the retest ticket).
- From ticket 06 (ADR-0007):
  - The two eyes of a pair must share a lens type.
  - Single vision is inferred when neither eye has an add. Lens type is asked for only when an add
    is above 0.
  - Custom lenses use the shop's allowed values: cylinder 0.00 to -6.00 only, and axis required
    only when cylinder isn't 0.
  - Positive powers are shown with a `+`.
