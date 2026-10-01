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
  handover should ask DGI to review the coating list against the online shop's (ticket 07). Once
  ticket 15 is built, DGI must also enter the children's frame colours and pictures; until then a
  children's frame can only be sold with the colour "Other". Once ticket 16 is built, DGI should
  also re-upload the six adult frame pictures, which otherwise load from the online shop and
  don't show offline.
- The shared checkout is used by other sessions, so map edits happen on a branch in a worktree.

## Decisions so far

<!-- one line per resolved ticket: [title](issues/NN-slug.md) — gist -->

**Specified, 2026-10-01, not yet built:** the decisions marked "Not yet built" below are grouped
into four specs, each with build tickets in its `issues/` folder:
- [Spec C — Managing users and organisations](../user-and-org-management/spec.md): tickets 03, 04,
  17, 18 and 20.
- [Spec D — Recording forms and wording](../recording-forms-and-wording/spec.md): tickets 13, 14
  and 22.
- [Spec E — Frame colours and their pictures](../frame-colours-and-pictures/spec.md): tickets 15
  and 16.
- [Spec F — Custom orders and reporting](../custom-orders-and-reporting/spec.md): tickets 11, 12
  and 21. See ADR-0008.

Suggested order: Spec D's tickets 01–04 first (they move the most of the recording form and its
rules), then E and F, with C alongside since it touches different screens. Ticket 19 needs no
build. Ticket 23 is still open and needs a person on staging.

**Shipped, 2026-09-29:** tickets 01, 02 and 05–10 are built. Tickets 01 and 02 shipped as the
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

- [Editing a user's role and org assignments](issues/03-edit-user-role-and-scope.md) — a dedicated Edit user page for role, org assignments and full name, with one Save that applies only what the admin changed. A user partly outside the admin's scope has a read-only role and a count of hidden orgs. An admin can't change their own role, and can change their own assignments only if their scope doesn't shrink. Invite and Edit share an indented org tree picker. Shipped 2026-10-01 (`.scratch/user-and-org-management/`).
- [Assigning several users to an org at once](issues/04-assign-many-users-at-once.md) — the Organisations screen's Assign users dialog becomes a filterable checkbox list of Active users, assigned to one org in one transaction, with each user's role shown and a line saying what the assignment grants. Unassigning stays one at a time, with a confirmation at a retail point. Shipped 2026-10-01 (`.scratch/user-and-org-management/`).
- [Ordering a custom lens from a lead](issues/11-custom-lens-leads-can-order.md) — a custom order becomes its own record, placed when a Lead or a Sale is recorded and pointing back to it. An ordering Lead must hold a complete lens and a full coating set. A Sale converted from an ordered Lead shares the order with its lens locked. The queue marks unpaid orders, and the dashboard counts orders rather than Sales. Not yet built.
- [Event History: lens power columns and the training-org marker](issues/12-event-history-columns.md) — the Sales and Leads tabs get Lens range, Lens power LE and Lens power RE columns in the existing one-line format. The CSV gets a column per value, plus lens type and coatings. A "Training" badge marks rows the dashboard leaves out, with a matching CSV column and no filter. Not yet built.
- [Question order and optional fields on the consultation forms](issues/13-consultation-form-order.md) — every form opens Age, Gender, Occupation and ends with "Referred or treated" (on a Test, just before the contact-details question). A Test continued into a Lead offers its referral answers as starting values. Referral location becomes optional; nothing else does. The Admin Portal's conversion form follows the same order. Shipped 2026-10-01 (`.scratch/recording-forms-and-wording/`, tickets 01–02).
- ["Customer aware of price": leads only, and never blocking](issues/14-customer-aware-of-price.md) — the step after Save is removed from both forms. The Lead form asks "Has the customer been told the price?" as a required Yes or No after "Reason not purchased", and stores the answer. Event History's Leads tab and CSV show it. A Sale no longer asks. Shipped 2026-10-01 (`.scratch/recording-forms-and-wording/` ticket 03); the Event History column is Spec F's.
- [Separate frame colours for adult and child frames](issues/15-adult-and-child-frame-colours.md) — two reference data lists, adult and child, each with its own pictures. The current colours become the adult list, and the child list starts with "Other" only. The forms offer the list matching the "children's frame" tick and the server enforces it. Dot colour isn't recorded. Not yet built.
- [Reference data images: URL or upload](issues/16-reference-data-images.md) — upload only, into the existing blob container: PNG, JPEG or WebP up to 1 MB, for the frame colour lists. The container stays private and the Admin Portal serves the pictures. The Field App keeps copies for offline use. The six existing shop addresses work until replaced. Not yet built.
- [Organisation "Kind" as a managed dropdown](issues/17-organisation-kind-dropdown.md) — Kind is removed altogether; nothing read it. The level labels become "Retail Point" and "Retailer/distributor" wherever a level is shown. Shipped 2026-10-01 (`.scratch/user-and-org-management/`).
- [Deactivating and reactivating an org with sub-orgs](issues/18-cascading-deactivation.md) — deactivating takes everything beneath it, after a confirmation with counts. Reactivating restores what went with it, and is refused while the org directly above is deactivated. Nothing is re-parented. Data stays in the reports under the real name marked "(deactivated)". Shipped 2026-10-01 (`.scratch/user-and-org-management/`).
- [A default or remembered lens set](issues/19-default-lens-set.md) — no change. A new Sale still has no starting lens range. An admin-set default, remembering the last one used and starting on the first in the list were each considered and turned down.
- [Forgot password on the sign-in pages](issues/20-forgot-password.md) — both sign-in pages get it, the Field App through its own screen. The link goes by email only, to the existing set-password page, and returns the person to the app they came from. The same message is shown for any address; suspended accounts get no email. One day, single use, one email per account every five minutes. Shipped 2026-10-01 (`.scratch/user-and-org-management/`).
- [Dashboard gaps against the demo app](issues/21-dashboard-vs-demo-app.md) — Country and Retailer filters are added; Retail-point type is not. "Top performing" rows show Tests, Leads, Sales and Conversion, with conversion worked out as the tiles do so it can't exceed 100%, and a switch to rank by sales or conversion. A referral counts once per customer journey. Not yet built.
- [Plain, friendly prompts and error messages](issues/22-user-facing-wording.md) — every error message a person can see in either app is reworded to one voice: say what to do, in the screen's own words. Unreachable rule failures share one plain message per group. "Username" becomes "email", and the location-switch message tells offline apart from failure. English only. Shipped 2026-10-01 (`.scratch/recording-forms-and-wording/`, tickets 04–05).

## Not yet specified

- **MI that doesn't tally.** The call expects small reporting discrepancies after these changes.
  Ticket 21 settled the two found by reading the code (referrals counted more than once, and
  conversion above 100%). Any others can't be named until the changes are on staging.

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
