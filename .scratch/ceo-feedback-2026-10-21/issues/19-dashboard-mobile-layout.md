# 19 — Dashboard is wider than a phone

**Status:** ready-for-agent
**Category:** bug
**Blocked by:** None — can start immediately

> *Found while checking ticket 11's other screens at 375px, 2026-10-31. Filed separately, as
> ticket 11 asks.*

At 375×812 the MI Reporting Dashboard scrolls sideways. With no records the page is 388px wide;
with a single Lead it is 440px. The cause is the same as ticket 11's: `Views/Home/Index.cshtml`
lays out its lower section with an inline `grid-template-columns: 2fr 1fr` and no breakpoint, and
the narrow right-hand column (Referrals logged, and the cards under it) can't shrink below its
content.

## Agent Brief

**Category:** bug
**Summary:** The Dashboard fits a phone: its two-column section stacks, and nothing scrolls
sideways.

**Desired behavior:**
- Below the Admin Portal's mobile breakpoint (991px) the two columns stack; above it they are
  unchanged.
- No horizontal page scroll at 375px wide, with or without records.
- Use a `dg-*` class from `wwwroot/css/dot-glasses.css` rather than the inline style. `.dg-split`,
  added for ticket 11, already does this for a 1.4fr/1fr split; reuse it with a modifier, or
  generalise it, rather than adding a second near-copy.

**Acceptance criteria:**
- [ ] At 375×812, with at least one record, `document.documentElement.scrollWidth` is 375.
- [ ] Desktop layout unchanged.
