# 09 — Custom orders seen and edited at lower levels

Type: grilling
Status: needs-triage
Blocked by: None

## Question

Should users below Country level see their custom lens orders and status, and may anyone change
an order's lens after it's placed?

## Context

- Feedback: "Users and admins at lower levels should be able see their custom lens orders and
  their status. They can edit and custom lens as long as it has not advanced to too late a
  stage."
- Today `CustomOrders.View` needs Country level or above, and the screen also carries the
  advance-status action.
- **Editing conflicts with ADR-0008.** A custom order holds no lens of its own: the lens,
  coatings, pupil distance and customer are read through the Lead or Sale that placed it, and
  those can't be edited by design (create-once events, no update endpoint). There is no cancel.

## Points to settle

- Seeing: a read-only view for retail points and Retailer/distributors, scoped as usual? Which
  columns? Should the advance-status action stay Country+?
- Editing: which fields (lens power, coatings, PD?), until which `FulfilmentStatus`, and by whom?
- How editing fits ADR-0008: a correction record that supersedes the Lead/Sale's lens for the
  order, a cancel-and-reorder, or editing the order's own copy (which reverses "read through,
  never copied")? Whichever, the record's history must say what was ordered first.
- Where the Field App fits: does a technician see their orders on the device, offline?
- Is the editing part Day 2? The seeing part looks small on its own and could split off.
