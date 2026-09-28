# 02 — Recording tests, leads and sales only at retail points

Type: grilling
Status: resolved
Blocked by: 01

## Question

The call settled that tests, leads and sales are only ever recorded at a retail point; any level
that wants to distribute creates a dummy retail point for it. How is that enforced, and what does
the Field App offer a user whose assignments include non-retail-point orgs?

## Context

- Call: "anything other than a retail point should not be able to distribute", "we would just
  train people to create dummy retail points".
- Today events are stamped with the caller's active `HierarchyPath`, whatever its level.
- Touches: the Field App location picker, the create endpoints, lens-set availability (which already
  asks "reaches this retail point"), and any existing non-retail-point events in nonprod.
- From ticket 01 (ADR-0006): the Field App records at one **current location**, chosen from the
  user's assignments and remembered per device. The user picks one when nothing is remembered, and
  it's chosen automatically when only one qualifies. Still open here:
  - which orgs qualify as a current location;
  - whether retail points *beneath* an assigned org count (a Kenya admin recording at a Kenyan
    outlet without a direct assignment);
  - what a user with no qualifying location sees in the Field App.

## Answer

Grilled 2026-09-28. `CONTEXT.md` gains **Retail point**, and **Current location** is tightened to
match. ADR-0006 gains a consequence for this rule.

- **A current location must be an active retail point that the user is directly assigned to.**
  - Being assigned to an org above a retail point doesn't let a user record there. An admin assigns
    them to that retail point first.
  - A retail point that is, or sits under, a training org still counts.
  - A deactivated retail point never counts.

  (Q1, Q2)
- **The Field App offers only those retail points**, following ticket 01's rules: remember the last
  one used on the device, choose automatically if there's only one, otherwise ask. (Q3)
- **A user with no qualifying retail point can still sign in to the Field App.** They see one screen
  saying they can't record from the app and should ask their admin to assign them to a retail
  point, with a sign-out button. (Q3)
- **The server enforces the rule.** A Test, Lead or Sale whose location fails it is refused. An
  offline record that fails it lands on Failed records with a clear message, as with a lost
  assignment in ticket 01. (Q4)
- **No new records at a deactivated retail point.** This covers converting a Lead on the Admin
  Portal: if the Lead's retail point is deactivated, the conversion is refused with a message
  saying so. (Q5)
- **Existing records above retail-point level in staging are left alone.** There is no migration,
  because the product isn't live. (Q6)
- **A dummy retail point, for example for outreach, is an ordinary retail point with a descriptive
  name.** There is no marker. (Q7)
