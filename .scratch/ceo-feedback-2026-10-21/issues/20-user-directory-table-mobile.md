# 20 — User Directory table makes the page scroll sideways on a phone

**Status:** ready-for-agent
**Category:** bug
**Blocked by:** None — can start immediately

> *Found while checking ticket 11's other screens at 375px, 2026-10-31. Filed separately, as
> ticket 11 asks.*

At 375×812 the User Directory page is 958px wide: its table sits directly in the page, so the
whole page scrolls sideways rather than the table. Lens Sets and Event History already avoid this
by wrapping each table in a `<div style="overflow-x:auto">` (Lens Sets adds `position:relative`
to keep its visually-hidden "Actions" heading inside the scroll box).

## Agent Brief

**Category:** bug
**Summary:** On a phone the User Directory's table scrolls inside its own box and the page does
not.

**Desired behavior:**
- Wrap the User Directory table the way Lens Sets and Event History wrap theirs, so the table
  scrolls and the page doesn't.
- While there: the Custom Orders screen's per-customer tables have no such wrapper either. It
  couldn't be checked with data locally (no orders); check it at 375px with an order present and
  wrap it the same way if it overflows.
- Optional, if it stays small: give the three-plus copies of the wrapper one `dg-*` class in
  `wwwroot/css/dot-glasses.css` instead of repeating the inline style.

**Acceptance criteria:**
- [ ] At 375×812, User Directory's `document.documentElement.scrollWidth` is 375, and the table
      can be scrolled sideways inside its box.
- [ ] Custom Orders, with at least one order, likewise.
- [ ] Desktop layout unchanged.
