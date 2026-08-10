# DOT Glasses — Functional Capability Review

**What this is:** a reverse-engineered functional specification of what the DOT Glasses platform
actually does today, derived **exclusively from application source code** (controllers, views,
Razor components, services, validators, authorization handlers, domain entities and seed data).
No `.md` file, design mockup or planning document was used as a source of requirements.

**Reviewed at:** commit `f0314c1`, 2026-08-07.

**Scope:** the Admin Portal (`DotGlasses.Web`, server-rendered MVC), the Field App
(`DotGlasses.App`, Blazor WebAssembly PWA), and the v1 REST API that joins them.

---

## 1. How to read this

Capability in this system is a function of **two independent things**: a user's *role*
(Admin / Manager / User) and the *organisation level* they are attached to (DGI / Country /
Intermediate / Retail Point). A Manager at Country level and a Manager at a Retail Point see
materially different products. This document therefore works in role × level personas rather
than in three role sections.

Every section is written from the end user's point of view: what appears on the screen, what
they can do with it, exactly which fields exist and what the system will and won't accept.
Each screen ends with a **Not built** subsection — capabilities a user demonstrably cannot
perform today. Those are statements of current fact, not criticism; several are deliberate.

---

## 2. The access model

### 2.1 Two independent mechanisms

The system separates **what rows you can see** from **what you can do with them**, and the two
never touch.

**Data scoping (visibility)** is a global EF Core query filter applied automatically to every
entity carrying a hierarchy path — `OrganisationNode`, `Customer`, `Test`, `Lead`, `Sale` and
`WidgetExample`. The rule is: *a row is visible if its `HierarchyPath` starts with the signed-in
user's own `HierarchyPath`*. This is completely role-independent. A DGI Admin, a DGI Manager and
a (hypothetical) DGI-level User all see identical data. Scoping is downward only — you see your
own node and everything beneath it, never anything above or beside you.

Four entities are **not** hierarchy-scoped and are therefore globally visible to every
authenticated user: `ReferenceDataItem`, `PresetCatalogue`, `LensOption` and
`LensStrengthCoatingOption`. Reference data and catalogues are a single global library, not a
per-country one. Note also that `ApplicationUser` is an Identity type, outside the automatic
filter entirely — the User Directory applies the same prefix rule manually in code.

Because reporting screens need to resolve a row's *ancestor* names (which country is this outlet
in?), and ancestors are by definition invisible under the downward-only filter, three services —
Dashboard, Event History and the Field App's catalogue lookup — deliberately read the org tree
through an explicit unscoped query instead.

**RBAC (permissions)** is separate, policy-based, and evaluated per action.

### 2.2 Roles and org levels

Three roles exist and are the only ones assignable: `Admin`, `Manager`, `User`. A user holds one
role, at one primary organisation node.

Four org levels, ordered: `Dgi` (0) → `Country` (1) → `Intermediate` (2) → `RetailPoint` (3).
"At or above Country" means level ≤ 1, i.e. DGI or Country. Only DGI, Country and Retail Point
carry business rules; every reseller/distributor tier in between is `Intermediate`, distinguished
only by a free-text `Kind` display label.

The tree's shape is enforced: DGI's only legal child is a Country; a Country or Intermediate may
have Intermediate or Retail Point children; a Retail Point is always a leaf.

### 2.3 The six authorization policies

| Policy | Rule as coded | Gates |
|---|---|---|
| `ReferenceData.Manage` | Role = **Admin** AND level = **DGI exactly** | The whole Reference Data screen |
| `PresetCatalogue.Manage` | Role ∈ {Admin, Manager} AND level ≤ **Country** | The whole Preset Catalogues screen |
| `CustomOrders.View` | **Any role** AND level ≤ **Country** | The whole Custom Orders screen, *and* its Advance-status action |
| `Organisations.ManageInScope` | Role ∈ {Admin, Manager} AND target org's path is at/below the caller's | Every Organisations write action, per node |
| `Users.ManageInScope` | Role ∈ {Admin, Manager} AND target user's path is at/below the caller's | Every User Directory write action, per user |
| `WidgetExample.Create` | Role ∈ {Admin, Manager} (no level or scope check) | The developer sandbox API only |

Two design points worth stating explicitly, because they are deliberate:

- The two `...InScope` policies check the **caller's** role but never the **target's**. A Manager
  can suspend, reset, or reassign an *Admin* who sits below them in the tree.
- `CustomOrders.View` is the only policy where a plain `User` gets access to something a Manager
  below Country level does not — a `User` attached at Country level can advance fulfilment status,
  a `Manager` attached to a Retail Point cannot even see the queue.

Dashboard, Organisations, Event History and User Directory carry only a bare `[Authorize]` —
every authenticated user reaches them, and what they see is narrowed by data scoping rather than
by policy.

### 2.4 The eight personas

| # | Persona | Realistic description |
|---|---|---|
| **P1** | Admin @ DGI | Super admin. The only persona that can edit reference data. |
| **P2** | Admin @ Country | Country office lead. Everything except reference data. |
| **P3** | Admin @ Intermediate | Retailer/distributor admin. Loses catalogues and custom orders. |
| **P4** | Admin @ Retail Point | Outlet admin. Same as P3 but with a single-node view. |
| **P5** | Manager @ Country | Functionally identical to P2 in this codebase. |
| **P6** | Manager @ Intermediate / Retail Point | Identical to P3/P4 in this codebase. |
| **P7** | User @ Retail Point | The field technician. Read-only MI; the Field App is theirs. |
| **P8** | User @ Country or DGI | Unusual but creatable via the invite form. Read-only MI **plus** the full Custom Orders queue. |

**Admin and Manager are functionally identical today.** Every policy that admits one admits the
other, with the single exception of `ReferenceData.Manage` — and that is gated on *level* (DGI),
not role, so it separates P1 from P5 only because no Manager is expected at DGI. If a Manager were
created at DGI they would be blocked from Reference Data; that is the only behavioural difference
between the two roles anywhere in the system.

---

## 3. Persona × surface capability matrix

`●` = full access · `◐` = visible but scoped/partial · `○` = reachable but no write actions ·
`✕` = blocked

| Surface | P1 Admin@DGI | P2/P5 @Country | P3/P4/P6 @Interm./RP | P7 User@RP | P8 User@Country+ |
|---|---|---|---|---|---|
| **Dashboard** | ● all data | ◐ own subtree | ◐ own subtree | ◐ own outlet | ◐ own subtree |
| **Organisations** — view tree | ● whole tree | ◐ own subtree | ◐ own subtree | ◐ own node only | ◐ own subtree |
| **Organisations** — add child / toggles / assign users | ● | ● in scope | ● in scope | ✕ (403) | ✕ (403) |
| **Event History** (4 tabs) | ● all | ◐ own subtree | ◐ own subtree | ◐ own outlet | ◐ own subtree |
| **User Directory** — view | ● all users | ◐ subtree users | ◐ subtree users | ○ subtree users | ○ subtree users |
| **User Directory** — invite / reset / suspend | ● | ● in scope | ● in scope | ✕ (403) | ✕ (403) |
| **Preset Catalogues** | ● | ● | ✕ (403) | ✕ (403) | ✕ (403) |
| **Custom Orders** — view + advance | ● | ● | ✕ (403) | ✕ (403) | ● |
| **Reference Data** | ● | ✕ (403) | ✕ (403) | ✕ (403) | ✕ (403) |
| **Field App** — record Test/Lead/Sale | ● (stamped to DGI root) | ● (stamped to their node) | ● | ● | ● |

Two consequences of that last row are worth flagging: **any** authenticated user can record a
Test, Lead or Sale through the API — there is no role or level restriction on the write
endpoints — and the record is stamped with whatever org the caller sits at. A DGI Admin recording
a Sale produces a Sale attached to the DGI root node, which then resolves as "Unknown outlet" and
"Unknown country" on every reporting screen.

**Navigation does not reflect permissions.** The Admin Portal sidebar renders all seven links to
every authenticated user. A Retail Point User clicking "Reference Data" is refused, and cookie
authentication redirects them to `/Account/AccessDenied` — an action and view that **do not
exist**, producing a bare 404 rather than an access-denied page.

---

## 4. Admin Portal — screen by screen

### 4.1 Sign-in and account pages

**Login** — `/Account/Login`, anonymous.

Two fields, both required client-side: `UserName`, `Password`. Optional `returnUrl`, honoured only
if local. Sign-in is persistent (a lasting cookie) and counts failures toward lockout. On success
`LastLoginUtc` is stamped and the user lands on the Dashboard or their return URL. On any failure
— wrong password, unknown user, or a suspended account — the same message appears: *"Invalid
username or password."* Suspension is deliberately not distinguishable from a bad password.

**Set password** — `/Account/SetPassword?userId=…&token=…`, anonymous.

The target of an invite or reset link. Fields: hidden `UserId`, hidden `Token`, `Password`. The
token is a genuine ASP.NET Identity password-reset token. Password rules as configured: minimum
8 characters, at least one digit, **no** uppercase or non-alphanumeric requirement. On success the
account's `EmailConfirmed` is set to true and the user is redirected to Login with "Password set
— you can now log in." An invalid or expired token surfaces Identity's own error text.

**Not built**
- **There is no sign-out anywhere in the Admin Portal.** A `Logout` POST action exists but no view
  posts to it and no button renders. Ending a session requires clearing cookies.
- No self-service "forgot password" — a reset can only be initiated by an admin from the User
  Directory.
- No password change for a signed-in user, no MFA, no account lockout feedback.

---

### 4.2 Dashboard (MI Reporting)

**Route** `/` · **Access** any authenticated user · **Data** automatically scoped to the viewer's
subtree.

Six stat tiles across the top:

| Tile | Exactly what it counts |
|---|---|
| Pending leads | Leads where `ConvertedFlag` is false |
| Total tests | All visible Tests |
| Standard sales | All visible Sales *minus* custom orders |
| Custom orders | Sales where `FulfilmentStatus` is set (i.e. created with "order from DOT Glasses") |
| Test-to-sale conversion | % of all Tests that reached a Sale, walking `Test.ConvertedToLeadId` → `Lead.SaleId` |
| Needed-to-sale conversion | Same numerator rule, but the denominator is only Tests with outcome *Needs glasses* |

There is no direct Test → Sale link in the data model, so **both conversion figures only count
Tests that were converted via the Lead route.** A technician who records a Test and then records
a Sale separately produces no conversion, by design of the data model as built.

Right-hand column, three cards:
- **Referrals logged** — count of Tests with outcome *Referred*, plus a pointer to Event History.
- **Conversion trend (last 6 weeks)** — six bars, each a rolling 7-day window ending at "now",
  showing that window's test-to-sale conversion %. Bar height is the percentage; the tooltip is the
  raw number. No axis, no dates, no labels.
- **Gender split** — a two-segment bar computed from `Test.Gender` only. Only Female and Male exist
  in the domain; there is no third value or "unspecified".

Main card, **Top performing** — four fixed top-5-by-sales-volume lists, each row showing a name,
its sale count and its own conversion % (that key's Sales ÷ that key's Tests):
- **Top outlets** — exact hierarchy-path match on the org node.
- **Top retailers** — the nearest `Intermediate`-level ancestor (longest matching path prefix).
- **Top countries** — the `Country`-level ancestor.
- **Top technicians** — the recording user's full name, falling back to their username.

Unresolvable names appear as "Unknown outlet" / "Unknown retailer" / "Unknown country" rather than
failing. If all four lists are empty the whole card collapses to "No sales recorded yet."

**Organisations flagged `IsTrainingOrg` are excluded from every figure on this page** — the tile
counts, the conversions, the trend, the gender split and all four rankings. Training exclusion is
applied on this screen only.

**Not built**
- No date range, country, outlet or role filters — every figure is all-time and unfiltered.
- No drill-down: nothing on this page is clickable.
- No export (CSV/PDF), no scheduled or emailed reports.
- No retail-point-type distribution — no such taxonomy exists in the domain.
- Per-key conversion % can exceed 100% (a Sale recorded where no Test was, at that key).
- Training-org exclusion is *not* applied to Event History, Custom Orders or the User Directory's
  sales counts — those still include training data.

---

### 4.3 Organisations

**Route** `/Organisations`, `?selectedId=` to select a node · **Access** any authenticated user
can view; write actions require `Organisations.ManageInScope`.

**Left panel — the tree.** Rendered recursively with 24px indentation per level, a coloured dot
per level (DGI black, Country blue, Intermediate orange, Retail Point green), the node name, a
yellow "Training" badge where applicable, and the level name right-aligned. Children sort
alphabetically. Every node is a link that selects it.

Because reads are scoped downward only, **your own node becomes the displayed root** — a Kenya
Manager sees Kenya as the top of the tree with DGI absent entirely, not greyed out. A Retail Point
user sees a single-node "tree" consisting of themselves.

**Right panel — the selected node.** Shows the level badge, a training badge if flagged, the name,
the free-text `Kind` label, a Retail-Point-only note that stock is tracked externally in Zoho, and
a list of users assigned to this node. If the viewer fails the scope check the panel says "You
don't have permission to manage this node." and no actions render.

**Actions** (all re-checked server-side; hidden buttons are never trusted alone):

1. **Flag / unflag as training organisation** — one-click toggle, available at any level. Excludes
   the node and everything beneath it from Dashboard aggregates.
2. **Enable / disable custom orders** — one-click toggle, **rendered only for Country-level nodes**,
   and the service rejects the call outright for any other level.
3. **Add child node** — a modal. Fields: `Name` (required, ≤ 200 chars), `Kind` (optional free-text
   display label, ≤ 100 chars), `Level` (a select only when more than one level is legal; a hidden
   field with an explanatory line when exactly one is; the button is hidden entirely for Retail
   Points). Level legality is enforced three times over — in the UI, in the validator, and in the
   service. The new node's hierarchy path is minted as *parent path + (global maximum path segment
   across the whole tree + 1)*, so segments are globally unique and ever-increasing rather than
   per-parent.
4. **Assign users** — a modal with a single-select dropdown of every user in the caller's own
   scope. Creates a `UserOrgAssignment` row; re-submitting the same pair is a silent no-op. This
   **does not** change the user's primary organisation, so it does not change what they can see or
   which org their records are stamped with.

**Not built**
- No rename, no edit of `Kind` or level after creation.
- No delete, deactivate or archive of an organisation node.
- No move/re-parent.
- No un-assign — a user assigned to a node cannot be removed from it through any screen.
- Assigning a user has no functional effect yet (see §9).
- The "Assigned users" list matches users to the node **by org name string**, not by ID — it is
  correct only while org names remain unique.
- Path-segment minting is read-max-then-increment with no locking; concurrent creates could
  collide.
- Users created by the developer seeder have no `UserOrgAssignment` rows, so they never appear in
  any node's "Assigned users" list despite having a primary org.

---

### 4.4 Event History

**Route** `/EventHistory?tab=…&search=…&page=…` · **Access** any authenticated user · **Data**
automatically scoped to the viewer's subtree. Four tabs, 25 rows per page.

**Sales tab** — columns: Type badge (green "Sale", suffixed "· Custom" when the lens range was
Custom), customer name, outlet, country, absolute local timestamp (`yyyy-MM-dd HH:mm`). Newest
first.

**Tests tab** — same five columns, blue "Test" badge. **The Name column is always "—"**: `Test`
carries a `CustomerId` field but nothing in the application ever writes to it, including the
Test → Lead conversion flow. Tests are effectively anonymous records.

**Leads tab** — columns: name, masked phone, outlet, reason not purchased, relative "Logged" time
("just now", "N minutes/hours/days ago", falling back to an absolute date beyond a week). Phone
masking keeps the first 4 and last 3 characters and replaces the middle with a fixed 4-character
redaction; numbers of 7 characters or fewer are shown unmasked, and a missing number shows "—".
Includes a search box filtering on customer full name; the filter is applied at the database level
*before* paging, so page numbers stay meaningful. The match is a substring `LIKE`, which on
PostgreSQL is **case-sensitive**.

**Referrals tab** — columns: outlet, country, reason, absolute time, preceded by a note that these
are tracked for government reporting. This is a filtered view of the same Tests data
(outcome = *Referred*), not a separate record type — a referred test correctly appears in both
tabs.

Reference-data labels (referral reason, reason not purchased) resolve against **all** reference
items including retired ones, so a historical event referencing a since-retired option still
displays its label rather than blanking. Where the chosen option was the category's "Other" row,
the row's own free-text is shown in place of the generic "Other".

Paging shows "Page X of Y (Z total)" with Previous/Next links only, and only when there is more
than one page. Switching tabs resets to page 1; the search term is carried across page links.

**Not built**
- No date, outlet, country, technician or event-type filters.
- Search exists on the Leads tab only, and is case-sensitive.
- No export.
- No row detail view — you cannot open the underlying Test, Lead or Sale from here.
- No action on any row (e.g. no "convert this lead to a sale").
- The Tests tab's Name column can never be populated.
- Training-org data is included here (unlike the Dashboard).

---

### 4.5 User Directory

**Route** `/UserDirectory` · **Access** any authenticated user can view the list; every write
action requires `Users.ManageInScope` against that specific user. Listing applies the hierarchy
prefix filter manually, since `ApplicationUser` is outside the automatic query filter.

**The table** — Name, Role, Scope, Last login, Sales, Status, actions.

- **Name** is `FullName`, falling back to username where absent.
- **Scope** is a set of badges listing the org names from the user's `UserOrgAssignment` rows. A
  user with a primary org but no assignment rows shows an empty Scope column.
- **Last login** is stamped on both sign-in paths — the Admin Portal cookie login and the Field
  App's API login — so it is populated for field technicians who never open the portal. Shows "—"
  if never.
- **Sales** counts `Sale` rows recorded by that user.
- **Status** is derived, never stored: **Invited** if the account has no password hash at all;
  otherwise **Suspended** if the lockout end date is in the future; otherwise **Active**.

**Actions per row**, shown only where the viewer passes the scope check:

1. **Reset password** — generates a fresh Identity reset token and a `/Account/SetPassword` link,
   calls the email sender, and displays the link on screen. Does *not* clear the existing password;
   the old one keeps working until the link is used.
2. **Suspend / Unsuspend** — implemented with Identity's own lockout mechanism (lockout end set to
   maximum, or cleared). A suspended user is refused at both the portal and the API login, with
   the generic invalid-credentials message.

**Invite platform user** — a modal, opened from a button that renders for *every* viewer
regardless of permission (the refusal happens on submit). Fields:

| Field | Rules |
|---|---|
| Full name | Required, ≤ 200 characters |
| Email | Required, valid email format, ≤ 256 characters, must not already exist |
| Hierarchy scope | A scrollable checkbox list of every org in the caller's own scope. At least one required; every selection re-validated as in-scope server-side. **The first checked box becomes the primary/active location.** |
| Role | Select: Admin / Manager / User, defaulting to User |

On submit the system creates the account **with no password at all** (which is what "Invited"
means), adds a `UserOrgAssignment` row for every selected org, stamps the *first* selection as the
primary org — writing `OrgNodeId`, `HierarchyPath` and `OrgLevel`, which become the user's sign-in
claims and therefore drive everything they can see — generates a reset token, and builds a
set-password link.

**Email delivery does not exist.** The email sender is a stub that writes to the log. The link is
displayed once in a green banner on the page, with the text "No email sending is wired up yet —
copy this set-password link and send it to *[email]* yourself". Reloading the page loses it; the
only recovery is Reset password, which mints a new one.

Only the *first* selected org is checked for scope in the controller before inviting; the
validator separately checks that all of them are in scope, so the combination is safe, but the two
checks use different policies.

**Not built**
- No edit of a user's name, email, role or org assignments after invite.
- No delete or deactivate (only suspend).
- No way to change which of a multi-org user's locations is primary.
- No un-assign from an org.
- No search, filter, sort or paging — the full in-scope list renders every time.
- No resend of an existing invite without invalidating it.
- No bulk invite or import.
- The invite button is shown to users who cannot invite.

---

### 4.6 Preset Catalogues

**Route** `/Catalogues` · **Access** `PresetCatalogue.Manage` — Admin **or** Manager, at DGI or
Country level. Everyone else gets a 403 (rendered as a 404, see §3).

Catalogues themselves are **not** hierarchy-scoped: every user who reaches this page sees every
catalogue in the system, regardless of who owns it.

**Catalogue cards** — one per catalogue, showing name, description, "Diopter range: …", and
"Assigned to N org(s)". Below that, the lens roster as removable badges, and an add-lens form.

- **Create package** / **Edit** — a modal with three fields: `Name` (required, ≤ 200),
  `Description` (≤ 500), `Diopter / strength range` (a free-text label, ≤ 100). On create, the
  owning org is stamped from the caller's own primary org, never submitted by the client; the
  service rejects the create if that org isn't DGI or Country level.
- **Add lens** — a dropdown of every active `LensStrength` reference item plus an Add button. The
  chosen strength is appended at the end of the catalogue's sort order. A catalogue's lens roster
  is therefore *"which curated strength labels are included, in what order"* — the actual power and
  bifocal-ness live in the reference item's own label (e.g. `+2.50`, `+0.00 / +2.50 (Bifocal)`).
- **Remove lens** (the × on each badge) — a **hard delete**, not a retire. This is safe because no
  Test, Lead or Sale can reference a lens option that was never chosen on a real transaction.

**Assign packages to a retailer** — a form with a single-select org dropdown (restricted to
`Intermediate` and `RetailPoint` nodes only), a multi-select catalogue list, and an Assign button.
Each selected catalogue produces one assignment; re-assigning an existing pair is a silent no-op.
**Assignment cascades downward** — assigning to an Intermediate makes the catalogue available to
every Retail Point beneath it.

**Lens strength coating availability** — a grid with active lens strengths as rows and active
coatings as columns. Each cell is a one-click ✅/⬜ toggle recording "this strength can be sold in
this coating". This grid directly drives the Field App: a technician choosing a preset lens is
offered exactly the coatings ticked here, and a strength with **no** coatings ticked cannot be
sold on a preset range at all — the Field App shows an explanatory message instead of an empty
dropdown, and the API rejects the sale. If no active Coating reference items exist, the whole grid
is replaced with a pointer to the Reference Data screen.

**Not built**
- No delete or archive of a catalogue.
- No un-assign — a catalogue assigned to an org cannot be removed from it.
- No visibility of *which* orgs a catalogue is assigned to; only a count.
- No reordering of lenses within a catalogue.
- No duplicate guard — the same lens strength can be added to a catalogue repeatedly, and will
  appear repeatedly in the Field App's picker.
- Catalogues cannot be assigned to Country or DGI nodes from this screen (only Intermediate and
  Retail Point), even though the seeded assignments are at Country level and the cascade logic
  supports it.
- The Field App maps its "6-Lens Set" / "9-Lens Set" buttons to catalogues **by name substring**,
  so a third catalogue is invisible to technicians unless it is named to match.

---

### 4.7 Custom Orders

**Route** `/CustomOrders` · **Access** `CustomOrders.View` — **any role**, at DGI or Country level
only. Hidden entirely below that. The same policy gates both viewing and advancing status.

The queue lists every Sale with a fulfilment status set — that is, every Sale recorded as a Custom
prescription with "Order this lens from DOT Glasses" ticked. Newest first.

Columns: **Customer** (name, or "—"), **Source outlet** (resolved from the sale's hierarchy path,
or "Unknown outlet"), **Prescription** (a formatted string, `OD <right> / OS <left>`, each eye
showing sphere and, where non-zero, `cyl` and `add`), **Status** badge, and the advance action.

**Advance status** is a single button labelled with the next state. The flow is linear and
forward-only: **Submitted → In Lab → Ready for Pickup → Fulfilled**. Status is set to *Submitted*
automatically at the moment the sale is created. Once Fulfilled the button disappears, and the
service refuses any further advance. There is no way to set an arbitrary status.

Empty state: "No custom orders yet."

**Not built**
- No revert, cancel, or reject.
- No search, filter, or paging — the entire queue renders in one table.
- No notes, expected dates, lab assignment, or tracking references.
- No notification back to the originating outlet or technician; the Field App never learns that an
  order progressed.
- The prescription string omits axis and pupil distance, both of which are captured on the sale.
- No link from a queue row back to the underlying Sale record.
- The `CanHandleCustomOrders` flag on Country nodes does not gate this queue (see §9).

---

### 4.8 Reference Data

**Route** `/ReferenceData` · **Access** `ReferenceData.Manage` — **Admin at DGI only.** The single
most restricted screen in the product.

Seven category cards in a fixed display order, each with an explanatory scope note:

| Category | Where the values are consumed |
|---|---|
| Reasons not purchased | Field App Lead form (required) |
| Referral reasons | Field App Test form when outcome is *Referred* |
| Coatings & tints | Lead coating preference, Sale coating, and the Preset Catalogues availability grid |
| Frame colors | Sale frame-colour swatches |
| Hard case colors | Sale, when a hard case is sold |
| Occupations | Optional on Test, Lead and Sale |
| Lens strengths | Building preset catalogue rosters |

**Each card shows** its active options as chips — with a circular 18px thumbnail where an image URL
is set, on the Frame colors card only — each carrying a × to retire it, and a collapsed
"Retired (N)" section with a Restore link per option.

**Removing an option retires it; it is never deleted.** Historical Tests, Leads and Sales may
reference it by ID, and Event History deliberately resolves labels against retired items too.
Retired options disappear from every Field App dropdown immediately but remain restorable.

**The add form** on each card:

| Field | Rules |
|---|---|
| Label | Required, ≤ 200 characters. The machine code is auto-slugified from it (lowercased, non-alphanumeric runs → hyphens). Sort order is assigned as (current maximum in category + 1). |
| Image URL | ≤ 2000 characters. **Rendered on the Frame colors card only.** A pasted URL — there is no upload. |
| "Mark as this category's Other option" | A checkbox. Disabled with the note "(already set — retire it first)" when the category already has an active Other option, and independently enforced server-side. |

The Other flag matters functionally: every consuming dropdown in the Field App keys off it to
reveal a free-text "please specify" field, and the API requires that free text whenever an
Other-flagged option is chosen. Two active Others in one category would be ambiguous, hence the
one-per-category rule.

**Out of the box** the system seeds: 12 Occupations, 9 Reasons not purchased, 6 Referral reasons,
5 Coatings (Photochromic, Clear, Blue block, Polarized, Sunglasses), 7 Frame colors, 3 Hard case
colors, and 16 Lens strengths (12 standard plus 4 bifocal). Each of the first six categories except
Coatings ships with an "Other" row.

Only the four bifocal lens strengths ship with a coating configured (Photochromic). **The other
twelve strengths have no coatings configured and are therefore unsellable on a preset range** until
someone ticks a box on the Preset Catalogues grid. This is visible rather than silent — the Field
App tells the technician the lens has no coatings configured.

**Not built**
- No edit — a label, image or Other flag cannot be changed after creation; the only route is
  retire-and-re-add, which loses the historical association.
- No reordering; sort order is creation order and is not exposed.
- No hard delete for a mistyped entry.
- Image is a URL only. No upload, no validation that the URL resolves, no image for any category
  other than Frame colors.
- Seven identical forms share a single page-level error banner, so a validation failure does not
  indicate which card produced it.
- Gender, frame coverage, lens range type and fulfilment status are hard-coded enumerations and
  are not editable here or anywhere else.
- The Frame colors swatch shown in the *Field App* is not driven by the image URL — it uses a
  hard-coded hex table matched against six known colour names, falling back to grey.

---

## 5. Field App — screen by screen

A Blazor WebAssembly PWA with no persistent navigation chrome — each screen is full-bleed with its
own back arrow. Authenticated with a JWT (default lifetime 60 minutes).

### 5.1 Login — `/login`

Two fields: **Email** and **PIN** (a password input). Posts to the API's login endpoint; on success
the token is stored **in memory only**.

Errors: "Invalid username or password." for a rejected credential, "Could not reach the server —
check your connection." for a network failure. If already signed in, a green banner shows the token
expiry time. Footer text reads: *"Log in once online — you can keep working fully offline after
that."*

**Not built / caveats**
- **The token is not persisted.** Any page refresh, browser restart or tab close signs the user
  out, and re-login requires connectivity. The footer promise holds only within a single
  uninterrupted session.
- **There is no offline login at all** — first use, and every use after a refresh, needs a
  connection.
- No sign-out. The token store has a clear method that nothing calls.
- The field is labelled "PIN" but validates as an Identity password (≥ 8 characters, at least one
  digit).
- No route guard: only the Home screen checks for a token. Navigating directly to
  `/consultation/sale` renders the form, which then fails to load its dropdown options.

### 5.2 Home — `/`

The launcher. Redirects to login if no valid token.

- **Connectivity banner** — "Online · Synced", "Online · N record(s) unsynced", "Offline", or
  "Offline · N record(s) unsynced", driven by the browser's online flag, with a warning marker when
  anything is queued.
- **Failed-sync banner** — a red banner reading "N record(s) couldn't sync — needs review, not
  retrying automatically", followed by a list of each failed item's entity type, creation time and
  error. The error text is the raw HTTP status, e.g. "HTTP 400".
- **Three action tiles** — Record Test (blue, "Vision test outcome"), Record Lead (yellow,
  "Customer needs glasses, not ready to buy"), Record Sale (green, "Standard or custom order").
- **Links** to Messages and Settings.
- **Bottom button**, one of: "Queued (N) — waiting for signal" (disabled, when offline), "Sync now
  (N)" (active, when online with a backlog), or "Synced" (disabled).

The counts refresh only on page load and after a manual sync, so they go stale while the background
timer syncs underneath.

### 5.3 Record Test — `/consultation/test`

Fields, in order:

| Field | Control | Rules |
|---|---|---|
| Age | Number input, min 0, no upper bound in the UI | Optional; server accepts 0–120 |
| Gender | Select: Female / Male, defaulting to Female | — |
| Outcome | Select: No glasses needed / Needs glasses / Referred, defaulting to *No glasses needed* | — |

The form then branches on outcome:

**Referred** →
- *Reason for referral* — reference dropdown; choosing the "Other" row reveals a "please specify"
  text field. Server: required, must be an active Referral reason, and the free text is required
  when Other is chosen (≤ 200 chars).
- *Referral location (hospital, clinic, etc.)* — free text. **Server-required** (≤ 500 chars) but
  not marked or enforced as required in the UI.

**Needs glasses** →
- *Occupation (optional)* — reference dropdown with Other free-text.
- *"Did the customer share contact details?"* — a No / Yes pair of buttons.
  - **Yes** reveals **"Continue as Lead →"**, which saves the Test and navigates to the Lead form
    carrying `sourceTestId` plus the age and gender as pre-filled values. This is the only path
    that links a Test to a Lead, and therefore the only path that produces a conversion figure on
    the Dashboard.
  - **No** shows "Recorded as a test only — not entered into the leads pipeline."

**No glasses needed** → *Occupation (optional)* only.

Saving a Test is immediate — no price-confirmation step. A Test carries no customer name or phone
at all.

### 5.4 Record Lead — `/consultation/lead`

Fields: Age, Gender, **Full name**, **Phone number**, Occupation (optional), a consent checkbox
("Customer consents to be contacted by DOT Glasses for follow-ups/marketing"), **Reason not
purchased** (reference dropdown + Other free-text), the shared **lens range selector** with "No
preference yet" permitted, and — only when the chosen range is *not* a preset — **Coating
preference (optional)**.

Server rules: full name required (≤ 200), phone required (≤ 32), reason not purchased must be an
active option with its free text present if Other, age 0–120, and if a `sourceTestId` is carried
it must reference an existing Test that has **not already been converted** (a second attempt is
rejected).

The customer is matched or created server-side by exact **name + phone within the same outlet** —
a repeat visitor with identical details reuses their existing customer record rather than creating
a duplicate.

Submitting shows the **price-awareness confirmation**: *"Has the customer been told the price for
this order?"* with "Not yet" (returns to the form) and "Yes, save".

### 5.5 Record Sale — `/consultation/sale`

Fields: Age, Gender, **Full name**, Phone number *(optional here)*, Occupation, consent checkbox,
then the shared **lens range selector** with no "no preference" option — a Sale must always have a
range.

**When the range is Custom**, two extra controls appear:
- *"Order this lens from DOT Glasses (outlet doesn't have stock)"* — a checkbox. Ticking it is what
  creates a Custom Order: the sale is stamped *Submitted* and appears in the Admin Portal queue.
  Server-rejected if the range is not Custom.
- *Coating* — a dropdown of **all** active coatings (a preset range instead gets a restricted list
  from the lens range selector; see 5.6).

Then, for every sale:
- **Frame colour** — a row of circular colour swatches, one per active Frame colour reference item.
  The colour shown comes from a hard-coded six-entry hex table matched by name substring, falling
  back to grey; the admin-entered image URL is not used here. Selecting the "Other" swatch reveals
  a "please specify" text field. Required server-side.
- **Frame coverage** — select: Full frame / Eye-frame rims only.
- **Hard case sold** — a checkbox; ticking it reveals a **Hard case colour** reference dropdown
  with Other free-text. Server-enforced both ways: colour required when sold, and both colour
  fields must be empty when not.

A coating is **always required** on a Sale. For a preset range it must be one the admin has ticked
as available for the chosen *left eye* lens's strength; for Custom, any active coating is
accepted. Note the documented simplification: one coating column exists for both eyes, resolved
against the left eye's configuration.

Submitting shows the same price-awareness confirmation as a Lead.

**A Sale recorded here can never be linked to an existing Lead** — no Leads list or "convert to
sale" entry point exists in either application, so `SourceLeadId` is never populated by any user
action, and a Lead's converted flag is only ever set through the same-session Test → Lead → Sale
path.

### 5.6 The lens range selector (shared by Lead and Sale)

A single dropdown chooses the range: *No preference yet* (Leads only), *6-Lens Set*, *9-Lens Set*,
*Custom prescription*. Switching range clears every field belonging to the previous one.

The two preset options are matched to catalogues **by name substring**. If the matching catalogue
isn't assigned to the technician's retail point, the option is suffixed "(not available)" and
choosing it shows "This preset isn't assigned to your retail point."

**Preset range** →
- *Lens power — left eye* and *— right eye*: selects listing the catalogue's lens options in sort
  order.
- *Pupil distance (0–4)*: a coarse frame-fit bucket, **not** millimetres. Drops to 0–2 when the
  children's-frame box is ticked, and a previously chosen out-of-range value is cleared.
- *Coating*: appears once a left lens is chosen, listing **only** the coatings configured for that
  lens strength. If none are configured it is replaced by: "No coatings are configured for this
  lens yet — it can't be sold on a preset range until DGI configures one in Reference Data."
  Changing the left lens clears any coating already picked.

**Custom range** → per eye (left and right):
- *Sphere*: a select from −10.00 to +10.00 in 0.25 steps.
- *Cylinder*: a select from −6.00 to +0.25 in 0.25 steps.
- *Axis*: a number input, whole degrees 0–180.
- *Add power*: a select from +0.00 to +3.00 in 0.25 steps.

Plus *Pupil distance (mm, 54–74)* — a select of whole millimetres. Both sphere values are required.

All of these constraints are enforced twice: as generated dropdown ranges client-side, and
independently server-side (values outside range, or off the 0.25 increment, are rejected with a
400 even if the client is bypassed).

Finally, a **Children's frame** checkbox applies to both ranges.

The preset bucket and the millimetre value are mutually exclusive and enforced as such: a preset
range must not carry millimetres and a Custom range must not carry a bucket. The bucket is
**required** on a preset-range Sale but **optional** on a preset-range Lead.

### 5.7 Placeholder screens

- **Messages** (`/messages`) — two hard-coded announcements ("Reference data updated", "Reminder").
  Nothing is fetched; the "Refreshes on sync" note is aspirational.
- **Settings** (`/settings`) — an "Active location" list of two hard-coded outlet names with a tick
  on one, and three "· Coming soon" toolkit items (Talking points & FAQs, Near-vision chart,
  Distance-vision chart). Nothing is clickable or wired to real data.
- **Outlet select** (`/outlet-select`) — two hard-coded outlet names; selecting one navigates home
  and does nothing. Nothing in the app links to this route.
- **Not found** — the router's fallback.

One routing note: the consultation route accepts any type segment, and anything that isn't "test"
or "sale" is treated as a Lead. `/consultation/anything` renders and saves a Lead.

---

## 6. Offline and sync behaviour

This is a genuine functional capability, not just plumbing, so it is described from the
technician's point of view.

**What is queued.** Every Test, Lead and Sale is written to a browser IndexedDB outbox *before*
any network call is attempted, with a client-generated GUID. Batched client-side log entries ride
the same queue. Nothing is written directly to the API from a form.

**Idempotency.** The GUID is the idempotency key and every create endpoint treats a create as an
upsert on it, so a record replayed after an interrupted sync is never duplicated.

**When sync runs.** On save (best effort, immediately), on the browser's `online` event, on a
30-second background timer, and on the Home screen's "Sync now" button. Concurrent runs are
suppressed so the queue is never drained twice at once.

**Outcomes per item.**
- **Success** → marked Synced and never sent again.
- **Network error or 5xx** → left queued, logged as a warning, retried on the next cycle
  indefinitely. This is the offline case and it works as intended.
- **400, 401 or 403** → marked **Failed**, which is *terminal*. It is excluded from the retry queue
  permanently and surfaced on the Home screen's red banner instead.

**What the technician sees when something fails permanently.** The entity type, the creation
timestamp, and the literal string "HTTP 400". Nothing about which field was wrong.

**What they can do about it.** Nothing, from within the app.

**Not built**
- **No client-side form validation anywhere in the consultation forms.** There is no validator
  component, no required markers, and no field-level messages. A technician can leave the required
  "Reason not purchased" or "Frame colour" unselected, submit through the price confirmation, and
  see the record accepted — the empty value goes out as an empty GUID, the API rejects it, and the
  record becomes permanently Failed with no indication of the cause. This is the single largest
  functional gap in the Field App.
- No retry-after-edit and no discard for a failed item — a broken record sits on the home screen
  indefinitely.
- No offline caching of reference data or catalogues. They are fetched once per session and held
  in memory only; a technician who has never been online in this session cannot open a form at all,
  and sees "Couldn't reach the server to load lens/coating/frame options" with a Retry button.
- No offline login and no token persistence (see 5.1).
- No conflict resolution — the create-as-upsert is last-write-wins, with no version or ETag column.
- The Failed banner has no dismiss.
- The published build's service worker caches the app shell for offline start-up; the development
  build deliberately does not.

---

## 7. REST API surface

Versioned at `v1`, with Swagger exposed in development only.

| Endpoint | Auth | Who | Behaviour |
|---|---|---|---|
| `POST /api/v1/auth/login` | Anonymous | Anyone | Username + password → JWT (60 min default). Failures count toward lockout; suspended accounts are refused as invalid credentials. |
| `GET /api/v1/tests` · `GET /api/v1/tests/{id}` | JWT | Any authenticated user | Hierarchy-scoped list / fetch. |
| `POST /api/v1/tests` | JWT | Any authenticated user | Idempotent create. Rejected with 400 if the caller has no org assignment. |
| `GET/POST /api/v1/leads`, `/api/v1/leads/{id}` | JWT | Any authenticated user | As above. |
| `GET/POST /api/v1/sales`, `/api/v1/sales/{id}` | JWT | Any authenticated user | As above. |
| `GET /api/v1/reference-data` | JWT | Any authenticated user | All **active** reference items across all categories. Not hierarchy-scoped. |
| `GET /api/v1/preset-catalogues` | JWT | Any authenticated user | Catalogues assigned at or above the caller's org, with each lens's available coatings. 400 if the caller has no org. |
| `POST /api/v1/client-logs` | JWT | Any authenticated user | Accepts a batch of client log entries with a correlation ID; writes them to the server log. |
| `GET/POST/PUT/DELETE /api/v1/widget-examples` | JWT | Read/update/delete: any user. Create: Admin or Manager. | Developer sandbox (see §8). |

**Capabilities reachable through the API that no UI exposes:**
- `GET` list and by-ID for Tests, Leads and Sales — nothing in either application reads these
  endpoints; the Field App only writes, and the Admin Portal queries the database directly.
- Setting `SourceLeadId` on a Sale (converting an existing Lead) is fully implemented and
  validated server-side, including the atomic flag update and the "already converted" rejection,
  but no screen can produce it.
- Full update and hard delete of a widget example.

**Restrictions that exist only in the UI, not the API:**
- Any authenticated user of any role or level can create a Test, Lead or Sale. The Admin Portal
  simply has no form for it.
- The API applies no level restriction on custom orders — a Sale posted with
  `OrderFromDotGlasses` from any Retail Point enters the fulfilment queue regardless of whether
  its country is flagged as able to handle custom orders.

Cross-origin access is restricted to two hard-coded localhost development origins.

---

## 8. Placeholder and developer-only surfaces

These exist in the running product but are not real product capability:

| Surface | Status |
|---|---|
| Field App **Messages** | Two hard-coded announcements. No backing data or API. |
| Field App **Settings** | Two hard-coded outlet names; three "Coming soon" items. No real data. |
| Field App **Outlet select** | Hard-coded, and unreachable — nothing links to the route. |
| Field App **Widget Examples** (`/widget-examples`) | A developer walkthrough for the offline outbox. Lets the user type an **arbitrary hierarchy path**, which the API accepts as-is (unlike Test/Lead/Sale, where the server stamps it). A `User`-role account is refused by the create policy and produces a permanently Failed outbox item. |
| **Widget Examples API** | The full CRUD sandbox behind the above. |
| **Developer user seeder** | Creates three accounts at DGI / Country / Retail Point on start-up so RBAC is exercisable locally. Gated behind configuration that is never set in production, but the Manager and User passwords are hard-coded constants in source. These accounts have a primary org but **no** org-assignment rows, so their Scope column in the User Directory is blank. |
| **Seeded org tree** | Four nodes: DOT Glasses International → Kenya (custom orders enabled) → Kangemi Vision Centre → Kangemi Vision Centre — Outreach Post. |
| **Automatic database migration on start-up** | Development only. |

---

## 9. Cross-cutting gaps

Capabilities that cut across screens and are absent or inert today.

**Configured but inert.** Three pieces of data can be set by an administrator and have no
functional effect anywhere:

1. **`CanHandleCustomOrders`** on a Country node. The Organisations screen offers the toggle, and
   the domain describes it as determining "whether the Custom Order flow appears on the Field App
   for retail points under this country". **Nothing reads it.** The Field App shows the "Order from
   DOT Glasses" checkbox to every technician on every Custom sale, and neither the API nor the
   Custom Orders queue consults it.
2. **`UserOrgAssignment`** — multi-location assignment. Rows are written by the invite form and by
   the Organisations "Assign users" action, and read back only for display (the Scope badges and
   the assigned-users list). They do not affect what a user can see, what they can do, or which org
   their records are stamped with — all of that comes from the *primary* org alone, which is fixed
   at invite time as the first checkbox ticked and cannot subsequently be changed. There is no
   "switch active location" anywhere; the Field App's Settings screen that would host it is
   hard-coded.
3. **`ConsentGiven`** on Leads and Sales. Captured on every consultation form, stored, and never
   read, reported, filtered on, or exported.

**Absent across the board.**
- **No editing or deletion of transactional data.** Tests, Leads and Sales are create-once atomic
  events by design — but that means a technician who mistypes a phone number or picks the wrong
  frame colour has no correction path in either application, and no admin can fix it either.
- **No customer-facing surface at all.** `Customer` is internal-only: matched by exact name +
  phone within an outlet, never listed, searched, edited or merged. Near-duplicates (a phone typed
  with and without a country code) silently become two customers.
- **No email delivery.** The sender is a logging stub. Every invite and password reset requires an
  administrator to manually copy a link out of the browser and relay it.
- **No Leads worklist.** Leads are recorded and reported on but never worked: no follow-up, no
  assignment, no status, and no conversion to a Sale from any screen.
- **No export or reporting output** of any kind, from any screen.
- **No audit trail is surfaced.** Created/modified user and timestamp are captured on every entity
  but no screen displays them.
- **No notifications** in either direction — the Field App's Messages screen is static and nothing
  server-side can push to it.
- **No search** except the single Leads-name box in Event History.
- **No paging** except Event History; every other list renders in full.

**Behavioural fragilities worth knowing about.**
- The Admin Portal's access-denied path resolves to a non-existent action, so every permission
  refusal renders as a 404 rather than an explanation.
- The sidebar advertises all seven screens to every user, including the four they may be refused
  from.
- A user whose `HierarchyPath` is blank would match the visibility filter's prefix test against
  every row and see the entire database. All current creation paths set it, but nothing enforces
  that it is non-empty at the filter itself — unlike the resource-based RBAC check, which
  explicitly rejects an empty prefix.
- Records created by a DGI- or Country-level user through the API are stamped at that level and
  render as "Unknown outlet" / "Unknown country" throughout reporting, and are counted in the
  Dashboard's totals.
