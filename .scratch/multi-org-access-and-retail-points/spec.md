# Spec — Multi-org access and retail-point recording

Status: ready-for-agent
Source: wayfinder map `.scratch/ceo-feedback-2026-09-27/` — tickets 01 (How several org
assignments combine into one user's access) and 02 (Recording tests, leads and sales only at retail
points). Decisions: ADR-0006. Vocabulary: `CONTEXT.md` — **Org assignment**, **Scope**, **Current
location**, **Role**, **Retail point**.

## Problem Statement

The CEO is assigned to DGI and to a test retail point. He found that his access "changes to the
lowest" org. The Admin Portal hid Lens Sets, Reference Data and Custom Orders from him, and
opening Custom Orders showed Access Denied. When he tried to remove the retail point assignment,
the Admin Portal refused because it was his "primary org". No screen let anyone change that.

The cause is that every user has one *active* org, and scope and permissions come from that org
alone. Their other assignments are only a list to switch between, and only the Field App can
switch. "Primary org" is a hidden concept: the invite form silently makes the first org in list
order primary, and the rest of the product never explains it.

Two related problems sit alongside it:

- **Records can be made at any level.** Tests, Leads and Sales are stamped with whatever org the
  technician happens to be on, including DGI, a country or a distributor. The business has decided
  records belong only at retail points.
- **Access changes are slow to take effect.** Removing an assignment, changing a role or suspending
  a user doesn't bite until the sign-in cookie refreshes (up to 30 minutes) or the Field App token
  expires (up to 60 minutes). That is a security gap.

## Solution

**Admin Portal.** A user's access is their **Scope**: everything at or beneath *any* of their org
assignments, combined. Being assigned to DGI gives DGI's scope, whatever else the user is assigned
to.
- A screen that needs a minimum level opens on the user's highest assigned level.
- Screens show the whole scope, counting each record once.
- The Organisations tree shows one tree for each separate part of the scope.
- The sidebar shows what the user is assigned to.

**Field App.** The Field App records at one **Current location**. It must be an active **Retail
point** the user is directly assigned to.
- Each device remembers its last location. If only one retail point qualifies, it is chosen
  automatically. Otherwise the technician picks one.
- The location is always on screen.
- A user with no qualifying retail point sees one clear screen saying they can't record from the
  app.

**"Primary org" is removed.** No assignment is special. A user always keeps at least one assignment;
suspending the user is how all access is removed.

**Access is rechecked on every request, on both apps.**
- A removed assignment or a changed role takes effect on the user's next request.
- A suspended user is signed out on their next request.
- Offline records queued for a location the user has since lost are rejected at sync. They land on
  Failed records with a clear message.

**The server enforces where records can be made.**
- A Test, Lead or Sale is accepted only at an active retail point the user is directly assigned to.
- Converting a Lead at a deactivated retail point is refused.

## User Stories

1. As a DGI admin also assigned to a retail point, I want DGI-level access on the Admin Portal, so that adding a retail point never reduces what I can see or do.
2. As an admin assigned to two countries, I want to see both countries' data together on the Admin Portal, so that I don't have to switch between them.
3. As an admin, I want Lens Sets, Custom Orders and Reference Data to open based on my highest assigned level, so that a lower assignment never hides a screen I'm entitled to.
4. As an admin with overlapping assignments (e.g. DGI and a retail point under it), I want every record counted once on the Dashboard, Event History and Custom Orders, so that totals are right.
5. As an admin with assignments in separate parts of the hierarchy, I want the Organisations screen to show each part as its own tree, highest level first and alphabetical within a level, so that I can find everything I manage.
6. As an admin whose assignments nest (e.g. DGI and a retail point under DGI), I want only the outer tree shown, so that the same org doesn't appear twice.
7. As any Admin Portal user, I want my role and assignments shown under my name in the sidebar, so that I can see at a glance what my access is based on.
8. As a user with many assignments, I want the sidebar list shortened with "+N more", so that the nav stays tidy.
9. As any Admin Portal user, I want my email to stay inside the sidebar rather than overflowing into the page, so that the layout isn't broken.
10. As an admin inviting a user, I want to tick any number of orgs without one silently becoming "primary", so that there is no hidden rule.
11. As an admin, I want to remove any of a user's assignments except their last one, so that I can correct assignments freely.
12. As an admin, I want a clear refusal when I try to remove a user's last assignment, telling me to suspend them instead, so that I know how to remove all access.
13. As a country admin, I want to see a user in the User Directory if any of their assignments is in my scope, so that I can see everyone working in my area.
14. As a country admin, I want to be refused when I try to suspend, reset the password of or change the role of a user who also has assignments outside my scope, so that I can't act on someone senior to me.
15. As an admin, I want to add or remove a single assignment of a user as long as that org is in my scope, so that I can manage my part of someone's access without affecting the rest.
16. As a DGI admin, I want a user's removed assignment to stop working on their very next request, so that access is cut straight away.
17. As a DGI admin, I want a role change to take effect on the user's very next request, so that granting or withdrawing admin rights is immediate.
18. As a DGI admin, I want a suspended user signed out of both apps on their next request, so that suspension actually stops them.
19. As a user whose assignments change, I want to stay signed in and simply see my new access, so that a reassignment doesn't interrupt my work.
20. As an admin creating a lens set, I want to choose which of my Country-or-above assignments owns it, so that it belongs to the right org when I have several.
21. As an admin with only one qualifying assignment, I want the owning org chosen automatically, so that I'm not asked a question with one answer.
22. As a technician, I want the Field App to offer only the retail points I'm directly assigned to, so that I can't record somewhere by accident.
23. As a technician signing in on a device that remembers my last location, I want to land straight on it, so that I can start recording immediately.
24. As a technician on a new device, or whose remembered location is no longer assigned to me, I want to be asked to pick one, so that I never record at the wrong place.
25. As a technician assigned to exactly one retail point, I want it chosen for me, so that I'm not asked a question with one answer.
26. As a technician, I want my current location always shown in the Field App header, so that I always know where I'm recording.
27. As a technician, I want every form to say "Recording at <name>" beside its submit button, so that I check the location at the moment I save.
28. As a technician, I want to change my location in Settings, so that I can move between my retail points.
29. As a technician with unsent records, I want switching location to stay blocked until they are sent, so that queued records keep their location.
30. As a technician sharing an account across two devices at two retail points, I want each device to keep its own location, so that they don't overwrite each other.
31. As a DGI or country admin with no retail point assignment, I want the Field App to tell me clearly that I can't record from it and should ask to be assigned to a retail point, so that I understand why rather than seeing a broken form.
32. As that admin, I want a sign-out button on that screen, so that I'm not stuck.
33. As a technician, I want the Leads list and the Lead match on conversion to show only my current location's Leads, so that I work on my outlet's Leads only.
34. As a technician whose assignment was removed while I was offline, I want records I made afterwards to appear on Failed records with a message saying I'm no longer assigned and should ask my admin, so that I know what happened.
35. As that technician, I want to resend those records from Failed records once my admin restores the assignment, so that genuine work isn't lost.
36. As DGI, I want the server to refuse any Test, Lead or Sale whose location isn't an active retail point the user is directly assigned to, so that records only ever sit at retail points, whatever the client sends.
37. As DGI, I want no new records accepted at a deactivated retail point, so that deactivation actually stops activity.
38. As an admin converting a Lead on the Admin Portal, I want a clear refusal when the Lead's retail point has been deactivated, so that I know to reactivate it or record the sale elsewhere.
39. As DGI, I want records at training-org retail points still accepted, so that training works as before (they stay out of Dashboard totals).
40. As DGI, I want to set up a dummy retail point (e.g. "DGI Outreach") as an ordinary retail point, so that head-office distribution follows the same rule as everyone else.
41. As an existing user whose account had an active org before this change, I want that org kept as one of my assignments, so that I don't lose access when the change ships.

## Implementation Decisions

**The current user**
- The current-user abstraction stops reading one org from claims. Per request it provides:
  - the user's **scope paths**: their assignment paths, with any path inside another one removed;
  - their **highest assigned level**;
  - their role;
  - whether they are suspended;
  - on Field App (JWT) requests only, the **current location**: its id and path.
- The scope, level, role and suspension state are loaded from the database once per request and
  memoised for that request. Claims carry the user's identity and, for the Field App, the current
  location id. They are no longer trusted for scope, level, role or suspension.
- **The request's scope depends on the app.**
  - **Admin Portal (cookie) requests** use the user's scope paths.
  - **Field App (JWT) requests** use the current location only. That makes the Leads list, the
    conversion Lead match, reference data and lens-set availability location-scoped with no extra
    code.

**Scoping and permissions**
- **Hierarchy filter.** A row is visible when its `HierarchyPath` starts with *any* of the request's
  scope paths. This is one SQL predicate, for example `LIKE ANY` over an array parameter, so a row
  matches once however many paths cover it. That is what makes the combined figures count each
  record once. Per ADR-0004, the filter stays on the raw string column. The existing empty-prefix
  guard becomes "no scope paths → no rows".
- **Level requirement.** Checks the highest assigned level.
- **Descendant requirement.** Passes when the target path sits under any scope path.
- **A new user-target rule.** Suspend, reset password and change role need *all* of the target
  user's assignment paths to be within the actor's scope. Adding or removing one assignment keeps
  the existing per-org check.
- **User Directory.** Lists a user if any of their assignments is in the actor's scope. This is the
  manual prefix query that already exists for Identity users, now matched against assignment paths.

**Rechecking on every request**
- **Admin Portal.** A cookie event runs on each request. If the user is suspended or no longer
  exists, it signs them out and redirects to sign-in. Otherwise the per-request lookup already
  reflects any assignment or role change.
- **Field App.** A JWT-validated event runs the same lookup. It returns 401 when the user is
  suspended. The current location is valid only if it is still one of the user's direct
  assignments, is Retail Point level and is active. An invalid location doesn't fail
  authentication. It makes the create endpoints refuse, as described next.

**Recording**
- **The rule.** The create endpoints for Tests, Leads and Sales stamp `HierarchyPath` from the
  validated current location. They refuse with a 400 `ValidationProblemDetails` keyed on `""` when
  there is no valid current location. The Field App's outbox already treats that as Rejected, and
  Failed records shows the message. There are three messages:
  - "You're no longer assigned to <name> — ask your admin.";
  - "<name> has been deactivated.";
  - "Choose a retail point before recording."
- **Lead conversion on the Admin Portal** checks that the Lead's retail point is still active
  before creating the Sale. If it isn't, it throws `DomainRuleViolationException` with a message
  naming the deactivated retail point.

**Auth API for the Field App**
- **The "my orgs" call** returns only *eligible locations*: the active, Retail Point level orgs the
  user is directly assigned to.
- **Sign-in** accepts an optional preferred location, the one the device remembers. It issues a
  token carrying that location if it is eligible, or the single eligible location if there is
  exactly one, or no location otherwise.
- **Switch location** issues a new token with the chosen eligible location. It no longer writes
  anything to the user row.

**Schema and user management**
- **Schema change.** Remove the active-org columns from the user (org node id, path, level) and
  their foreign key. The migration first makes sure every user has an assignment row for their
  former active org, so no one loses access.
- **Removing assignments.** Removing the last assignment is refused with a
  `DomainRuleViolationException` telling the admin to suspend the user instead. The old
  "primary org" refusal goes.
- **Invite.** It takes one or more orgs, with no ordering meaning. The "first checked becomes the
  primary/active location" hint is removed from the form.

**Admin Portal screens**
- **Lens set creation** takes an explicit owning org, chosen from the admin's assignments at DGI or
  Country level. The field is shown only when more than one qualifies. The server checks that the
  choice is one of those assignments.
- **The Organisations screen** builds one tree per scope path. Trees are ordered by level (DGI
  first), then by name.
- **The sidebar footer** shows the user's role and assignment names, sorted by level then name and
  shortened to "+N more". The email is truncated with an ellipsis and stays within the nav.

**Field App screens**
- **Remembering the location.** The device keeps the last current location in IndexedDB, alongside
  the cached token, and sends it at sign-in.
- **Picking a location.** With no remembered location and several eligible ones, the app shows the
  outlet picker, which is currently a placeholder and becomes real.
- **The no-location screen.** With no eligible locations, the app shows a "can't record here"
  screen with sign-out.
- **Showing the location.** The header shows the current location's name, and every consultation
  form shows "Recording at <name>" beside its submit button.
- **Unchanged:** location switching is still blocked while the outbox holds unsent records, and
  sign-out is still blocked the same way.

**Other**
- **Existing records.** Records already stamped above retail-point level are left untouched.
- **Dev seeding.** The dev seeder and test fixtures seed assignments instead of active-org columns.
  Seed at least one user with nested assignments and one with assignments in separate trees.
- **CLAUDE.md** is updated in the same change:
  - Data scoping describes the scope as a union and the Field App as current-location scoped.
  - The RBAC section says permissions use the highest assigned level and a check against any
    scope path.
  - The ApplicationUser/claims description is updated.
  - The offline-sync "known accepted risk" paragraph is updated: records are now also refused if
    the location is no longer assigned.

## Testing Decisions

- **What a good test is.** It drives the app from outside: HTTP requests to the Admin Portal screens
  and the Field App API, against a real containerised Postgres. It asserts on what the user or
  client sees: status codes, redirects, rendered screen content and response bodies. It never
  asserts on internals such as claims, service calls or query shape, and no mocking library is
  used.
- **One automated seam: `DotGlasses.Web.Tests`**, on the shared `WebApplicationFactory` fixture.
  Tests sign in as seeded users with several assignments. Prior art:
  - access-control policy tests (screens gated by policy, redirect to Access Denied);
  - conversion source scoping API tests (field-keyed refusals on the create endpoints);
  - lens-set availability API tests (location-dependent API behaviour);
  - the lens-set retire/reactivate screen tests (Admin Portal POST-redirect-GET).
- **Cases to cover:**
  - Combined scope on a screen: a DGI-plus-retail-point user sees DGI-wide data. An overlapping
    record is counted once on the Dashboard.
  - Level gating on the highest level: a Country-plus-retail-point user reaches Custom Orders and
    Lens Sets.
  - The Organisations screen shows one tree per separate part of the scope, in order.
  - User management:
    - seeing a user needs any assignment in scope;
    - suspending, resetting a password or changing a role needs all of the user's assignments in
      scope;
    - removing the last assignment is refused;
    - invite has no primary.
  - Instant recheck:
    - remove an assignment directly in the database, and the next request no longer shows that
      scope;
    - change the role, and the next request reflects it;
    - suspend, and the next Admin Portal request redirects to sign-in and the next API request
      returns 401.
  - Create endpoints refuse a record when:
    - there is no current location;
    - the current location is not Retail Point level;
    - the retail point is assigned only indirectly;
    - the assignment has been removed since the token was issued;
    - the retail point has been deactivated.

    They accept one at a training-org retail point.
  - "My orgs" returns only directly assigned, active retail points.
  - Sign-in with a remembered location that is still eligible, no longer eligible, or where only one
    location exists.
  - Admin Portal Lead conversion at a deactivated retail point is refused with its message.
  - Lens set creation with an explicit owning org. A choice outside the admin's DGI/Country
    assignments is refused.
  - Migration: an existing user's former active org exists as an assignment after upgrade.
- **Field App behaviour is checked by hand in the browser.** There is no Field App test project, and
  its new logic is a thin layer over the server's answers. Check each of these:
  - it remembers the location per device;
  - it chooses automatically when there's only one;
  - the picker appears;
  - the "can't record here" screen appears;
  - the header and "Recording at";
  - the Failed records message after a lost assignment;
  - the switch stays blocked while the outbox holds unsent records.
- **`Application.Tests` stays dependency-free.** Any pure helper, such as collapsing nested scope
  paths, may be unit-tested there with no fakes beyond the existing hand-written ones.

## Out of Scope

- **How the User Directory's invite and edit forms present these rules.** That includes editing a
  user's role and assignments in place, and the misaligned org checkboxes. They are covered by the
  open ticket "Editing a user's role and org assignments" (map ticket 03). This spec supplies only
  the rules those forms must enforce, and the invite form's "primary" hint removal.
- **Assigning several users to an org at once** (map ticket 04).
- **One role per assignment.** A user keeps one role; this may be revisited.
- **Narrowing filters on the Dashboard**, for example by country (map ticket 21).
- **A marker for dummy or outreach retail points.**
- **Migrating or cleaning up existing records stamped above retail-point level.**
- **Remembering a default lens set** (map ticket 19).
- **Anything from the lens redesign** (ADR-0007; a separate spec will follow). This spec's lens-set
  change is only the owning-org choice at creation.

## Further Notes

- **Tokens issued before this ships** carry no current location. Records queued under one will be
  refused as "Choose a retail point before recording" and land on Failed records. They can be
  resent after picking a location. The product isn't live, so this is accepted rather than bridged.
- **ADR-0006 is the reasoning of record.** Read it before changing the scope model, especially the
  rejected option of one active org with a switcher.
- **Per-request cost.** The per-request lookup adds one small query per request, reading the user's
  assignments joined to their orgs, plus the role and lockout. It is memoised within the request.
  Don't cache it across requests. Container Apps runs several replicas, and a cross-request cache
  would reintroduce the delay this spec removes, the same reasoning ADR-0002 gives for reference
  data.
- **Map follow-ups.** Once this ships, the CEO's retest should confirm that the "Custom order page
  doesn't open" item from the feedback doc is gone. That item was this bug.
