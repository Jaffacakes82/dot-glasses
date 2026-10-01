# A custom order is its own record, placed by a Lead or a Sale

A **custom order** (see `CONTEXT.md`) used to be two fields on a Sale: `OrderFromDotGlasses` and
`FulfilmentStatus`. The Custom Orders queue was "every Sale with a fulfilment status", and the
dashboard counted the same thing. The CEO then asked for an org to be able to order a custom lens
before the customer pays, which means ordering from a Lead. On 2026-10-01 we decided that a custom
order is a record of its own. It holds the order itself (its status, when it was placed, the retail
point) and points to the Lead or Sale that placed it. The lens, coatings, pupil distance and
customer are read from that record. An order is placed when the Lead or Sale is recorded and never
afterwards, and a Sale converted from a Lead that already ordered shares that Lead's order.

**Considered and rejected:**
- **The same two fields on the Lead as well.** The queue would list ordered Leads and ordered Sales
  together. It is the smaller change, but an order then lives in two tables, every reader has to
  know both, and a converted Sale needs a rule to stop it counting as a second order.
- **The order keeps its own copy of the lens.** Leads and Sales can't be edited, so the placing
  record can never drift from what was ordered. A second copy would be one more thing to keep in
  step.
- **Ordering on an existing Lead, after it is recorded.** That would be the first after-the-fact
  change to a Lead. Tests, Leads and Sales are create-once by design.

**Consequences:**
- A custom order can exist with no Sale at all. The queue marks such an order "Not yet paid" until
  its Lead converts, and the dashboard's "Custom orders" tile counts orders, not Sales.
- There is no cancel. If the customer never pays, the order still runs to Fulfilled and the org
  carries the cost. That is the risk the org takes by ordering early.
- A Sale converted from an ordered Lead has its lens and coatings locked to what was ordered. A
  changed prescription is a new Sale recorded from scratch.
- [ADR-0001](0001-coating-is-a-set-with-pairing-and-exclusion-rules.md) describes the coating set as
  the Sale's alone. A Lead that places an order now carries a coating set too, because the lab
  makes what was ordered. A Lead that doesn't order keeps its single coating preference.
- Existing orders are migrated off the Sale into the new records. The solution was not live when
  this was decided, so the old fields are removed rather than kept alongside.
