# 01 — A custom order becomes its own record

**What to build:** Custom orders move off the Sale into a record of their own, with no change in
what anyone sees. The queue, the advance action and the dashboard's tiles read the new record.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Opus 5.5 — a data migration and a change to every reader of orders.

**Seam:** `DotGlasses.Infrastructure.Tests` for the migration and atomicity; `DotGlasses.Web.Tests`
over HTTP for the screens.

## Acceptance criteria

- [ ] A new hierarchy-scoped entity for a custom order: id, `HierarchyPath`, status, placed-at,
      and the placing Sale's id (the placing Lead comes in ticket 02). It holds no copy of the
      lens, coatings, pupil distance or customer.
- [ ] A migration creates one order per Sale that has a fulfilment status, with that status and
      the Sale's creation time, then drops `Sale.OrderFromDotGlasses` and `Sale.FulfilmentStatus`.
- [ ] `CreateSaleRequest.OrderFromDotGlasses` stays as the instruction to place an order.
      `SaleService` creates the order in the same unit of work as the Sale.
- [ ] Sending the same Sale twice (the outbox retrying) places one order.
- [ ] `CustomOrderService` lists and advances from the new table, reading the lens through the
      Sale. The existing refusals still come back as sentences.
- [ ] The dashboard's "Custom orders" and "Standard sales" tiles give the same numbers as before
      for the same data.
- [ ] `SaleDto` still tells the Field App and the Admin Portal whether a Sale has an order.
- [ ] Infrastructure.Tests: the migration's result; the Sale and its order commit or roll back
      together; a repeated create places one order.
- [ ] The existing Custom Orders and dashboard Web.Tests pass with only their setup changed.

## Notes

- Spec: `../spec.md` — "The custom order record". Decision: ADR-0008.
- The new entity is hierarchy-scoped, so the global filter in `DotGlassesDbContext` picks it up;
  read ADR-0004 before touching that filter.
- Skills: `/tdd`, then `/code-review`.
