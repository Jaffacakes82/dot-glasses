# 01 — A custom order becomes its own record

**What to build:** Custom orders move off the Sale into a record of their own, with no change in
what anyone sees. The queue, the advance action and the dashboard's tiles read the new record.

**Blocked by:** None

**Status:** resolved

**Model:** Opus 5.5 — a data migration and a change to every reader of orders.

**Seam:** `DotGlasses.Infrastructure.Tests` for the migration and atomicity; `DotGlasses.Web.Tests`
over HTTP for the screens.

## Acceptance criteria

- [x] A new hierarchy-scoped entity for a custom order: id, `HierarchyPath`, status, placed-at,
      and the placing Sale's id (the placing Lead comes in ticket 02). It holds no copy of the
      lens, coatings, pupil distance or customer.
- [x] A migration creates one order per Sale that has a fulfilment status, with that status and
      the Sale's creation time, then drops `Sale.OrderFromDotGlasses` and `Sale.FulfilmentStatus`.
- [x] `CreateSaleRequest.OrderFromDotGlasses` stays as the instruction to place an order.
      `SaleService` creates the order in the same unit of work as the Sale.
- [x] Sending the same Sale twice (the outbox retrying) places one order.
- [x] `CustomOrderService` lists and advances from the new table, reading the lens through the
      Sale. The existing refusals still come back as sentences.
- [x] The dashboard's "Custom orders" and "Standard sales" tiles give the same numbers as before
      for the same data.
- [x] `SaleDto` still tells the Field App and the Admin Portal whether a Sale has an order.
- [x] Infrastructure.Tests: the migration's result; the Sale and its order commit or roll back
      together; a repeated create places one order.
- [x] The existing Custom Orders and dashboard Web.Tests pass with only their setup changed.

## Notes

- Spec: `../spec.md` — "The custom order record". Decision: ADR-0008.
- The new entity is hierarchy-scoped, so the global filter in `DotGlassesDbContext` picks it up;
  read ADR-0004 before touching that filter.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-02 — built.** `CustomOrder` (hierarchy-scoped: path, status, placed-at, `LeadId`, `SaleId`) and migration
`CustomOrderBecomesItsOwnRecord`. The migration was hand-ordered: the scaffold dropped the Sale's two columns first, which would
have lost every existing order, so it now creates the table, copies one order per Sale with a fulfilment status (same status,
retail point and time; a deleted Sale's order stays deleted), and only then drops the columns. `Down` puts a paid order back on
its Sale; an unpaid one has nowhere to go in the old shape and is lost on the way down. `SaleService` places the order in the
Sale's unit of work through `ICustomOrderRepository`; `CustomOrderService` lists and advances from the new table;
`AdvanceStatus` takes `orderId`. `SaleDto.OrderFromDotGlasses` and a new `CustomOrderStatus` are worked out from the order.

A repeated create is answered with the existing record in `SaleService` (and `LeadService`), so a retry places one order.

Tests: `CustomOrderRecordTests` (migration up and down, rollback, sent twice), `CustomOrderAdvanceStatusTests`,
`SaleServiceTests`. The "advancing a Sale that is not a custom order" refusal is gone with the case: an id that isn't an
order is now simply "no longer available".
