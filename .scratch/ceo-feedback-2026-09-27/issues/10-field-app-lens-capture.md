# 10 — How the Field App captures a lens after the redesign

Type: grilling
Status: resolved
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
- From ticket 07 (ADR-0007, "Coatings"):
  - On a lens set, offer only coatings that both chosen lenses come in.
  - Both lenses' pairings auto-add, and a paired coating can't be unticked.
  - The coating preference on a Test or Lead follows the same list.
  - On a custom prescription, any active coating is allowed, subject to exclusions only, with no
    pairings.
- Raised 2026-09-28, while reviewing the lens set screen prototype: on a lens set, the technician
  picks one lens *per eye* from the set. That's how records already work: one lens-set entry is
  one lens. Most readers want the same power in both eyes, so decide how the form makes that quick.
  One option is "same lens for both eyes", ticked by default, with the right eye copying the left.

## Answer

Grilled 2026-09-28. This covers the lens section of the Test, Lead and Sale forms. The general
question order (referred-or-treated last, occupation earlier) is ticket 13.

**Lens set**
- **One "Lens" dropdown, with "Same lens for both eyes" ticked by default.** Unticking it shows
  left and right dropdowns. When they differ, the right eye offers only lenses with the left eye's
  lens type, because a pair can't mix types. (Q1)
- **Lenses are listed by their typed label, in a fixed order worked out from the lenses:** single
  vision by sphere, then bifocal, progressive and other, each by add. There is no admin sort order
  to maintain. (Q2)
- **Once a lens is chosen, its lens power shows underneath,** for example "Lens power: SPH 0.00 ·
  ADD +2.00 · Bifocal". There is one line per eye when the eyes differ, with `+` on positive
  values. (Q9)

**Custom prescription**
- **Copy the shop.**
  - Each eye has four dropdowns, sphere, cylinder, axis and add, with the shop's values in the
    shop's order.
  - Axis is a 0–180 dropdown, replacing today's free number box. It is available only when that
    eye's cylinder isn't 0.
  - Lens type appears only when either eye has an add above 0.
  - There is no "same for both eyes" shortcut.

  (Q3)

**How choices appear**
- **"Checkboxes only" means visible tick-style choices, never dropdowns.**
  - A Sale's coatings are checkboxes.
  - Lens type is a set of radio buttons.
  - A Test or Lead's coating preference is also radio buttons, with "No preference" first.

  (Q4)
- **A paired coating is ticked and locked,** with the note "Comes with Blue block on this lens".
  Unticking the trigger coating releases it. Both lenses' pairings apply to the pair. (Q6)
- **If a lens change drops a ticked coating from what both lenses come in,** the coating is
  unticked with a short note, for example "Anti-glare removed: not available on +2.50". (Q7)

**Order of the lens section** (Q5)
1. Lens range.
2. The lens: a lens-set lens, or the custom prescription and then its lens type.
3. Children's frame. This now comes before pupil distance, as the feedback asked.
4. Pupil distance.
5. Coatings on a Sale, or coating preference on a Test or Lead.
6. "Order this lens from Dot Glasses", on custom only, last. It now comes after the coatings, as
   the feedback asked.

**Carrying over and correcting**
- **Converting a Lead or Test pre-selects the matching entry in the same set,** matched by power
  and lens type.
  - If the lens is no longer in the set, that eye is left empty, with a note such as "The +2.50 on
    this Lead is no longer in Kenya Readers. Choose a lens."
  - The existing "this lens set isn't available here" message still covers a whole set that has
    gone.
  - "Same lens for both eyes" starts ticked only if the two eyes match.
  - Reopening a record from Failed records to correct it works the same way.

  (Q10)

**The Admin Portal's lead-conversion form** follows the same rules and field order, including
"Same lens for both eyes", in its own styling. (Q8)
