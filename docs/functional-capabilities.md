# DOT Glasses — Functional Capability Review

**What this is:** a functional specification of what the DOT Glasses platform actually does
today, derived from application source (controllers, views, Razor components, services,
validators, authorization handlers, domain entities and seed data).

**Reviewed at:** commit `b745220` (`main`), 2026-08-12 — updated from the original 2026-08-07
review to reflect the full 2026-08-09 roadmap (Phases 1–8, all shipped). Sections untouched by
that roadmap are carried over from the original review; everything else was re-checked against
current source.

**Scope:** the Admin Portal (`DotGlasses.Web`, server-rendered MVC), the Field App
(`DotGlasses.App`, Blazor WebAssembly PWA), and the v1 REST API that joins them.

---

## 1. How to read this

Capability in this system is a function of **two independent things**: a user's *role*
(Admin / User) and the *organisation level* they are attached to (DGI / Country / Intermediate /
Retail Point). An Admin at Country level and an Admin at a Retail Point see materially different
products, so this document works in role × level personas rather than in role sections.

Every section is written from the end user's point of view: what appears on the screen, what
they can do with it, exactly which fields exist and what the system will and won't accept.
Each screen ends with a **Not built** subsection — capabilities a user demonstrably cannot
perform today. Those are statements of current fact, not criticism; several are deliberate (see
[`open-issues.md`](open-issues.md) for which ones and why).

---

## 2. The access model

### 2.1 Two independent mechanisms

The system separates **what rows you can see** from **what you can do with them**, and the two
never touch.

**Data scoping (visibility)** is a global EF Core query filter applied automatically to every
entity carrying a hierarchy path — `OrganisationNode`, `Customer`, `Test`, `Lead`, `Sale` and
`WidgetExample`. The rule is: *a row is visible if its `HierarchyPath` starts with the signed-in
user's own `HierarchyPath`*. This is completely role-independent. Scoping is downward only — you
see your own node and everything beneath it, never anything above or beside you.

Four entities are **not** hierarchy-scoped and are therefore globally visible to every
authenticated user: `ReferenceDataItem`, `PresetCatalogue`, `LensOption` and
`LensStrengthCoatingOption`. Reference data and catalogues are a single global library, not a
per-country one. Note also that `ApplicationUser` is an Identity type, outside the automatic
filter entirely — the User Directory applies the same prefix rule manually in code.

Because reporting screens need to resolve a row's *ancestor* names (which country is this outlet
in?), and ancestors are by definition invisible under the downward-only filter, Dashboard, Event
History and the Field App's catalogue lookup deliberately read the org tree through an explicit
unscoped query instead.

**RBAC (permissions)** is separate, policy-based, and evaluated per action.

### 2.2 Roles and org levels

**Two roles exist and are the only ones assignable: `Admin`, `User`.** (A third role, `Manager`,
existed until 2026-08-10 and was removed — every policy that admitted a Manager admitted an Admin
identically, so it had never carved out a real distinction. Existing Manager accounts were
migrated to Admin on removal, preserving their access.) A user holds one role, at one primary
organisation node.

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
| `PresetCatalogue.Manage` | Role = **Admin** AND level ≤ **Country** | The whole Preset Catalogues screen |
| `CustomOrders.View` | **Any role** AND level ≤ **Country** | The whole Custom Orders screen, *and* its Advance-status action |
| `Organisations.ManageInScope` | Role = **Admin** AND target org's path is at/below the caller's | Every Organisations write action, per node |
| `Users.ManageInScope` | Role = **Admin** AND target user's path is at/below the caller's | Every User Directory write action, per user |
| `WidgetExample.Create` | Role = **Admin** (no level or scope check) | The developer sandbox API only |

One design point worth stating explicitly, because it's deliberate: **`CustomOrders.View` is the
only policy where a plain `User` gets access to something an Admin below Country level does
not** — a `User` attached at Country level can advance fulfilment status; an Admin attached to a
Retail Point cannot even see the queue. It's the only policy in the table that's level-gated but
not role-gated.

Dashboard, Organisations, Event History and User Directory carry only a bare `[Authorize]` —
every authenticated user reaches them, and what they see is narrowed by data scoping rather than
by policy. The sidebar hides Preset Catalogues/Custom Orders/Reference Data per-request against
these same three policies, and a failed policy check redirects to a real `/Account/AccessDenied`
page (not a 404).

### 2.4 The five personas

| # | Persona | Realistic description |
|---|---|---|
| **P1** | Admin @ DGI | Super admin. The only persona that can edit reference data. |
| **P2** | Admin @ Country | Country office lead. Everything except reference data. |
| **P3** | Admin @ Intermediate / Retail Point | Retailer/distributor/outlet admin. Loses catalogues and custom orders. |
| **P4** | User @ Retail Point | The field technician. Read-only MI; the Field App is theirs. |
| **P5** | User @ Country or DGI | Unusual but creatable via the invite form. Read-only MI **plus** the full Custom Orders queue. |

---

## 3. Persona × surface capability matrix

`●` = full access · `◐` = visible but scoped/partial · `○` = reachable but no write actions ·
`✕` = blocked

| Surface | P1 Admin@DGI | P2 Admin@Country | P3 Admin@Interm./RP | P4 User@RP | P5 User@Country+ |
|---|---|---|---|---|---|
| **Dashboard** | ● all data | ◐ own subtree | ◐ own subtree | ◐ own outlet | ◐ own subtree |
| **Organisations** — view tree | ● whole tree | ◐ own subtree | ◐ own subtree | ◐ own node only | ◐ own subtree |
| **Organisations** — add child / rename / deactivate / assign / un-assign | ● | ● in scope | ● in scope | ✕ (redirected) | ✕ (redirected) |
| **Event History** (4 tabs) | ● all | ◐ own subtree | ◐ own subtree | ◐ own outlet | ◐ own subtree |
| **User Directory** — view | ● all users | ◐ subtree users | ◐ subtree users | ○ subtree users | ○ subtree users |
| **User Directory** — invite / reset / suspend | ● | ● in scope | ● in scope | ✕ (redirected) | ✕ (redirected) |
| **Preset Catalogues** | ● | ● | ✕ (redirected) | ✕ (redirected) | ✕ (redirected) |
| **Custom Orders** — view + advance | ● | ● | ✕ (redirected) | ✕ (redirected) | ● |
| **Reference Data** | ● | ✕ (redirected) | ✕ (redirected) | ✕ (redirected) | ✕ (redirected) |
| **Field App** — record Test/Lead/Sale, convert Lead | ● (stamped to DGI root) | ● (stamped to their node) | ● | ● | ● |

Two consequences of that last row are worth flagging: **any** authenticated user can record a
Test, Lead or Sale through the API — there is no role or level restriction on the write
endpoints — and the record is stamped with whatever org the caller sits at. A DGI Admin recording
a Sale produces a Sale attached to the DGI root node, which then resolves as "Unknown outlet" and
"Unknown country" on every reporting screen.

**Navigation now reflects permissions.** The sidebar filters Preset Catalogues/Custom Orders/
Reference Data per the policies above; a direct hit on a blocked route (bookmarked, typed) renders
a real Access Denied page.

---

## 4. Admin Portal — screen by screen

### 4.1 Sign-in and account pages

**Login** — `/Account/Login`, anonymous.

Two fields, both required client-side: `UserName`, `Password`. Optional `returnUrl`, honoured only
if local. Sign-in is persistent (a lasting cookie) and counts failures toward lockout. On success
`LastLoginUtc` is stamped and the user lands on the Dashboard or their return URL. On any failure
— wrong password, unknown user, or a suspended account — the same message appears: *"Invalid
username or password."* Suspension is deliberately not distinguishable from a bad password.

**Sign out** — a real POST action, reachable from a button in the sidebar on every authenticated
page. Ends the cookie session and returns to Login.

**Set password** — `/Account/SetPassword?userId=…&token=…`, anonymous.

The target of an invite or reset link. Fields: hidden `UserId`, hidden `Token`, `Password`. The
token is a genuine ASP.NET Identity password-reset token. Password rules as configured (tightened
2026-08-12): minimum 8 characters, **at least one digit, one uppercase letter and one
non-alphanumeric character all required.** On success the account's `EmailConfirmed` is set to
true and the user is redirected to Login with "Password set — you can now log in." An invalid or
expired token surfaces Identity's own error text.

**Not built**
- No self-service "forgot password" — a reset can only be initiated by an admin from the User
  Directory.
- No password change for a signed-in user, no MFA, no account lockout feedback.

---

### 4.2 Dashboard (MI Reporting)

**Route** `/` · **Access** any authenticated user · **Data** automatically scoped to the viewer's
subtree.

A **date range filter** (From/To, Apply button) sits above the tiles — leave both blank for
all-time. Six stat tiles across the top:

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
a Sale separately produces no conversion, by design of the data model as built. The
conversion-*measurement* universe narrows with the date filter, but whether a given Test counts as
converted at all is evaluated against the **full, unfiltered** Lead/Sale history — narrowing the
date range narrows which Tests are being measured, not the facts used to decide if each one
converted.

**Four of the six tiles, plus Referrals logged, are clickable** — each links through to the
matching Event History tab (or the Custom Orders screen, for the Custom orders tile), carrying
the current date filter along so the drill-down shows exactly the rows behind the number.

Right-hand column, three cards:
- **Referrals logged** — count of Tests with outcome *Referred*, links to Event History's
  Referrals tab.
- **Conversion trend (last 6 weeks)** — six bars, each a rolling 7-day window ending at "now",
  showing that window's test-to-sale conversion %. **Always the real last 6 weeks, unaffected by
  the date filter above** — a "trend over time" widget re-scoped to an arbitrary custom window
  would defeat its own purpose. Bar height is the percentage; the tooltip is the raw number. No
  axis, no dates, no labels.
- **Gender split** — a two-segment bar computed from `Test.Gender` only. Only Female and Male exist
  in the domain; there is no third value or "unspecified".

Main card, **Top performing** — four fixed top-5-by-sales-volume lists (not filterable, not
clickable), each row showing a name, its sale count and its own conversion % (that key's Sales ÷
that key's Tests):
- **Top outlets** — exact hierarchy-path match on the org node.
- **Top retailers** — the **Retailer**: the nearest `Intermediate`-level ancestor.
- **Top countries** — the `Country`-level ancestor.
- **Top technicians** — the recording user's full name, falling back to their username.

Unresolvable names appear as "Unknown outlet" / "Unknown retailer" / "Unknown country" rather than
failing. Sales at a retail point that sits directly under a Country are ranked under a separate
**"No retailer"** row — that outlet genuinely has none, and reporting says so rather than
substituting the country (2026-09-05). "No retailer" and "Unknown retailer" are different rows
carrying different facts: the first means "there is none", the second "we cannot resolve this
path". If all four lists are empty the whole card collapses to "No sales recorded yet."

**Organisations flagged `IsTrainingOrg` are excluded from every figure on this page** — the tile
counts, the conversions, the trend, the gender split and all four rankings. Training exclusion is
applied on this screen only.

**Not built**
- No country, outlet or role filters — only the date range.
- Top-performing lists stay non-interactive — no drill-down or per-key filter.
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

Because reads are scoped downward only, **your own node becomes the displayed root** — an Admin
at Country level sees their own country as the top of the tree with DGI absent entirely, not
greyed out. A Retail Point user sees a single-node "tree" consisting of themselves.

A separate collapsed **"Deactivated orgs"** list, below the tree, shows the caller's own
deactivated nodes with a Reactivate link per row.

**Right panel — the selected node.** Shows the level badge, a training badge if flagged, the name,
the free-text `Kind` label, a Retail-Point-only note that stock is tracked externally in Zoho, and
a list of users assigned to this node (each with an × to un-assign). If the viewer fails the scope
check the panel says "You don't have permission to manage this node." and no actions render.

**Actions** (all re-checked server-side; hidden buttons are never trusted alone):

1. **Flag / unflag as training organisation** — one-click toggle, available at any level. Excludes
   the node and everything beneath it from Dashboard aggregates.
2. **Rename** — a modal, `Name` only (≤ 200 chars). `Kind` and level are still fixed after
   creation.
3. **Deactivate / Reactivate** — soft-delete via the node's existing `IsDeleted` flag. Deactivating
   a node with active (non-deactivated) children is refused — deactivate the children first, so
   nothing gets silently orphaned under a node that's disappeared from every admin's tree. A
   deactivated node moves to the "Deactivated orgs" list; Reactivate moves it back into the tree.
4. **Add child node** — a modal. Fields: `Name` (required, ≤ 200 chars), `Kind` (optional free-text
   display label, ≤ 100 chars), `Level` (a select only when more than one level is legal; a hidden
   field with an explanatory line when exactly one is; the button is hidden entirely for Retail
   Points). Level legality is enforced three times over — in the UI, in the validator, and in the
   service. The new node's hierarchy path is minted as *parent path + (global maximum path segment
   across the whole tree + 1)*, so segments are globally unique and ever-increasing rather than
   per-parent.
5. **Assign users** — a modal with a single-select dropdown of every user in the caller's own
   scope. Creates a `UserOrgAssignment` row; re-submitting the same pair is a silent no-op. This
   **does not** change the user's primary organisation on its own — see §4.5 and §5.7 for how a
   technician actually switches their active/primary location.
6. **Un-assign a user** — the × next to a name in the "Assigned users" list. Refused (with an
   inline error, not a crash) if the target org is that user's **primary** org — there's still no
   "change primary org" UI to move it first, so un-assigning it would leave the user with nothing
   driving their JWT/hierarchy scope.

**Not built**
- No edit of `Kind` or level after creation (only the name).
- No move/re-parent.
- Assigning a user has no effect on their primary org — see the location-switching capability in
  §4.5/§5.7 for how that's actually controlled.
- Path-segment minting is read-max-then-increment with no locking; concurrent creates could
  collide (see `open-issues.md`).
- Users created by the developer seeder have no `UserOrgAssignment` rows unless separately
  assigned, so they won't appear in any node's "Assigned users" list despite having a primary org.

---

### 4.4 Event History

**Route** `/EventHistory?tab=…&search=…&fromDate=…&toDate=…&page=…` · **Access** any authenticated
user · **Data** automatically scoped to the viewer's subtree. Four tabs, 25 rows per page, plus a
date range filter shared with the Dashboard's drill-down links.

**Sales tab** — columns: Type badge (green "Sale", suffixed "· Custom" when the lens range was
Custom), customer name, outlet, country, **Consent** (Yes/No, from `ConsentGiven`), absolute local
timestamp (`yyyy-MM-dd HH:mm`). Newest first.

**Tests tab** — Type badge, outlet, country, timestamp. **There is no Name column at all** — `Test`
carries no customer reference of any kind (the unused `CustomerId` field was removed); Tests are
genuinely anonymous records, not just displayed without a name.

**Leads tab** — columns: name, masked phone, outlet, reason not purchased, **Consent**, a
**convert-to-sale action** (shows "Converted" once done, otherwise a "Convert to sale" link
opening the admin conversion form described below), relative "Logged" time ("just now", "N
minutes/hours/days ago", falling back to an absolute date beyond a week). Phone masking keeps the
first 4 and last 3 characters and replaces the middle with a fixed 4-character redaction; numbers
of 7 characters or fewer are shown unmasked, and a missing number shows "—". Includes a search box filtering on customer full name, applied at the database level
*before* paging so page numbers stay meaningful. The match is **case-insensitive** (`ILIKE`).

**Referrals tab** — columns: outlet, country, reason, absolute time, preceded by a note that these
are tracked for government reporting. This is a filtered view of the same Tests data
(outcome = *Referred*), not a separate record type — a referred test correctly appears in both
tabs.

The **admin conversion form** (`/Leads/Convert/{id}`) asks for the Sale fields a Lead has no
equivalent for — coating, frame colour, hard case, "order from DOT Glasses", and the lens range
where the Lead captured no preference — plus **referred or treated**, with a referral reason, its
"Other" free text, a treated-in-facility flag and a referral location, following exactly the same
conditional rules as every other capture path (2026-09-04). Frame coverage is **not** asked here,
matching the Field App's Sale form; the sale records the Full frame default. Every field is
rendered unconditionally with its condition stated in the label — the rules are enforced
server-side and reported as a validation summary on submit, not by live show/hide.

Reference-data labels (referral reason, reason not purchased) resolve against **all** reference
items including retired ones, so a historical event referencing a since-retired option still
displays its label rather than blanking. Where the chosen option was the category's "Other" row,
the row's own free-text is shown in place of the generic "Other".

Paging shows "Page X of Y (Z total)" with Previous/Next links only, and only when there is more
than one page. Switching tabs resets to page 1; the search term and date filter are carried across
page/tab links.

**Not built**
- No outlet, country or technician filters — date range and (Leads-only) name search are the only
  filters.
- No export.
- No row detail view — you cannot open the underlying Test, Lead or Sale from here (except Leads'
  convert-to-sale action, which is a write path, not a detail view).
- Training-org data is included here (unlike the Dashboard).

---

### 4.5 User Directory

**Route** `/UserDirectory?search=…&role=…&status=…&page=…` · **Access** any authenticated user can
view the list; every write action requires `Users.ManageInScope` against that specific user.
Listing applies the hierarchy prefix filter manually, since `ApplicationUser` is outside the
automatic query filter.

**The table** — Name, Role, Scope, Last login, Sales, Status, actions. A search box (name or
email), a Role filter, a Status filter and Previous/Next paging sit above it.

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
   attempts email delivery (see below), and displays the link on screen regardless. Does *not*
   clear the existing password; the old one keeps working until the link is used.
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
| Role | Select: **Admin / User**, defaulting to User |

On submit the system creates the account **with no password at all** (which is what "Invited"
means), adds a `UserOrgAssignment` row for every selected org, stamps the *first* selection as the
primary org — writing `OrgNodeId`, `HierarchyPath` and `OrgLevel`, which become the user's sign-in
claims and therefore drive everything they can see — generates a reset token, and builds a
set-password link.

**Email delivery is real when Azure Communication Services is provisioned** (staging/production,
once the user has run the deploy that provisions `acs.bicep`); locally, and in any environment
where ACS hasn't been provisioned, the email sender is a logging stub. Either way, **the
set-password link is always also shown once in a banner on the page** — real delivery doesn't
remove that fallback, since it's the only way to recover if the email genuinely doesn't arrive.
Reloading the page loses the on-screen copy; the only recovery at that point is Reset password,
which mints a new one.

**Not built**
- No edit of a user's name, email, role or org assignments after invite (org un-assignment is done
  from the Organisations screen instead — see §4.3).
- No delete or deactivate (only suspend).
- No way to change which of a multi-org user's locations is primary from the Admin Portal (the
  Field App technician can switch their own *active* location — see §5.7 — but that's not the
  same as re-designating which org is primary here).
- No resend of an existing invite without invalidating it.
- No bulk invite or import.
- The invite button is shown to users who cannot invite.

---

### 4.6 Preset Catalogues

**Route** `/Catalogues?search=…` · **Access** `PresetCatalogue.Manage` — Admin at DGI or Country
level. Everyone else is redirected to Access Denied.

Catalogues themselves are **not** hierarchy-scoped: every user who reaches this page sees every
catalogue in the system, regardless of who owns it. A name search box narrows the list (no
paging — the table is structurally small, see below).

**Catalogue cards** — one per catalogue, showing name, description, "Diopter range: …", its
**Kind** ("Field App picker role: SixLensSet/NineLensSet", omitted for `Other`), and the list of
orgs it's assigned to (with an un-assign action per org, not just a count). Below that, the lens
roster as removable badges, and an add-lens form.

- **Create package** / **Edit** — a modal with four fields: `Name` (required, ≤ 200),
  `Description` (≤ 500), `Diopter / strength range` (a free-text label, ≤ 100), and **`Kind`**
  (`Other` / `SixLensSet` / `NineLensSet`) — at most one catalogue may hold `SixLensSet` and at
  most one `NineLensSet`; any number may be `Other`. `Kind` is what the Field App's lens range
  selector actually matches against (see §5.6) — not the catalogue's name. On create, the owning
  org is stamped from the caller's own primary org, never submitted by the client; the service
  rejects the create if that org isn't DGI or Country level.
- **Add lens** — a dropdown of active `LensStrength` reference items **not already on this
  catalogue** (both client-filtered and server-guarded — a strength can no longer be added twice).
  The chosen strength is appended at the end of the catalogue's sort order. A catalogue's lens
  roster is therefore *"which curated strength labels are included, in what order"* — the actual
  power and bifocal-ness live in the reference item's own label (e.g. `+2.50`,
  `+0.00 / +2.50 (Bifocal)`).
- **Remove lens** (the × on each badge) — a **hard delete**, not a retire. This is safe because no
  Test, Lead or Sale can reference a lens option that was never chosen on a real transaction.

**Assign packages to a retailer** — a form with a single-select org dropdown (restricted to
`Intermediate` and `RetailPoint` nodes only), a multi-select catalogue list, and an Assign button.
Each selected catalogue produces one assignment; re-assigning an existing pair is a silent no-op.
**Assignment cascades downward** — assigning to an Intermediate makes the catalogue available to
every Retail Point beneath it. Each assignment can be individually removed from the catalogue
card's assigned-orgs list.

**Lens strength coating availability** — a grid with active lens strengths as rows and active
coatings as columns. Each cell is a one-click ✅/⬜ toggle recording "this strength can be sold in
this coating". This grid directly drives the Field App: a technician choosing a preset lens is
offered exactly the coatings ticked here, and a strength with **no** coatings ticked cannot be
sold on a preset range at all — the Field App shows an explanatory message instead of an empty
dropdown, and the API rejects the sale. If no active Coating reference items exist, the whole grid
is replaced with a pointer to the Reference Data screen.

**Not built**
- No delete or archive of a catalogue.
- No reordering of lenses within a catalogue.
- Catalogues cannot be assigned to Country or DGI nodes from this screen (only Intermediate and
  Retail Point), even though the seeded assignments are at Country level and the cascade logic
  supports it.

---

### 4.7 Custom Orders

**Route** `/CustomOrders?status=…` · **Access** `CustomOrders.View` — **any role**, at DGI
or Country level only. Hidden entirely below that. The same policy gates both viewing and
advancing status.

The queue lists every Sale with a fulfilment status set — that is, every Sale recorded as a Custom
prescription with "Order this lens from DOT Glasses" ticked. Unpaged (custom-order volume is
naturally small), with a status-filter pill row (`Submitted` / `In Lab` / `Ready for Pickup` /
`Fulfilled`) above the list.

Orders are grouped **Retailer → retail point → customer**, each order showing its **Prescription**
(a formatted string, `OD <right> / OS <left>`, each eye showing sphere and, where non-zero, `cyl`
and `add`), a **Status** badge, and the advance action. The Retailer and retail-point headings each
carry an "N active" badge counting *unfulfilled* orders across the caller's whole scoped set,
regardless of which status pill is selected, so the badge reads as a stable "how much sits here"
signal rather than shifting with the filter.

**Retailer** here means what it means everywhere else in the product: the nearest
`Intermediate`-level ancestor of the order's retail point, not that retail point's immediate parent
(2026-09-05 — the two disagree whenever a retail point hangs directly off a Country, and the old
resolution headed such a group with the country's name). Three headings are possible where no
Retailer node resolves, and they are deliberately distinct groups rather than one bucket:

| Heading | Means |
|---|---|
| *the retailer's name* | A reseller tier was found at or above the retail point. |
| **No retailer** | The retail point is known and hangs directly off a Country — it genuinely has none. |
| **Unknown retailer** | The order's hierarchy path names no org node at all — a data problem. |

A retail point that cannot be resolved likewise appears as **"Unknown outlet"**. Ancestor names are
resolved against the whole org tree rather than the caller's own subtree, so a caller scoped at a
retail point still sees their own Retailer named rather than "Unknown".

**Advance status** is a single button labelled with the next state. The flow is linear and
forward-only: **Submitted → In Lab → Ready for Pickup → Fulfilled**. Status is set to *Submitted*
automatically at the moment the sale is created. Once Fulfilled the button disappears, and the
service refuses any further advance. There is no way to set an arbitrary status.

A refused advance — the order was already Fulfilled (a colleague got there first, a double click,
a browser resubmit), it isn't a custom order, or it isn't visible to the caller — comes back as a
sentence in a red banner above the queue, not an error page (2026-09-04).

Empty state: "No custom orders yet" (or a filtered variant when a status pill with no matches is
selected).

**Not built**
- No revert, cancel, or reject.
- No notes, expected dates, lab assignment, or tracking references.
- No notification back to the originating outlet or technician; the Field App never learns that an
  order progressed.
- The prescription string omits axis and pupil distance, both of which are captured on the sale.
- No link from a queue row back to the underlying Sale record.

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
is set, on the Frame colors card only — each carrying a **pencil icon to edit** (label and image
URL only — category, code and the Other flag stay fixed after creation), **↑/↓ buttons to
reorder**, and a × to retire it, plus a collapsed "Retired (N)" section with a Restore link per
option.

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
someone ticks a box on the Preset Catalogues grid (tracked in `open-issues.md`). This is visible
rather than silent — the Field App tells the technician the lens has no coatings configured.

**Not built**
- No hard delete for a mistyped entry (edit covers a mislabel; retire covers removal).
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
the token is **persisted to IndexedDB**, not just held in memory. Password rules (tightened
2026-08-12, same as the Admin Portal): minimum 8 characters, at least one digit, one uppercase
letter and one non-alphanumeric character.

Errors: "Invalid username or password." for a rejected credential, "Could not reach the server —
check your connection." for a network failure. If already signed in, a green banner shows the token
expiry time. Footer text: *"Log in once online — you can keep working fully offline after that.
Sign out from Settings when you hand the device to someone else."*

**Not built / caveats**
- No offline login for the *very first* use on a device — that first session still needs
  connectivity. After that, the persisted token and cached reference data (see 5.7, 6) keep the
  app usable offline across restarts.
- The field is labelled "PIN" but validates as a full Identity password.
- No route guard: only the Home screen checks for a token. Navigating directly to
  `/consultation/sale` renders the form, which then fails to load its dropdown options if there's
  no cached copy and no connection.

### 5.2 Home — `/`

The launcher. Redirects to login if no valid token.

- **Connectivity banner** — "Online · Synced", "Online · N record(s) unsynced", "Offline", or
  "Offline · N record(s) unsynced", driven by the browser's online flag, with a warning marker when
  anything is queued.
- **Failed-sync banner** — a red banner reading "N record(s) need review — tap to fix or discard",
  linking to `/failed-records` (see 5.7a).
- **Four action tiles** — Record Test (blue, "Vision test outcome"), Record Lead (yellow,
  "Customer needs glasses, not ready to buy"), Record Sale (green, "Standard or custom order"),
  and **Leads** (orange, "Convert an open lead into a sale" — see 5.7a).
- **Links** to Messages and Settings.
- **Bottom button**, one of: "Queued (N) — waiting for signal" (disabled, when offline), "Sync now
  (N)" (active, when online with a backlog), or "Synced" (disabled).

The counts refresh only on page load and after a manual sync, so they go stale while the background
timer syncs underneath.

### 5.3 Record Test — `/consultation/test`

**Client-side validation runs before submit** — required fields are checked and shown inline
before anything reaches the price-confirmation step or the network, matching the server's own
rules exactly (see 6).

Fields, in order:

| Field | Control | Rules |
|---|---|---|
| Age | Number input, min 0, no upper bound in the UI | Optional; server accepts 0–120 |
| Gender | Select: Female / Male, defaulting to Female | — |
| Outcome | Select: No glasses needed / Needs glasses / Referred, defaulting to *No glasses needed* | — |

The form then branches on outcome:

**Referred** →
- *Reason for referral* — reference dropdown; choosing the "Other" row reveals a "please specify"
  text field. Required, must be an active Referral reason, free text required when Other is chosen
  (≤ 200 chars).
- *Referral location (hospital, clinic, etc.)* — free text, required (≤ 500 chars), now enforced
  and marked in the UI as well as the server.

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
at all. A Test opened via `?fixOutboxId=` (from the failed-records review screen) pre-fills every
field from the originally-queued payload.

### 5.4 Record Lead — `/consultation/lead`

Client-side validation as above. Fields: Age, Gender, **Full name**, **Phone number**, Occupation
(optional), a consent checkbox ("Customer consents to be contacted by DOT Glasses for
follow-ups/marketing"), **Reason not purchased** (reference dropdown + Other free-text), the
shared **lens range selector** with "No preference yet" permitted, and — only when the chosen
range is *not* a preset — **Coating preference (optional)**.

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

Client-side validation as above. Fields: Age, Gender, **Full name**, Phone number *(optional
here)*, Occupation, consent checkbox, then the shared **lens range selector** with no "no
preference" option — a Sale must always have a range.

**Two ways a Sale gets linked to a Lead** (`SourceLeadId`):
- **Opened from the Leads worklist** (`/leads`, see 5.7a) via `?sourceLeadId=…` — every field the
  Lead actually captured (name, phone, age, gender, occupation, consent, lens/prescription
  preference if any) pre-fills. Frame colour, coating, hard case and "order from DOT Glasses"
  still need filling in fresh — a Lead has no equivalent fields for any of those.
- **Automatic match prompt** — for a fresh Sale (not already opened from a specific Lead), the app
  checks once per form visit whether the entered name + phone matches an existing open Lead. If it
  does, a card appears before the price-confirmation step: *"Existing lead found — `<name>` already
  has an open lead from an earlier visit. Convert it into this sale instead of creating a separate
  record?"* — accepting sets `SourceLeadId` and proceeds; declining continues as an ordinary
  unlinked Sale and doesn't ask again on that visit.

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
- **Hard case sold** — a checkbox; ticking it reveals a **Hard case colour** reference dropdown
  with Other free-text. Server-enforced both ways: colour required when sold, and both colour
  fields must be empty when not.

**Frame coverage is never asked** — not here and not on the admin conversion form (§4.4). The
column and the request field remain, and every Sale records the Full frame default; existing
records read back unchanged.

A coating is **always required** on a Sale. For a preset range it must be one the admin has ticked
as available for the chosen *left eye* lens's strength; for Custom, any active coating is
accepted. Note the documented simplification: one coating column exists for both eyes, resolved
against the left eye's configuration.

Submitting shows the same price-awareness confirmation as a Lead. A Sale opened via
`?fixOutboxId=` pre-fills from the originally-queued payload, same as Test/Lead.

### 5.6 The lens range selector (shared by Lead and Sale)

A single dropdown chooses the range: *No preference yet* (Leads only), *6-Lens Set*, *9-Lens Set*,
*Custom prescription*. Switching range clears every field belonging to the previous one.

The two preset options are matched to catalogues **by the catalogue's `Kind` field**
(`SixLensSet`/`NineLensSet` — see §4.6), not by name. If no catalogue holds a given `Kind`, or the
matching catalogue isn't assigned to the technician's retail point, the option is suffixed "(not
available)" and choosing it shows "This preset isn't assigned to your retail point."

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

### 5.7 Leads worklist, Settings, and other screens

**Leads — `/leads`.** Lists the technician's own outlet's **open** Leads (not yet converted), each
showing the customer's name, phone and when it was logged, with a **"Convert to sale"** button that
opens the Sale form pre-filled (see 5.5). Empty state: "No open leads at this outlet — everything's
been converted or nothing's been logged yet."

**Failed records — `/failed-records`.** Every permanently-rejected outbox item (see §6), each with
its real server-parsed error message, and three actions: **Fix & re-send** (reopens the originating
Test/Lead/Sale form pre-filled from the stored payload, for a form the record type supports),
**Re-send as is** (useful when the data was fine and only the session had expired), and
**Discard** (behind a confirm). Empty state: "Nothing to review — every record on this device has
been sent."

**Settings — `/settings`.** An **"Active location"** list, populated from the technician's real
`UserOrgAssignment` rows (not hard-coded) — tapping an unselected one switches it as their active
location (a server round trip that re-issues their JWT with the new `HierarchyPath`/`OrgNodeId`).
Disabled while anything is queued unsent, with an explanation, for the same reason sign-out is
blocked (see §6). Below that, three "· Coming soon" toolkit items (Talking points & FAQs,
Near-vision chart, Distance-vision chart) remain static placeholders. A **Sign out** button (behind
a confirm) is at the bottom, also disabled while records are queued.

**Outlet select — `/outlet-select`.** Functionally identical to Settings' location list (same
`IUserLocationClient`, real data) but nothing in the app currently links to this route — it's
real, just unreached by any in-app navigation.

**Messages — `/messages`.** Two hard-coded announcements ("Reference data updated", "Reminder").
Nothing is fetched; the "Refreshes on sync" note is aspirational. Still a placeholder.

**Not found** — the router's fallback.

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
upsert on it, so a record replayed after an interrupted sync is never duplicated — including a
record re-queued under the same Id after being fixed on the failed-records screen.

**When sync runs.** On save (best effort, immediately), on the browser's `online` event, on a
30-second background timer, and on the Home screen's "Sync now" button. Concurrent runs are
suppressed so the queue is never drained twice at once.

**Outcomes per item.**
- **Succeeded** → marked Synced and never sent again.
- **Deferred** (network error or 5xx) → left queued, logged as a warning, retried on the next cycle
  indefinitely. This is the offline case and it works as intended.
- **Rejected** (400, 401 or 403) → marked **Failed**, which is *terminal*. Excluded from the retry
  queue permanently and surfaced on `/failed-records` (§5.7) with the real, server-parsed field
  error — not just an HTTP status code.

**Client-side validation now exists on every consultation form** (Test/Lead/Sale), matching the
server's own rules field-for-field, so most invalid submissions are caught before they're even
queued. A record can still end up `Failed` from a background sync — e.g. the token expired, or a
reference-data item got retired between form-fill and send — and the review screen is exactly for
that case.

**Sign-out and location-switching are both blocked while anything is queued**, with an inline
explanation — the API stamps `TechnicianUserId`/`HierarchyPath` from the JWT presented **at sync
time**, not when the record was created, so draining the queue under a different identity would
misattribute it. This is a client-side mitigation, not a full fix — see `open-issues.md`.

**Not built**
- No offline caching of the app shell in the development build (the published build's service
  worker does cache it).
- No conflict resolution — the create-as-upsert is last-write-wins, with no version or ETag column.
- No offline login for a device's very first use (see §5.1).

---

## 7. REST API surface

Versioned at `v1`, with Swagger exposed in development only.

| Endpoint | Auth | Who | Behaviour |
|---|---|---|---|
| `POST /api/v1/auth/login` | Anonymous | Anyone | Username + password → JWT (60 min default). Failures count toward lockout; suspended accounts are refused as invalid credentials. |
| `GET /api/v1/auth/my-orgs` · `POST /api/v1/auth/switch-org` | JWT | Any authenticated user | Lists the caller's own `UserOrgAssignment` rows; switches which one is active, re-issuing a fresh JWT stamped with the new org. Rejects a target that isn't one of the caller's own assignments. |
| `GET /api/v1/tests` · `GET /api/v1/tests/{id}` | JWT | Any authenticated user | Hierarchy-scoped list / fetch. |
| `POST /api/v1/tests` | JWT | Any authenticated user | Idempotent create. Rejected with 400 if the caller has no org assignment. |
| `GET/POST /api/v1/leads`, `/api/v1/leads/{id}` | JWT | Any authenticated user | As above. |
| `GET /api/v1/leads/open` | JWT | Any authenticated user | The caller's own outlet's open (unconverted) leads — backs the Field App's `/leads` worklist. |
| `GET /api/v1/leads/match?fullName=&phoneNumber=` | JWT | Any authenticated user | An open Lead matching the given name+phone, or 204 — backs the Sale form's automatic conversion prompt. |
| `GET/POST /api/v1/sales`, `/api/v1/sales/{id}` | JWT | Any authenticated user | As above. `SourceLeadId` on create atomically links and marks the source Lead converted; a second attempt against an already-converted Lead is rejected. |
| `GET /api/v1/reference-data` | JWT | Any authenticated user | All **active** reference items across all categories. Not hierarchy-scoped. |
| `GET /api/v1/preset-catalogues` | JWT | Any authenticated user | Catalogues assigned at or above the caller's org, with each lens's available coatings and the catalogue's `Kind`. 400 if the caller has no org. |
| `POST /api/v1/client-logs` | JWT | Any authenticated user | Accepts a batch of client log entries with a correlation ID; writes them to the server log. |
| `GET/POST/PUT/DELETE /api/v1/widget-examples` | JWT | Read/update/delete: any user. Create: Admin. | Developer sandbox (see §8). |

**Capabilities reachable through the API that no UI exposes:**
- `GET` list and by-ID for Tests, Leads and Sales — nothing in either application reads these
  endpoints; the Field App only writes, and the Admin Portal queries the database directly.
- Full update and hard delete of a widget example.

**Restrictions that exist only in the UI, not the API:**
- Any authenticated user of any role or level can create a Test, Lead or Sale. The Admin Portal
  simply has no general-purpose form for it (only the narrower Lead-conversion screen, see §4.4).
- The API applies no level restriction on custom orders — a Sale posted with
  `OrderFromDotGlasses` from any Retail Point enters the fulfilment queue regardless.

Cross-origin access is restricted to two hard-coded localhost development origins.

---

## 8. Placeholder and developer-only surfaces

These exist in the running product but are not real product capability:

| Surface | Status |
|---|---|
| Field App **Messages** | Two hard-coded announcements. No backing data or API. |
| Field App **Widget Examples** (`/widget-examples`) | A developer walkthrough for the offline outbox. Lets the user type an **arbitrary hierarchy path**, which the API accepts as-is (unlike Test/Lead/Sale, where the server stamps it). A `User`-role account is refused by the create policy and produces a permanently Failed outbox item. |
| **Widget Examples API** | The full CRUD sandbox behind the above. |
| **Developer user seeder** | Creates up to three accounts (DGI / Country / Retail Point) on start-up so RBAC is exercisable locally — gated behind `DevSeed:*` configuration values, sourced from user secrets, never committed and never set in production. Each account seeds independently based on which of its secrets is present. |
| **Seeded org tree** | Four nodes: DOT Glasses International → Kenya → Kangemi Vision Centre → Kangemi Vision Centre — Outreach Post. |
| **Automatic database migration on start-up** | Development only — real environments apply migrations via an explicit CI step instead. |

---

## 9. Cross-cutting notes

**No editing or deletion of transactional data.** Tests, Leads and Sales are create-once atomic
events by design — a technician who mistypes a phone number or picks the wrong frame colour has no
correction path in either application, and no admin can fix it either. This is a deliberate
product constraint (see `open-issues.md`), not an oversight.

**No customer-facing surface at all.** `Customer` is internal-only: matched by exact name + phone
within an outlet, never listed, searched, edited or merged. Near-duplicates (a phone typed with
and without a country code) silently become two customers.

**No export or reporting output** of any kind, from any screen — deliberately deprioritised; see
`open-issues.md` for the binding consent requirement that applies whenever it is eventually built.

**No audit trail is surfaced.** Created/modified user and timestamp are captured on every entity
but no screen displays them.

**No notifications** in either direction — the Field App's Messages screen is static and nothing
server-side can push to it.

**Search and paging are now present on most list screens** (Event History's Leads tab, User
Directory, Preset Catalogues) but not uniformly — Organisations' tree, Custom Orders' grouped queue
and Dashboard's top-N lists have neither, and only Event History/User Directory support true
server-side paging (Preset Catalogues' search filters an already-fully-loaded list, proportionate
to its small size).

**A user whose `HierarchyPath` is blank would match the visibility filter's prefix test against
every row and see the entire database.** All current creation paths set it, but nothing enforces
that it is non-empty at the filter itself — unlike the resource-based RBAC check, which explicitly
rejects an empty prefix.

**Records created by a DGI- or Country-level user through the API are stamped at that level** and
render as "Unknown outlet" / "Unknown country" throughout reporting, and are counted in the
Dashboard's totals.
