# 12 — Event History: lens power columns and the training-org marker

Type: grilling
Status: resolved
Blocked by: 06

## Question

What lens columns does the Sales tab show once a lens is a full record (left and right eye), and
how is a training org shown on the Outlet column?

## Context

- Feedback doc: add "lens power LE, lens power RE" on Sales; show whether the outlet is a training
  org "to aid data review".
- From ticket 06 (ADR-0007): every record stores each eye's lens power, whether it came from a
  lens set or a custom prescription. The LE/RE columns can read the record directly and show `+`
  on positive values.

## Answer

Decided by grilling, 2026-10-01. All of this is the Admin Portal's Event History screen.

**Lens columns**

- The **Sales** and **Leads** tabs each get three columns: **Lens range**, **Lens power LE** and
  **Lens power RE**, left eye first. The Tests tab is unchanged.
- Lens range is the lens set's name, or "Custom". The Sales tab's "· Custom" badge suffix is
  dropped, since the column says it.
- Each eye uses the existing one-line format from `LensPowerValues.FormatLensPower`
  (`SPH +2.50 · CYL -0.75 × 90 · ADD +2.00`). No new format.
- A record with no lens power shows "—". That covers records from before the lens redesign and
  Leads that captured no lens.
- Lens type and coatings are not shown on screen.

**CSV export**

- The Sales and Leads exports get the lens range, then a separate column per value for each eye
  (sphere, cylinder, axis, add), plus lens type and coatings.

**Training-org marker**

- A row is training data when its outlet is flagged as a training org or sits beneath one. This
  is the dashboard's rule, so the marker means "left out of the dashboard".
- On screen: a small "Training" badge beside the outlet name, on every tab with an Outlet column.
- In each CSV: a "Training org" column, Yes or No.
- No filter for training rows. A reviewer filters on the CSV column.
