# Lens sets are data-driven; a lens range is "a lens set" or "Custom prescription"

> Refined by [ADR-0007](0007-a-lens-power-is-a-value.md) (2026-09-28). What is *inside* a lens set
> changed: an entry is now a lens power with a typed label, not a "Lens strength" reference item.
> Everything below about lens sets being data-driven still stands.

The Field App used to offer exactly three lens ranges: 6-Lens Set, 9-Lens Set and Custom
prescription. The original lens powers came from two separate lists, one for six lenses and one for
nine. Each preset catalogue carried a `Kind`, and at most one catalogue system-wide could hold each
of `SixLensSet`/`NineLensSet`. Every other catalogue was invisible to the device, and assignment
could only decide *whether* a retail point got the 6-Lens set, never *which* one. On 2026-09-26 we
decided that **lens sets** (see `CONTEXT.md`) are configurable data. There can be any number of them.
The Field App offers every active, non-empty lens set assigned at or above the retail point, in
alphabetical order, then Custom prescription. A record's **lens range** is therefore either one lens
set, identified by id, or a Custom prescription. "6-Lens" and "9-Lens" survive only as lens set names.
`Kind` is removed, and the lens range type collapses from `SixLensSet`/`NineLensSet`/`Custom` to a
lens set / Custom pair.

**Considered and rejected:**
- **One worldwide 6-Lens set and one 9-Lens set.** This was the status quo. It made every catalogue
  beyond the two dead configuration, and it could not express a country stocking a different kit.
- **Per-country 6-Lens / 9-Lens sets.** The uniqueness rule would become "one of each kind reaching
  any retail point". This keeps the fixed three-way choice the business said it does not have, and
  needs its own ambiguity rules for a retail point that inherits two.

**Consequences:**
- Nothing that was stored is lost. Every Test, Lead and Sale already records which lens set it used
  (`PresetCatalogueId`), so existing 6/9 records map straight onto "lens set" and keep their set's
  name. Reporting only ever distinguished Custom from not-Custom.
- The solution was not live when this was decided, so no compatibility alias was kept for the old
  6/9 wire values.
- Because the choice is data-driven, availability has to be a rule rather than a UI accident. The
  server refuses a lens set that doesn't reach the record's location, and there is no default lens
  range: a new Sale must pick one.
- A set used across countries can't be changed by one country's admin. Editing and retiring a lens
  set are limited to admins at or above its owning org. Assigning any active set is open to any
  admin, within their own scope.
