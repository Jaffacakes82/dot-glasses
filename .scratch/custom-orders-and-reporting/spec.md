# Spec F — Custom orders and reporting

Status: resolved
Source: wayfinder map `.scratch/ceo-feedback-2026-09-27/` — tickets 11 (ordering a custom lens from
a lead), 12 (Event History's lens columns and the training-org marker) and 21 (dashboard gaps
against the demo app). Decision: ADR-0008. Vocabulary: `CONTEXT.md` — **Custom order**, **Coating
set**, **Coating preference**, **Retailer**, **Referred or treated**.

## Problem Statement

A custom lens can only be ordered from DOT Glasses when a Sale is recorded, which means after the
customer has paid. An organisation that wants to order before payment has no way to do it. The
order is two fields on the Sale, so every screen that shows orders reads Sales.

Event History shows who bought and where, but nothing about the lens. Training organisations' rows
look like any others, so a reviewer can't see why Event History and the dashboard disagree.

The dashboard lacks the demo app's Country and Retailer filters and its ranking switch. Two of its
figures can mislead: "Referrals logged" counts the same referral on a Test and on the Lead it
became, and the top-performing lists' conversion is Sales divided by Tests, which goes above 100%
wherever Sales are recorded without Tests.

## Solution

**A custom order is its own record** (ADR-0008). It is placed when a Lead or a Sale is recorded and
points back to it. A Lead can order a Custom-range lens; it must then hold a complete lens and a
full coating set. A Sale converted from an ordered Lead shares that order, with its lens locked.
The queue marks unpaid orders, and the Field App's Leads list shows an order's status.

**Event History shows the lens and marks training rows.** The Sales and Leads tabs get Lens range,
Lens power LE and Lens power RE. Rows the dashboard leaves out carry a "Training" badge. The Leads
tab shows whether the customer was told the price. The CSV exports carry all of it in separate
columns.

**The dashboard gets filters and honest figures.** Country and Retailer filters narrow the whole
page. Top-performing rows show Tests, Leads, Sales and Conversion, ranked by sales or by
conversion, with conversion worked out the way the tiles do it. A referral counts once per customer
journey.

## User Stories

1. As a technician recording a Lead with a Custom prescription, I want to tick "Order this lens from DOT Glasses", so that the lens is being made before the customer returns to pay.
2. As a technician, I want an ordering Lead to require the complete lens and its coatings, so that the lab has what it needs.
3. As a technician converting an ordered Lead, I want the lens and coatings locked and a line saying the lens is already ordered with its status, so that I don't order it twice or change what is being made.
4. As an admin converting an ordered Lead on the Admin Portal, I want the same, so that both ways of converting agree.
5. As a technician, I want an ordered Lead in my Leads list to show its order's status, so that I know when to call the customer.
6. As a custom-orders user, I want orders placed from Leads in the same queue as orders placed from Sales, so that I work one list.
7. As a custom-orders user, I want an order whose Lead hasn't converted marked "Not yet paid", so that I can see what the organisation is carrying.
8. As a DGI admin, I want the dashboard's "Custom orders" tile to count orders, paid or not, and "Standard sales" to count Sales with no order behind them, so that the two tiles don't overlap.
9. As a reviewer, I want the Sales and Leads tabs to show the lens range and each eye's lens power, so that I can check what was sold or wanted.
10. As a reviewer, I want a "Training" badge on rows from training organisations, so that I can see which rows the dashboard leaves out.
11. As a reviewer, I want the Leads tab to show whether the customer was told the price, so that the answer recorded on the Lead is visible.
12. As a reviewer working in a spreadsheet, I want the CSV to hold each lens value in its own column, plus lens type, coatings and the training flag, so that I can sort and filter.
13. As a DGI admin, I want to filter the dashboard by Country and by Retailer, so that I can see one country's or retailer's figures without signing in as someone else.
14. As a country admin, I want those filters to offer only what my scope contains, so that I'm not shown organisations I can't see.
15. As an admin, I want each top-performing row to show Tests, Leads, Sales and Conversion, so that I see the whole path for each retail point, retailer, country and technician.
16. As an admin, I want to rank those lists by most Sales or by best Conversion, so that I can ask either question.
17. As an admin, I want conversion in those lists never to exceed 100%, so that it means the same as the tiles.
18. As an admin, I want a referral counted once when a Test became a Lead or a Lead became a Sale, so that the figure isn't inflated by the same customer.

## Implementation Decisions

**The custom order record (ADR-0008)**
- A new hierarchy-scoped entity holding: its id, the `HierarchyPath` of the retail point, the
  status (`FulfilmentStatus`, unchanged), when it was placed, and the id of the Lead or the Sale
  that placed it. It holds no copy of the lens, coatings, pupil distance or customer.
- A Sale converted from an ordered Lead is linked to the same order. The order then has both a
  placing Lead and a Sale; "unpaid" means it has no Sale.
- A migration creates one order per existing Sale that has a fulfilment status, then drops
  `Sale.OrderFromDotGlasses` and `Sale.FulfilmentStatus`. The request DTOs keep
  `OrderFromDotGlasses` as the instruction to place an order.
- The order is created inside the same unit of work as the Lead or Sale, in `LeadService` and
  `SaleService`. A retried offline record (same client id) must not place a second order.
- `CustomOrderService` lists and advances orders from the new table. It reads the lens through the
  placing record: the Lead for a Lead-placed order, the Sale otherwise.

**Ordering from a Lead**
- `CreateLeadRequest` gains `OrderFromDotGlasses` and `CoatingRefIds`.
- Rules in `ConsultationRules.Check(CreateLeadRequest, …)`: ordering requires the Custom range,
  both eyes' power, pupil distance, lens type where there is an add, and a coating set that
  satisfies the exclusions, to the standard a Custom Sale meets today. A Lead that isn't ordering
  keeps its optional single `CoatingPreferenceRefId` and must send no coating set.
- A Lead stores its coating set the way a Sale does. `LeadDto` exposes it and the order's status.
- The Field App's Lead form shows the tick only for the Custom range, last in the lens section,
  and swaps the coating preference radios for the coating multi-selector when ticked. As on the
  Sale form, the Field App suppresses a stale tick when the range isn't Custom; the Admin Portal
  has no Lead-creation form.

**Converting an ordered Lead**
- `SaleAssembly.Seed` carries the ordered Lead's lens block and its coating set (in place of
  seeding the set from the single preference). `SaleAssemblyTests`' reflection walk is updated for
  any new `CreateSaleRequest` or `SaleAnswers` member.
- Both forms render the lens and coatings read-only and replace the "order from DOT Glasses" tick
  with "This lens is already ordered", plus the status.
- The server enforces it: a Sale whose `SourceLeadId` names an ordered Lead must carry that Lead's
  lens and coating set and must not ask for a new order. The check sits with the existing
  `SourceLeadId` resolution on the controller, producing a field-keyed failure, with the service
  guard as defence in depth.
- The failure keys are the request's own property names: the lens fields that differ,
  `CoatingRefIds`, and `OrderFromDotGlasses` for a second order request.
- The Field App's conversion-match path (the Lead found after the form is filled in) can't lock a
  lens that has already been typed. When the matched Lead has an order, the app's match card
  tells the technician to open that Lead from the Leads list and offers no "convert" button.
  This is a client behaviour; the server's check above is what stops a mismatched Sale either
  way.

**Queue, tiles and the Field App**
- The queue groups as today (Retailer, retail point, customer). An order with no Sale carries a
  "Not yet paid" badge.
- Dashboard: "Custom orders" counts order records by the date the order was placed. "Standard
  sales" counts Sales with no order linked. Training organisations stay excluded.
- The Field App's Leads list shows a status badge on an ordered Lead.

**Event History**
- Sales and Leads tabs: Lens range (the lens set's name, or "Custom"), Lens power LE, Lens power
  RE, using `LensPowerValues.FormatLensPower`. No new format. "—" where there is no power. The
  Sales tab's "· Custom" badge suffix is dropped.
- A "Training" badge beside the outlet name on every tab with an Outlet column, when the outlet is
  a training organisation or sits beneath one (`OrgTreeLookup`'s existing rule).
- Leads tab: an "Aware of price" column, "—" for Leads with no answer.
- CSV: lens range; sphere, cylinder, axis and add per eye in separate columns; lens type;
  coatings; a "Training org" Yes/No column; and "Aware of price" on Leads. The list and the export
  keep sharing one row shape.

**Dashboard**
- Country and Retailer dropdowns beside the date range, as query-string parameters. The
  drill-down links carry the date range only, since Event History and Custom Orders have no such
  filters; a line under the tiles says so when a filter is active. Options come from the viewer's
  scope. A choice narrows every figure by
  hierarchy path through `OrgTreeLookup`; "No retailer" is an option where it applies.
- Top performing: each row carries Tests, Leads, Sales and Conversion. Conversion is the tiles'
  definition applied to that key: of the Tests recorded there, the share that reached a Sale
  through a Lead. A ranking parameter chooses most Sales or best Conversion; ranking by conversion
  leaves out rows with no Tests.
- Referrals logged: build chains from `Test.ConvertedToLeadId` and `Lead.SaleId`; a chain with any
  record marked "Referred or treated" counts once. Unlinked records count on their own.

## Testing Decisions

- **`DotGlasses.Infrastructure.Tests`** (real Postgres, real migrations): the migration turns each
  Sale with a fulfilment status into one order with the same status; a Lead and its order commit
  or roll back together; a retried create places no second order.
- **`DotGlasses.Rules.Tests`**: the ordering-Lead rules; `SaleAssemblyTests` for the ordered-Lead seed.
- **`DotGlasses.Application.Tests`**:
  pure helpers for the referral chains and per-key conversion, with hand-written fakes.
- **`DotGlasses.Web.Tests`** over HTTP:
  - a Custom Lead with the tick creates an order; a lens-set Lead with the tick is refused; an
    ordering Lead with an incomplete lens or no coatings is refused, each keyed on its field;
  - converting an ordered Lead with a changed lens or a second order request is refused; a correct
    conversion links the Sale to the same order and the queue shows one order, now paid;
  - the queue shows Lead-placed and Sale-placed orders, with "Not yet paid" on the former;
    advancing works for both;
  - the dashboard tiles count as specified; the filters narrow the tiles and lists and offer only
    in-scope options; the lists show four figures and never exceed 100%; the ranking switch
    reorders them; a Test and its Lead both marked referred count once;
  - Event History renders the lens columns and the Training badge; the CSV carries the separate
    columns.
- **`AccessAuditTests`** audits every controller action for three kinds of caller. Every new
  action in this spec is added to it.
- **Field App** — the Lead form's order tick and coating selector, the locked conversion, the
  refused match path and the Leads list badge — is checked by hand, with checklists in the
  tickets' Comments.

## Out of Scope

- Cancelling a custom order.
- Ordering on an existing Lead after it is recorded.
- Showing order status in the Field App for orders placed from a Sale.
- A filter for training rows on Event History; lens type and coatings on screen there.
- A Retail-point type filter; any further dashboard items from the feedback.
- Custom orders or pending leads per row in the top-performing lists.
- Opening a record from Event History (Day 2).

## Further Notes

- **Order of work.** Ticket 01 is a refactor with no change in behaviour and goes first. Ticket 05
  needs Spec D's ticket 03 for the "Aware of price" column. Tickets that edit
  `ConsultationForm.razor` or `ConsultationRules` should follow Spec D's tickets 01–04.
- **ADR-0001** describes the coating set as the Sale's alone; ADR-0008 records the exception for
  an ordering Lead. Read both before touching coating rules.
- **A Sale with no Tests** shows 0% conversion in the lists after this change, beside its Sales
  count. That is accurate, and expected.
- **MI that doesn't tally.** These are the two mismatches found by reading the code. Others can
  only be named once the changes are on staging.
