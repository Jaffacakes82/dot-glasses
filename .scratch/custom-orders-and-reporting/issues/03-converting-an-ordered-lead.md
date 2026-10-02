# 03 — Converting an ordered Lead shares its order

**What to build:** When a Lead that already ordered its lens is converted to a Sale, the lens and
coatings are locked to what was ordered, the form says the lens is already ordered and shows its
status, and the Sale shares the Lead's order. This holds on the Field App and on the Admin Portal's
conversion form, and the server enforces it.

**Blocked by:** 02

**Status:** resolved

**Model:** Opus 5.5 — `SaleAssembly`, two forms and a server check that must agree.

**Seam:** `DotGlasses.Rules.Tests` (`SaleAssemblyTests`); `DotGlasses.Web.Tests` over HTTP; the Field
App by hand.

**Don't run alongside:** Spec D tickets 01, 03 and 04; Spec E ticket 02.

## Acceptance criteria

- [x] `SaleAssembly.Seed` carries an ordered Lead's lens block and its coating set. For a Lead
      that didn't order, it still seeds the set from the single preference. `SaleAssemblyTests`'
      reflection walk passes, with any new member carried or listed with a reason.
- [x] Field App and Admin Portal conversion: for an ordered Lead the lens and coatings are
      read-only and "Order this lens from DOT Glasses" is replaced by "This lens is already
      ordered" with the status. Frame colour, hard case and referral are asked as usual.
- [x] The server refuses a Sale whose `SourceLeadId` names an ordered Lead when its lens or
      coating set differs from the Lead's, or when it asks for a new order. The failure is keyed
      on the request's own property names (the lens fields that differ, `CoatingRefIds`,
      `OrderFromDotGlasses`) and comes from the controller, beside the existing `SourceLeadId` resolution;
      the service guards it too.
- [x] A correct conversion links the Sale to the Lead's order. No second order exists. The queue
      shows the order without "Not yet paid".
- [x] Field App, client-side: when the conversion-match prompt finds a Lead that has an order, the
      card tells the technician to open that Lead from the Leads list and offers no "convert"
      button. The server check above still applies to whatever is sent.
- [x] The dashboard's "Standard sales" tile leaves this Sale out.
- [x] Web.Tests cover: a correct conversion; a changed lens refused; a changed coating set
      refused; a second order request refused; the queue and tile after conversion.
- [x] Manual checklist recorded in Comments: the locked lens on both forms; the match-path refusal.

## Notes

- Spec: `../spec.md` — user stories 3–4; "Converting an ordered Lead".
- `Seed` is a seed, not an override (CLAUDE.md). The lock is the form's and the server's job.
- `ConversionSourceScopingApiTests` pins why this failure must be field-keyed.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-02 — built.** `SaleAssembly.Seed` carries an ordered Lead's coating set whole. `OrderedLeadConversion`
(`Rules/Sales`) is the lock: lens fields compared as a record stores them, the coating set compared as a set, a second order
refused — each keyed on the Sale request's own property. `SalesController` reports it beside the `SourceLeadId` check, the
Admin Portal's conversion asks it too, and `SaleService` guards it with one sentence. A correct conversion sets `SaleId` on
the Lead's order; no second order is created.

Both forms show the ordered lens read-only. The Admin Portal takes the coatings from the Lead and ignores a posted set or
tick. The Field App's match card, for a Lead with an order, offers "Go to Leads" and "Save as a separate sale" and no convert.

A Failed Sale that converts an ordered Lead reopens locked again when the Lead can be read; offline it opens unlocked and the
server's check is what holds.

Tests: `OrderedLeadConversionTests`, `SaleAssemblyTests`, `SaleServiceTests`, `CustomOrderFlowTests` (API and portal),
`DashboardCalculatorTests` and `DashboardScreenTests` for the Standard sales tile.

**Manual checklist — local browser, 2026-10-02:**
- [x] Field App: "Convert to sale" on an ordered Lead opens with the "This lens is already ordered — Submitted." card (both
      eyes, pupil distance, coatings), no lens controls and no order tick; frame colour, hard case and referral are asked.
- [x] Saving it converts the Lead; the queue shows one order for the customer with no "Not yet paid".
- [x] Field App match path: a new Sale typed for the same name and phone shows the card with "Save as a separate sale" and
      "Go to Leads" and no convert button.
- [ ] Admin Portal's locked form in a browser. **Not done by hand**; `CustomOrderFlowTests` reads the rendered page.
