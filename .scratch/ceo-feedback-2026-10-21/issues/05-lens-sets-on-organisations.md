# 05 — Lens sets on the Organisations screen

Type: grilling
Status: needs-triage
Blocked by: 03

## Question

Should lens sets be assigned from the Organisations screen, and chosen (with the training flag)
when a sub-organisation is created?

## Context

- Feedback: "Assign lens sets from organisation page. Reason being, if users can't see the lens
  set page then they can't add lens sets to organisations they create."
- Feedback: "When adding sub organisation, be able to choose the lens set and flag as training
  organisation on the same set up form." Today the Add sub-organisation dialog asks Name and
  Level only; the training flag is a separate button on the org's detail panel, and lens sets are
  assigned from the Lens Sets screen (`PresetCatalogue.AssignInScope`).
- Lens sets are data-driven (ADR-0005): the Field App offers every non-empty lens set assigned at
  or above the retail point, so a new retail point under an org that already has one needs
  nothing.

## Points to settle

- Who may assign from the Organisations screen — the same `PresetCatalogue.AssignInScope` rule,
  or whatever ticket 03 decides for Users?
- Which lens sets the picker offers (every active set the caller can see, or only ones not
  already inherited from above?), and whether it shows inherited ones read-only so the reader
  knows a retail point is already covered.
- On create: several lens sets or one? Optional? Shown for every level, or only where nothing is
  inherited?
- Training flag on create: a checkbox, default off. Any level, or retail points only?
- Does assigning stay on the Lens Sets screen too (two places), or move?
