# 13 — Lead coatings: several, with pairings

Type: grilling
Status: needs-triage
Blocked by: None

## Question

Should a Lead record a Coating set (several coatings, with the lens's pairings applied) instead
of a single Coating preference — always, or only in some cases?

## Context

- Feedback: "Lens set pairing not working on leads. Should be select multiple and blue block
  should also highlight photochromic." The screenshot is a Lead on a lens set, showing "Coating
  preference (optional)" as radio buttons (No preference, Photochromic, Clear, Blue block,
  Polarized, Sunglasses), with Blue block chosen.
- This is working as designed, not a bug. A Lead carries one optional **Coating preference**; only
  a Lead that orders a custom lens carries a **Coating set** (ADR-0008, Spec F). Pairings
  ("ticking A adds B") apply to a Coating set, which is why choosing Blue block does nothing.
- `SaleAssembly.Seed` turns a Lead's single preference into the Sale's starting Coating set; a
  Lead with a whole set would seed it whole, the way an ordered Lead already does.

## Points to settle

- Does every Lead now take a Coating set (optional? may be empty?), or only a Lead with a lens
  chosen?
- What the preference was for: did "No preference" carry meaning DGI reports on? Event History's
  Leads tab and CSV show the preference today.
- Pairings: apply the lens's pairings exactly as on a Sale (tick-and-lock)?
- Conversion: the Sale seeds from the Lead's set — and stays editable unless the Lead ordered
  (unchanged rule).
- Existing Leads keep their single preference: read as a one-item set, or kept as a separate
  field?
- Same change on a Test with "Needs glasses", which shares the preference control?
- Ticket 14 moves this section; settle together if the control changes shape.
