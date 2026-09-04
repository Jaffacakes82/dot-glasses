# 06 — Reporting adopts the hierarchy module and the Retailer definition

**What to build:** The Dashboard, Event History and Custom Orders all resolve outlet, Retailer and
country through the one lookup module, so an admin sees the same answer wherever they look. Custom
Orders stops resolving a Retailer as "the retail point's immediate parent", which disagrees with the
glossary definition whenever a retail point sits directly under a Country.

**Blocked by:** 05 — `HierarchyPath` value type and org lookup module.

**Status:** ready-for-agent
**Category:** bug

- [ ] Custom Orders groups by the glossary's Retailer definition
- [ ] A retail point whose nearest ancestor is a Country is reported as having no Retailer, on every screen that shows one
- [ ] Dashboard and Custom Orders report the same Retailer for the same retail point
- [ ] The two duplicated private org-lookup implementations are gone
- [ ] Outlet and country names still resolve correctly for a caller scoped below the level being resolved
- [ ] Existing reporting output is otherwise unchanged
