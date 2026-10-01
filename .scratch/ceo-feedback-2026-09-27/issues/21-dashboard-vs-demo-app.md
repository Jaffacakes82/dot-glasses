# 21 — Dashboard gaps against the demo app

Type: grilling
Status: resolved
Blocked by: None

## Question

Which elements of the demo app's dashboard (plus earlier feedback) are still missing, and which
belong in scope?

## Context

- Feedback doc: "Dashboard not yet including some of the elements on the demo app + any feedback
  given."
- Call: expect "small fry bugs where maybe the MI doesn't quite tally".

## Answer

Decided by grilling, 2026-10-01. The "demo app" is the original design prototype (kept locally in
`design/`, not in the repo).

**What the demo had that the built dashboard lacks**

- Country, Retailer and Retail-point type filters. The built dashboard has a date range only.
- A switch on "Top performing" between best sales volume and best conversion.
- Everything else matches: the six tiles, Referrals logged, the conversion trend, the gender split
  and the four top-performing lists.

**Filters**

- **Country** and **Retailer** dropdowns are added beside the date range. Every figure on the page
  narrows to the choice. Each dropdown lists only what the viewer's own scope contains.
- **Retail-point type** is left out. No organisation has a type, and adding one for a single
  filter repeats what ticket 17 removed. A follow-up if DGI does report by type.

**Top performing**

- Each row shows four figures: **Tests, Leads, Sales and Conversion**.
- **Conversion** is worked out the way the tiles do it: of the Tests recorded there, the share
  that reached a Sale through a Lead. It can no longer exceed 100%. Today it is Sales divided by
  Tests, which goes above 100% wherever Sales are recorded without Tests.
- A switch ranks the lists by most Sales or by best Conversion. Ranking by conversion leaves out
  rows with no Tests.
- Custom orders and Pending leads are not added per row; the filters give those for one country
  or retailer.

**Referrals logged**

- A referral is counted once per customer journey. A Test and the Lead it became, or a Lead and
  the Sale it became, count as one. Unlinked records count separately. Today every Test, Lead and
  Sale marked "Referred or treated" is counted, and ticket 13 would make double counting more
  common.

**Nothing else**

- The feedback names no other items. Anything specific comes back as its own ticket once these
  changes are on staging.

**Related decisions in other tickets**

- Ticket 11 changes the "Custom orders" and "Standard sales" tiles.
- Ticket 18 changes how a deactivated organisation is named in the lists.
