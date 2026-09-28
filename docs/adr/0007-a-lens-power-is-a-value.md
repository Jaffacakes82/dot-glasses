# A lens power is a value; lens sets and custom prescriptions share it

Until 2026-09-28 the two lens ranges described lenses in unrelated ways. A lens-set entry
(`LensOption`) pointed at a "Lens strength" reference item whose only content was a text label
such as `+0.00 / +2.50 (Bifocal)`. A custom prescription stored typed sphere/cylinder/axis/add
numbers per eye. Nothing linked the two, so the system couldn't tell that a +3.00 chosen from a lens
set and a +3.00 entered as a custom prescription were the same lens. The CEO asked for one lens
database behind both.

We decided that a **lens power** (see `CONTEXT.md`) is a *value*: one eye's sphere, cylinder, axis
and add. Two lenses with equal numbers are the same lens power, wherever they came from. The
"database" is the set of **allowed values**. They are copied from the DOT Glasses online shop's
prescription configurator (sphere -10.00 to +10.00, cylinder 0.00 to -6.00, add 0.00 to 3.00, all
in 0.25 steps; axis 0–180 in whole degrees). They live in the shared Rules project and change only
with a release. The Admin Portal shows them on a read-only page.

- **A lens set entry** is a lens power built from those values, plus a typed label (required, and
  unique within its set), a lens type when it has an add, and the coatings it comes in.
- **A Test, Lead or Sale** stores each eye's lens power, one **lens type** for the pair and its lens
  range. It holds no pointer to the lens-set entry.
- **The "Lens strength" reference data category is retired.**

**Considered and rejected:**
- **Stored lens rows**, found or created on first use and referenced by sets and records. A row
  would carry nothing but the numbers it is keyed on. It adds find-or-create bookkeeping, and makes
  "same lens" depend on a lookup rather than on equality.
- **Pre-listing every combination** (about 2.4 million rows). Unmanageable, and nobody browses it.
- **Admin-editable allowed values.** The device and the server must agree on them, and they change
  about once a year at most, so a release is the right way to change them.

**Consequences:**
- History is immune to lens-set edits. A record keeps the power that was sold, even after its set's
  entry is changed or removed. The entry can still be worked out from the set, the power and the lens
  type, because a set may repeat a power only with a different lens type.
- Reporting groups and filters on the numbers directly. Event History's "lens power LE/RE" columns
  read off the record.
- Single vision is inferred when neither eye has an add. It is never asked, and it is not reference
  data. Bifocal, Progressive and Other remain reference data.
- This refines ADR-0005. Lens sets stay data-driven, and a lens range is still "a lens set" or
  "Custom prescription", but what is *inside* a lens set changes.
