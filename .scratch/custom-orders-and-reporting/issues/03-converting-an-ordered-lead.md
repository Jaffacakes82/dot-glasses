# 03 — Converting an ordered Lead shares its order

**What to build:** When a Lead that already ordered its lens is converted to a Sale, the lens and
coatings are locked to what was ordered, the form says the lens is already ordered and shows its
status, and the Sale shares the Lead's order. This holds on the Field App and on the Admin Portal's
conversion form, and the server enforces it.

**Blocked by:** 02

**Status:** ready-for-agent

**Model:** Opus 5.5 — `SaleAssembly`, two forms and a server check that must agree.

**Seam:** `DotGlasses.Rules.Tests` (`SaleAssemblyTests`); `DotGlasses.Web.Tests` over HTTP; the Field
App by hand.

**Don't run alongside:** Spec D tickets 01, 03 and 04; Spec E ticket 02.

## Acceptance criteria

- [ ] `SaleAssembly.Seed` carries an ordered Lead's lens block and its coating set. For a Lead
      that didn't order, it still seeds the set from the single preference. `SaleAssemblyTests`'
      reflection walk passes, with any new member carried or listed with a reason.
- [ ] Field App and Admin Portal conversion: for an ordered Lead the lens and coatings are
      read-only and "Order this lens from DOT Glasses" is replaced by "This lens is already
      ordered" with the status. Frame colour, hard case and referral are asked as usual.
- [ ] The server refuses a Sale whose `SourceLeadId` names an ordered Lead when its lens or
      coating set differs from the Lead's, or when it asks for a new order. The failure is keyed
      on the request's own property names (the lens fields that differ, `CoatingRefIds`,
      `OrderFromDotGlasses`) and comes from the controller, beside the existing `SourceLeadId` resolution;
      the service guards it too.
- [ ] A correct conversion links the Sale to the Lead's order. No second order exists. The queue
      shows the order without "Not yet paid".
- [ ] Field App, client-side: when the conversion-match prompt finds a Lead that has an order, the
      card tells the technician to open that Lead from the Leads list and offers no "convert"
      button. The server check above still applies to whatever is sent.
- [ ] The dashboard's "Standard sales" tile leaves this Sale out.
- [ ] Web.Tests cover: a correct conversion; a changed lens refused; a changed coating set
      refused; a second order request refused; the queue and tile after conversion.
- [ ] Manual checklist recorded in Comments: the locked lens on both forms; the match-path refusal.

## Notes

- Spec: `../spec.md` — user stories 3–4; "Converting an ordered Lead".
- `Seed` is a seed, not an override (CLAUDE.md). The lock is the form's and the server's job.
- `ConversionSourceScopingApiTests` pins why this failure must be field-keyed.
- Skills: `/tdd`, then `/code-review`.
