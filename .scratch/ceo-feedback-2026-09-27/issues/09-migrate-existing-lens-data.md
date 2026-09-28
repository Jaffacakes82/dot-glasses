# 09 — What happens to existing lens data

Type: grilling
Status: open
Blocked by: 06, 07

## Question

Given the product isn't live, what happens to existing lens sets, `LensStrength` reference items,
the coating grid, and nonprod Tests/Leads/Sales that reference them: migrate, reseed or discard?

## Context

- Records reference `LensOption` Ids and `LensStrength` item Ids today.
- Precedent: ticket 04 of triage-2026-09-26 took "no compatibility aliases, not live" (Q5).
- From ticket 06 (ADR-0007):
  - Records will store each eye's lens power, one lens type and the lens range, with no pointer to
    a lens-set entry.
  - `LensOption` entries become powers with typed labels.
  - The `LensStrength` reference category is retired.
  - Today's lens-set labels (e.g. "+0.00 / +2.50 (Bifocal)") would have to be parsed into powers,
    or re-entered by hand.
