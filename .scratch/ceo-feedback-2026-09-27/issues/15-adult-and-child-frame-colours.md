# 15 — Separate frame colours for adult and child frames

Type: grilling
Status: resolved
Blocked by: None

## Question

Frame colours differ between adult and child frames, each with their own images. How is that
modelled in reference data and chosen on the forms?

## Context

- Feedback doc, Preset Catalogue page: "Frame colours vary by adult or child frame. Different
  reference data for the two frame sizes with their own images."
- Forms already ask "children's frame".
- From the e-commerce research (ticket 05): the shop also asks for a **dot colour** (blue, green,
  orange, pink, yellow, white). The Field App doesn't capture it. Decide here whether it should.

## Answer

Decided by grilling, 2026-10-01.

**Two reference data lists**

- Frame colours become two separate lists on the Reference Data screen: **Frame colours (adult)**
  and **Frame colours (child)**. Each has its own pictures and its own "Other" option.
- The current list (Black, Blue, Blue-Black, Brown-Black, Pink, Pink Black, Other) becomes the
  adult list unchanged.
- The child list starts with **"Other" only**. The children's colours and pictures aren't known
  yet, and the adult colours are deliberately not copied across. DGI fills the list in before
  go-live.

**On the forms**

- The colours offered follow the "children's frame" tick: the child list when ticked, the adult
  list when not. This applies on the Field App's Sale form and the Admin Portal's Lead conversion
  form.
- Changing the tick after picking a colour clears the pick.
- The server refuses a colour from the wrong list, as a rule in `ConsultationRules`.

**Sales already recorded**

- Left as they are. A children's-frame Sale recorded before this change keeps pointing at a
  colour now in the adult list and still shows its name. Only new Sales follow the rule.

**Dot colour**

- Not recorded. The feedback didn't ask for it, and it isn't known whether frames sold at a
  retail point offer a choice. A follow-up of its own if they do.
