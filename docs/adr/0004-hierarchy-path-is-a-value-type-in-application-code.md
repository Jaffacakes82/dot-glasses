# `HierarchyPath` is a value type in application code; the EF query filter keeps the raw string

`HierarchyPath` is a materialized path of ever-increasing integer segments (`/1/4/12/`) carrying a
load-bearing trailing slash — it is what stops the prefix `/1/4/` matching the unrelated sibling
`/1/40/`. It is a bare `string`, and roughly eight sites across four assemblies split it,
prefix-match it or interpolate it. The prefix comparison flips direction depending on the question
being asked — `data.StartsWith(me)` to find descendants, `me.StartsWith(data)` to resolve an
ancestor — with nothing in the codebase naming which is which. The trailing-slash invariant is
enforced nowhere except one `WidgetExample` DTO regex and the string interpolation that mints new
segments. CLAUDE.md already records ancestor resolution as a standing gotcha "caught twice
independently (Dashboard, Event History) before being treated as a standing rule."

**Decision.** A `HierarchyPath` value type in `Domain` owns the invariant and exposes the two
directions as separately named operations, so a caller has to say which question they are asking.
A companion org-tree lookup module absorbs the two near-identical private `OrgLookup` classes in
`DashboardQueryService` and `EventHistoryQueryService`, and becomes the single definition of
**Retailer** — the nearest `Intermediate`-level ancestor of a retail point (see `CONTEXT.md`),
replacing `CustomOrderService`'s divergent "immediate parent node" resolution, which disagrees
whenever a retail point hangs directly off a Country. `DotGlasses.App` never sees the type;
hierarchy paths are stamped server-side from claims.

**Considered and rejected:** an org-tree lookup module over raw strings, with no new type. It
removes the duplicated lookups and the copied `"Unknown outlet"`/`"Unknown country"` literals, but
leaves the direction-flip bug class alive at every remaining call site — which is the class that
has actually bitten.

**Consequences.** Persistence keeps `string`. The global query filter is assembled by reflection
over the `IHierarchyScoped.HierarchyPath` property (`DotGlassesDbContext`), and deliberately
continues to operate on the raw string column rather than the value type — changing it would mean
moving the expression builder to an `EF.Property<string>` form for no benefit, since the filter
asks exactly one question and asks it correctly. The value type wraps at the application edges, not
at the database. Anyone who later "tidies" the filter to use the value type should read this first.
`CreateWidgetExampleRequest`'s hierarchy-path string field stays a string — it is wire shape, and
`Contracts` may not reference `Domain`.
