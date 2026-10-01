# Map — CEO feedback, 26–27 Sep 2026

Label: wayfinder:map

## Destination

Every item from the CEO's 26 Sep call and 27 Sep feedback doc is either specified as
`ready-for-agent` tickets or ruled out of scope. The two big items (multi-org access and the lens
model) and the rule that events are recorded only at retail points are settled as specs, with ADRs
where they qualify. This map is for planning only; nothing is implemented from it.

## Notes

- **Sources**, kept outside the repo because they hold personal details and the repo is public:
  the feedback doc "Dot Admin & App Feedback" (27 Sep, with screenshots) and the Gemini transcript
  of the 26 Sep call. Tickets summarise them; never paste transcript text, names or emails in here.
- **Every item gets grilled**, including ones that look clear-cut. The lens model is changing
  underneath most of them.
- Work each ticket with `/grilling` and `/domain-modeling`. Update `CONTEXT.md` as terms settle, and
  write ADRs sparingly. ADR-0005 (lens sets are data-driven) is partly revisited by the lens tickets.
- Order: multi-org access first, then the lens model, then everything else.
- Day 2 items are deprioritised (see Out of scope).
- **Before go-live (for the handover):** production will have no active lens sets after the lens
  redesign ships (ticket 09). DGI must build the real ones on the new screen first. The same
  handover should ask DGI to review the coating list against the online shop's (ticket 07).
- The shared checkout is used by other sessions, so map edits happen on a branch in a worktree.

## Decisions so far

<!-- one line per resolved ticket: [title](issues/NN-slug.md) — gist -->

**Shipped, 2026-09-29:** every decision below is built. Tickets 01 and 02 shipped as the
[multi-org access spec](../multi-org-access-and-retail-points/spec.md) (PR #30), and tickets 05–10
as the [lens power spec](../lens-power-and-lens-sets/spec.md) (PR #31). Each spec's `issues/` folder
records what each build ticket delivered. The Field App browser checklists were run by hand on
staging on 2026-10-01 and pass.

- [How several org assignments combine into one user's access](issues/01-multi-org-access-model.md) — the Admin Portal uses the union of all assignments, and the Field App records at one remembered current location. "Primary org" is gone, and access is rechecked on every request. See ADR-0006.
- [Recording tests, leads and sales only at retail points](issues/02-record-only-at-retail-points.md) — a current location must be an active retail point the user is *directly* assigned to, and the server enforces it. No new records at a deactivated retail point. A user with no retail point sees a "can't record here" screen. Dummy retail points need no marker.
- [What a lens is: one lens database for lens sets and custom lenses](issues/06-what-a-lens-is.md) — a lens power is a value: sphere, cylinder, axis and add for one eye. Its allowed values copy the online shop and are fixed. Records store powers, not pointers to set entries. A lens-set entry is a power, a typed label and its coatings. Lens strength is retired. See ADR-0007.
- [Which coatings each lens in a lens set can have, and where pairings live](issues/07-coatings-per-lens.md) — each lens-set entry lists its coatings (at least one) and its own pairings, and the server enforces them. Global pairings are gone, and exclusions stay global. A pair gets one coating set, offered from what both lenses come in. See ADR-0007 "Coatings".
- [What happens to existing lens data](issues/09-migrate-existing-lens-data.md) — reset rather than convert. Old lens sets are retired and emptied, and lens strengths, the coating grid and global pairings are removed; exclusions stay. Old records keep their set name and show "—" for lens power. Staging and production start with no active lens sets, and example sets come from the dev-only seeder.
- [The lens set admin screen for adding lenses](issues/08-lens-set-admin-screen.md) — prototyped, and variant A was chosen: an "Add lens" dialog laid out like the online shop, one lens at a time, with a typed label, coatings and per-lens pairings. There is no bulk add for now. The prototype is on branch `prototype/lens-set-admin-screen`.
- [How the Field App captures a lens after the redesign](issues/10-field-app-lens-capture.md) — a lens set uses one "Lens" dropdown with "Same lens for both eyes" ticked by default, and shows the chosen lens's power underneath. Custom copies the shop, with an axis dropdown only when there's a cylinder. Choices are ticks, never dropdowns. The section runs lens → children's frame → PD → coatings → order. A converted Lead's lens is matched by power.
- [The lens option ranges the Dot Glasses e-commerce site offers](issues/05-custom-lens-option-ranges.md) — sphere (±10) and add (0 to 3) match the Field App. Cylinder doesn't: the site offers 0 to -6 only. The site also requires an axis of 0–180, asks lens type only when add is above 0, and has a different coating list.

## Not yet specified

- **Custom Orders after the redesign.** If custom lenses come from the lens database and leads can
  order, the Custom Orders queue and its fulfilment status may need to change shape.
- **MI that doesn't tally.** The call expects small reporting discrepancies after these changes.
  They can't be named until the dashboard ticket and the data changes land.

## Out of scope

Day 2, deprioritised for now. These come back only as a fresh effort.

- Transferring sub-orgs to a different parent before deactivating (the call called it "day two").
- Extra coating exclusions for individual lens sets (the feedback doc says "Day 2 or 3").
- Clicking an Event History record to see the full sale (the feedback doc says "maybe even Day 2").
- Assigning lens sets while creating an org, or from the org page (the feedback doc says "maybe Day 2").
- Changing the Field App's display name (the feedback doc says "no rush").
- Adding many lenses at once, whether by an editable grid or CSV import. Both were prototyped and
  set aside, 2026-09-28: few people set up lens sets, and rarely. See the lens set admin screen
  ticket.
