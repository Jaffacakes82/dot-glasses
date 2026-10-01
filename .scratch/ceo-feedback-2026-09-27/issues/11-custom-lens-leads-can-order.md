# 11 — Ordering a custom lens from a lead

Type: grilling
Status: resolved
Blocked by: 06

## Question

Custom-lens leads should be able to order the lens before the customer pays. What does that do to
the Lead, the Sale it later converts to, and the Custom Orders queue (fulfilment lives on the Sale
today)?

## Context

- Feedback doc: "Custom lens leads should have the option to order the lens as well. In case an org
  wants to order lenses before the client pays."
- Today `OrderFromDotGlasses`/`FulfilmentStatus` exist only on `Sale`.

## Answer

Decided by grilling, 2026-10-01. "Custom order" is now in `CONTEXT.md`.

**A custom order is its own record**

- It stops being two fields on a Sale. A custom order holds the order itself (status, when it was
  placed, the retail point) and points to the Lead or Sale that placed it. The lens, coatings,
  pupil distance and customer are read from that record, which can't be edited, so there is no
  second copy.
- Existing Sale orders are migrated into the new records. The product isn't live, so a migration
  is acceptable.
- The four statuses and the forward-only flow are unchanged. There is no cancel.

**Ordering from a Lead**

- A tick on the Lead form, offered for the Custom range only, at every retail point. No per-org
  switch.
- The order is placed when the Lead is recorded, never afterwards. Leads stay create-once.
- An ordering Lead must hold a complete lens, to the same standard as a Sale: both eyes' power,
  pupil distance, lens type where there is an add, and a full **coating set** rather than a single
  coating preference. A Lead that isn't ordering keeps its optional single preference.
- If the customer never pays, the order still runs to Fulfilled and the org carries the cost.

**Converting an ordered Lead to a Sale**

- On the Field App and the Admin Portal's conversion screen alike, the lens and coatings are
  locked to what was ordered. "Order from DOT Glasses" is replaced by a line saying the lens is
  already ordered, with its status.
- The Sale shares the Lead's order and never places a second one. A changed prescription is a new
  Sale recorded from scratch.

**Custom Orders queue**

- Lists custom order records, whichever kind of record placed them.
- An order whose Lead hasn't converted carries a "Not yet paid" badge, which drops off on
  conversion. No extra filter.

**Dashboard**

- "Custom orders" counts custom order records, paid or not, by the date the order was placed.
- "Standard sales" is Sales with no custom order behind them, whether the order was placed on the
  Sale or on the Lead it came from. An unpaid order shows as a custom order but not yet as a sale.

**Field App**

- The Leads list shows the order's status as a badge on an ordered Lead, so the technician can see
  when it is ready for pickup. Showing status for orders placed from a Sale is left out.

**Follow-on notes for the spec**

- `SaleAssembly.Seed` carries a Lead's lens today and seeds the Sale's coating set from the Lead's
  single preference. For an ordered Lead it carries the Lead's coating set instead, and the form
  locks both.
- ADR-0001 describes the coating set as the Sale's alone; an ordering Lead now has one too.
