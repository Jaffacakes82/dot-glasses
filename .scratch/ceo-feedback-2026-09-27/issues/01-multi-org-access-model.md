# 01 — How several org assignments combine into one user's access

Type: grilling
Status: resolved
Blocked by: None

## Question

A user can be assigned to several orgs, but today only one of them is *active*: scoping and every
permission check read `ApplicationUser.OrgNodeId`/`HierarchyPath`/`OrgLevel`, and
`UserOrgAssignment` is just the list the user may switch between. What should a user with several
assignments actually be able to see and do, on the Admin Portal and on the Field App?

Sub-questions to settle together:
- Union of all assigned subtrees, or one active org at a time with a switcher on both apps?
- One role per user (today) or one role per assignment (e.g. Admin at Kenya, User at a retail point)?
- Does the "primary org" concept survive at all? The invite form's "first checked becomes the
  primary/active location" and the "Can't un-assign a user's primary org" refusal both come from it.

## Context

- Feedback doc, Admin Portal: "user level of access changes to lowest when added to other orgs".
- Feedback doc: "Custom order page doesn't open" — the screenshot is the Access Denied page; the
  user's active org was a retail point, below `CustomOrders.View`'s Country+ rule. Same root cause.
- Call: removing the retail-point assignment was refused as "primary", with no way to change it.
- Code facts: invite makes `orgNodeIds[0]` (form order, not click order) active
  (`UserAdminService.InviteAsync`); `AssignUserToOrgAsync` never changes the active org;
  `UnassignUserFromOrgAsync` refuses the active org; only the Field App has a switcher
  (`AuthController.SwitchOrg`, `Settings.razor`); the Admin Portal has none.

## Answer

Grilled 2026-09-28. Recorded in [ADR-0006](../../../docs/adr/0006-admin-portal-scope-is-the-union-of-assignments.md). The
`CONTEXT.md` terms are **Org assignment**, **Scope**, **Current location** and **Role**. This also
resolves the "Custom order page doesn't open" item.

**Model**
- **The Admin Portal uses the user's scope:** everything at or beneath *any* of their assignments,
  combined. Assigning someone to DGI gives them DGI's scope. (Q1)
- **The Field App records at one current location**, chosen from the user's assignments. Which orgs
  qualify is decided in ticket 02. (Q1)
- **One role per user**, applying across their scope. This may become per-assignment later. (Q2)
- **"Primary org" is removed entirely.** The invite form's "first checked" rule goes, and the user
  row stops holding an active org. Any assignment can be removed except the last; suspend a user to
  remove all their access. (Q3, Q6)

**Field App location**
- **Each device remembers its own last location.** With nothing remembered, or if the remembered
  location is no longer assigned, the user picks one. If only one location qualifies, it's chosen
  automatically. Settings still lets them change it. (Q4, Q13)
- **The location is always visible.** The Field App header shows it, and every form says
  "Recording at <name>" by its submit button. Switching stays blocked while unsent records exist.
  (Q5)
- **The Leads page and the conversion Lead-match use the current location only**, not the whole
  scope. (Q11)

**Admin Portal**
- **Level-gated screens open on the user's highest assigned level.** What they then show is the
  whole scope. (Q7)
- **Dashboard, Event History and Custom Orders combine the whole scope**, counting each record once
  where assignments overlap. Narrowing filters belong to ticket 21. (Q9)
- **The Organisations screen shows one tree for each separate part of the scope.** Trees are stacked
  highest level first, then alphabetically. An assignment inside another assignment's tree doesn't
  get its own. (Q10, Q16)
- **Managing other users:**
  - An admin can *see* a user if any of that user's assignments is in the admin's scope.
  - The admin can *suspend* them, *reset their password* or *change their role* only if **all** of
    the user's assignments are in scope.
  - The admin can *add or remove one assignment* if that org is in scope. (Q12)
- **A new lens set's owning org** is picked at creation from the admin's assignments at Country
  level or above, and chosen automatically if there's only one. The lens redesign may revisit this.
  (Q17)
- **The sidebar footer lists the user's assignments** under their name and role, shortened with
  "+N more" when long. The same change fixes the user's email overflowing out of the nav into the
  main content. (Q18)

**Security**
- **Changes take effect on the user's next request, on both apps.** The server rechecks
  assignments, role and suspension on every request instead of trusting the sign-in claims.
  Assignment and role changes re-scope the user without signing them out; suspension signs them
  out. (Q8, Q14)
- **Offline records for a location the user has since lost are rejected at sync.** They appear on
  Failed records with a clear message, and can be resent if the assignment is restored. The server
  has no trustworthy creation time for them. (Q15)

**Handed on**
- Ticket 02: which orgs can be a current location, and whether retail points beneath an assigned
  org count as well.
- Ticket 03: the invite and edit forms without "primary", "at least one assignment" in the UI, and
  the misaligned org checkboxes.
