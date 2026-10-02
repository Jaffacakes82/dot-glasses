# DOT Glasses — Functional Capability Review

**What this is:** a functional specification of what the DOT Glasses platform actually does
today, derived from application source (controllers, views, Razor components, services,
validators, authorization handlers, domain entities and seed data).

**Reviewed at:** commit `b745220` (`main`), 2026-08-12 — updated from the original 2026-08-07
review to reflect the full 2026-08-09 roadmap (Phases 1–8, all shipped). Sections untouched by
that roadmap are carried over from the original review; everything else was re-checked against
current source. Further updated 2026-09-28 for the multi-org access and retail-point recording
change (ADR-0006): the access model, the Organisations/User Directory/Lens Sets screens, the
sidebar footer and the Field App's location handling and API surface were re-checked against
source; sections it didn't touch are otherwise unchanged.

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
entity carrying a hierarchy path — `OrganisationNode`, `Customer`, `Test`, `Lead` and `Sale`. The
rule is: *a row is visible if its `HierarchyPath` starts with any of the signed-in user's scope
paths*. This is completely role-independent. Scoping is downward only — you see your own node(s)
and everything beneath them, never anything above or beside you. **On the Admin Portal, scope is
the union of every org a user is assigned to** — being assigned to DGI and to one retail point
gives DGI's whole scope, and an overlapping record is only counted once (ADR-0006). There is no
"primary" or "active" org: every assignment counts equally, and access is re-read from the
database on each request, so a removed assignment, a changed role or a suspension bites on the
user's very next request. **The Field App scopes to one current location instead** — see §5 —
because a Test, Lead or Sale must be stamped with exactly one place.

Three entities (and the two child tables of `LensOption`, `LensOptionCoating` and
`LensOptionCoatingPairing`) are **not** hierarchy-scoped and are therefore globally visible to
every authenticated user: `ReferenceDataItem`, `PresetCatalogue` and `LensOption`. Reference data
and lens sets are a single global library, not a per-country one. Note also that `ApplicationUser` is an Identity type, outside the automatic
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
migrated to Admin on removal, preserving their access.) A user holds one role, applying across
every org node they're assigned to — a user can be assigned to any number of nodes, none of them
special, and a level-gated screen or policy opens on the **highest** level among them.

Four org levels, ordered: `Dgi` (0) → `Country` (1) → `Intermediate` (2) → `RetailPoint` (3).
"At or above Country" means level ≤ 1, i.e. DGI or Country. Only DGI, Country and Retail Point
carry business rules; every reseller/distributor tier in between is `Intermediate`, distinguished
only by a free-text `Kind` display label.

The tree's shape is enforced: DGI's only legal child is a Country; a Country or Intermediate may
have Intermediate or Retail Point children; a Retail Point is always a leaf.

### 2.3 The five authorization policies

| Policy | Rule as coded | Gates |
|---|---|---|
| `ReferenceData.Manage` | Role = **Admin** AND level = **DGI exactly** | The whole Reference Data screen |
| `PresetCatalogue.Manage` | Role = **Admin** AND level ≤ **Country** | The whole Lens Sets screen |
| `CustomOrders.View` | **Any role** AND level ≤ **Country** | The whole Custom Orders screen, *and* its Advance-status action |
| `Organisations.ManageInScope` | Role = **Admin** AND target org's path is at/below the caller's | Every Organisations write action, per node |
| `Users.ManageInScope` | Role = **Admin** AND target user's path is at/below the caller's | Every User Directory write action, per user |

One design point worth stating explicitly, because it's deliberate: **`CustomOrders.View` is the
only policy where a plain `User` gets access to something an Admin below Country level does
not** — a `User` attached at Country level can advance fulfilment status; an Admin attached to a
Retail Point cannot even see the queue. It's the only policy in the table that's level-gated but
not role-gated.

Dashboard, Organisations, Event History and User Directory carry only a bare `[Authorize]` —
every authenticated user reaches them, and what they see is narrowed by data scoping rather than
by policy. The sidebar hides Lens Sets/Custom Orders/Reference Data per-request against
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
| **Lens Sets** | ● | ● | ✕ (redirected) | ✕ (redirected) | ✕ (redirected) |
| **Custom Orders** — view + advance | ● | ● | ✕ (redirected) | ✕ (redirected) | ● |
| **Reference Data** | ● | ✕ (redirected) | ✕ (redirected) | ✕ (redirected) | ✕ (redirected) |
| **Field App** — record Test/Lead/Sale, convert Lead | ✕ unless also assigned to a retail point | ✕ unless also assigned to a retail point | ● at an assigned retail point | ● | ✕ unless also assigned to a retail point |

**Recording only ever happens at one current location, and it must be an active retail point the
technician is directly assigned to** — a broader Admin Portal scope never widens where someone can
record (ADR-0006). A DGI or Country admin with no retail-point assignment of their own sees the
Field App's "can't record here" screen (§5.7) instead of a form, and the create endpoints refuse
the same way if a stale or tampered client tries anyway — see §5.7 and §7 for the exact refusal
messages. This replaced an earlier behaviour where any authenticated user could record through the
API regardless of role or level, stamped to whatever org they happened to be attached to (a DGI
Admin's Sale used to resolve as "Unknown outlet"/"Unknown country" everywhere) — that gap is what
this change closed.

**Navigation now reflects permissions.** The sidebar filters Lens Sets/Custom Orders/
Reference Data per the policies above; a direct hit on a blocked route (bookmarked, typed) renders
a real Access Denied page.

**The sidebar footer shows what your access is based on.** Under the signed-in user's email, a
role line and an assignments line — the assigned org names, sorted highest level first then
alphabetically, shortened to the first three with a "+N more" suffix once there are more than
that. The email itself is truncated with an ellipsis rather than overflowing the nav.

---

## 4. Admin Portal — screen by screen

### 4.1 Sign-in and account pages

**Login** — `/Account/Login`, anonymous.

Two fields, both required: Email and Password. Optional `returnUrl`, honoured only
if local. Sign-in is persistent (a lasting cookie) and counts failures toward lockout. On success
`LastLoginUtc` is stamped and the user lands on the Dashboard or their return URL. On any failure
— wrong password, unknown user, or a suspended account — the same message appears: *"Email or
password is incorrect."* Suspension is deliberately not distinguishable from a bad password.

**Sign out** — a real POST action, reachable from a button in the sidebar on every authenticated
page. Ends the cookie session and returns to Login.

**Forgot password** — `/Account/ForgotPassword`, anonymous, linked from the sign-in page.

One field, Email. Whatever is entered, the page answers *"If that email has an account, we've
sent a link."* — it never says whether the address has an account. An email is sent to an
**Active or Invited** account; none to a suspended account or an unknown address, and none if
that account was emailed in the last **five minutes** (the last-sent time is stored on the
account, so the limit holds across replicas). The link is only ever emailed, never shown on
screen. The Field App has the same thing through `POST /api/v1/auth/forgot-password` (§5.1).

**Set password** — `/Account/SetPassword?userId=…&token=…[&app=…]`, anonymous.

The target of an invite, an admin's reset, or a forgot-password link. Fields: hidden `UserId`,
hidden `Token`, `Password`. The token is a genuine ASP.NET Identity password-reset token: it works
for **one day** and stops working once the password has changed. Password rules as configured
(tightened 2026-08-12): minimum 8 characters, **at least one digit, one uppercase letter and one
non-alphanumeric character all required.** On success the account's `EmailConfirmed` is set to
true and the person is sent back to the app the link was asked for from: the Admin Portal's Login
with "Password set — you can now log in.", or the Field App's sign-in page. The link says only
*which* app; the Field App's address is configuration (`FieldApp` in `appsettings.json`, looked up
by the Admin Portal host), so a crafted link can't redirect anywhere else. An invalid or expired
token surfaces Identity's own error text. Setting a password never unsuspends an account.

**Not built**
- No MFA, no account lockout feedback.

---

### 4.2 Dashboard (MI Reporting)

**Route** `/` · **Access** any authenticated user · **Data** automatically scoped to the viewer's
subtree.

Three filters sit above the tiles, sent as query-string parameters and applied together: a
**date range** (From/To — leave both blank for all-time), a **Country** and a **Retailer**. The
two dropdowns offer only what the viewer's scope contains: a country admin sees their own country
and its retailers, and someone below Country level is still offered the country they sit in.
Choosing a country narrows the Retailer choices to that country. **"No retailer"** is offered
where a retail point hangs directly off a country, and selects exactly those retail points. A
Retailer is matched by position in the tree, so choosing a distributor includes every retailer
beneath it. A value outside the viewer's scope (a hand-edited address) shows zeroes, never anyone
else's figures. "Clear filters" resets all three.

A choice narrows **everything** on the page: the six tiles, Referrals logged, the trend, the gender
split and the four lists. Six stat tiles across the top:

| Tile | Exactly what it counts |
|---|---|
| Pending leads | Leads where `ConvertedFlag` is false |
| Total tests | All visible Tests |
| Standard sales | Sales with **no custom order** behind them |
| Custom orders | Custom order records (§4.7), **paid or not**, by the date the order was **placed** |
| Test-to-sale conversion | % of all Tests that reached a Sale, walking `Test.ConvertedToLeadId` → `Lead.SaleId` |
| Needed-to-sale conversion | Same numerator rule, but the denominator is only Tests with outcome *Needs glasses* |

The two order tiles never overlap. A Sale that converted a Lead whose lens was already ordered
belongs to the order's tile, not to Standard sales; and an order placed from a Lead counts from the
day the Lead was recorded, whether or not anyone has paid since.

There is no direct Test → Sale link in the data model, so **both conversion figures only count
Tests that were converted via the Lead route.** A technician who records a Test and then records
a Sale separately produces no conversion, by design of the data model as built. The
conversion-*measurement* universe narrows with the date filter, but whether a given Test counts as
converted at all is evaluated against the **full, unfiltered** Lead/Sale history — narrowing the
date range narrows which Tests are being measured, not the facts used to decide if each one
converted.

**Four of the six tiles, plus Referrals logged, are clickable** — each links through to the
matching Event History tab (or the Custom Orders screen, for the Custom orders tile), carrying
the current date filter along. The Country and Retailer are **not** carried — those screens have
no such filter — so while one is chosen a line under the tiles says the linked screens list more
than the figures count.

Right-hand column, three cards:
- **Referrals logged** — counts **customer journeys**, not records. A Test continued into a Lead,
  and a Lead converted into a Sale, are one journey (`Test.ConvertedToLeadId`, `Lead.SaleId`), and
  "Referred or treated" is asked afresh at each step, so the same referral is often on two or
  three records. A journey counts once when any of its referred records falls in the date range; a
  record with no link is a journey of its own. Links to Event History's Referrals tab, which lists
  the records themselves and says so.
- **Conversion trend (last 6 weeks)** — six bars, each a rolling 7-day window ending at "now",
  showing that window's test-to-sale conversion %. **Always the real last 6 weeks, unaffected by
  the date filter above** — a "trend over time" widget re-scoped to an arbitrary custom window
  would defeat its own purpose — though it does follow the Country and Retailer. Bar height is the
  percentage; the tooltip is the raw number. No axis, no dates, no labels.
- **Gender split** — a two-segment bar computed from `Test.Gender` only. Only Female and Male exist
  in the domain; there is no third value or "unspecified".

Main card, **Top performing** — four top-5 lists (not clickable). Each row shows a name and four
figures: **Tests, Leads, Sales and Conversion**. Conversion is the tiles' definition applied to
that row: of the Tests recorded there, the share that reached a Sale through a Lead — so it can
never exceed 100%, and a row with Sales but no Tests shows 0%. A switch on the card ranks the lists
by **Most sales** (the default) or **Best conversion**; it is kept with the other filters, and
ranking by conversion leaves out rows with no Tests. The four lists:
- **Top outlets** — exact hierarchy-path match on the org node.
- **Top retailers** — the **Retailer**: the nearest `Intermediate`-level ancestor.
- **Top countries** — the `Country`-level ancestor.
- **Top technicians** — the recording user's full name, falling back to their username.

Unresolvable names appear as "Unknown outlet" / "Unknown retailer" / "Unknown country" rather than
failing. Sales at a retail point that sits directly under a Country are ranked under a separate
**"No retailer"** row — that outlet genuinely has none, and reporting says so rather than
substituting the country (2026-09-05). "No retailer" and "Unknown retailer" are different rows
carrying different facts: the first means "there is none", the second "we cannot resolve this
path". If all four lists are empty the whole card collapses to "Nothing recorded yet."

**Organisations flagged `IsTrainingOrg` are excluded from every figure on this page** — the tile
counts, the conversions, the trend, the gender split and all four rankings. Training exclusion is
applied on this screen only.

**Not built**
- No outlet, technician or role filters — the date range, Country and Retailer are the three.
- Top-performing lists stay non-interactive — no drill-down, and no custom orders or pending
  leads per row.
- No export (CSV/PDF), no scheduled or emailed reports.
- No retail-point-type distribution or filter — no such taxonomy exists in the domain.
- Training-org exclusion is *not* applied to Event History, Custom Orders or the User Directory's
  sales counts — those still include training data.

---

### 4.3 Organisations

**Route** `/Organisations`, `?selectedId=` to select a node · **Access** any authenticated user
can view; write actions require `Organisations.ManageInScope`.

**Left panel — the tree.** Rendered recursively with 24px indentation per level, a coloured dot
per level (DGI black, Country blue, Retailer/distributor orange, Retail Point green), the node
name, a yellow "Training" badge where applicable, and the level right-aligned. Levels are always
shown as words — "DGI", "Country", "Retailer/distributor", "Retail Point" — here, in the Add
dialog, in the org picker and in the CSV export. Children sort
alphabetically. Every node is a link that selects it.

Because reads are scoped downward only, **your own node becomes the displayed root** — an Admin
at Country level sees their own country as the top of the tree with DGI absent entirely, not
greyed out. A Retail Point user sees a single-node "tree" consisting of themselves.

A separate **"Deactivated orgs"** strip, below the tree, lists each group deactivated together
once, by its top organisation — "`<name>` and N beneath it" — with one Reactivate button. An
organisation deactivated on its own is listed by name. Organisations beneath a deactivated one
aren't offered on their own. The strip is absent when there is nothing to list.

**Right panel — the selected node.** Shows the level badge, a training badge if flagged, the name,
a Retail-Point-only note that stock is tracked externally in Zoho, and a list of users assigned to
this node, each with their role and an × to un-assign. If the viewer fails the scope
check the panel says "You don't have permission to manage this node." and no actions render.

**Actions** (all re-checked server-side; hidden buttons are never trusted alone):

1. **Flag / unflag as training organisation** — one-click toggle, available at any level. Excludes
   the node and everything beneath it from Dashboard aggregates.
2. **Rename** — a modal, `Name` only (≤ 200 chars). The level is fixed after creation.
3. **Deactivate / Reactivate** — soft-delete via the node's existing `IsDeleted` flag.
   **Deactivating takes every active organisation beneath it too**, in one step, after a
   confirmation that states how many organisations go with it, how many people hold an
   assignment in it, and that unsent Field App records for its retail points will be refused.
   Assignments are kept; they give no access while the organisation is deactivated and work again
   afterwards. **Reactivating restores exactly the group that was deactivated together** — an
   organisation beneath it that had been deactivated separately stays deactivated — and is refused
   ("Reactivate the organisation above it first") while the organisation directly above is
   deactivated, so nothing comes back outside the tree. Nothing is ever re-parented and no path
   changes. An organisation your own access comes through can't be deactivated — including the
   root — because you couldn't undo it.
4. **Add child node** — a modal. Fields: `Name` (required, ≤ 200 chars), `Level` (a select only
   when more than one level is legal; a hidden
   field with an explanatory line when exactly one is; the button is hidden entirely for Retail
   Points). Level legality is enforced three times over — in the UI, in the validator, and in the
   service. The new node's hierarchy path is *parent path + the next value of a database
   sequence*, so segments are globally unique rather than per-parent, and a segment is never
   handed out twice — not even one belonging to a deactivated org, which keeps its path in case it
   is reactivated. The database also refuses two orgs on one path outright.
5. **Assign users** — a modal listing, as tick boxes with a filter box, every **Active** user in
   the caller's scope who isn't already assigned here, each with their role and email. One submit
   assigns everyone ticked, all or nothing: if any one of them can't be assigned (not Active, or
   not visible to the caller), nobody is. A line under the title says what the assignment grants —
   at a Retail Point, recording there in the Field App; higher up, Admin Portal access to that
   organisation and everything beneath it. Invited and Suspended users are assigned from their
   Edit page (§4.5). Every assignment counts equally toward the user's Admin Portal scope and
   highest level — there is no "primary" assignment to designate, and this has no bearing on the
   Field App's separate, per-device *current location* (see §5.7).
6. **Un-assign a user** — the × next to a name in the "Assigned users" list, one at a time; at a
   Retail Point it asks first, since it applies at once and unsent Field App records for that
   retail point will be refused. Refused (with an inline error, not a crash) if it is that user's
   **last** remaining assignment — a user always keeps at least one; suspending them from User
   Directory (§4.5) is how all of their access is removed instead — and if it is your own
   assignment and removing it would shrink your scope.

**Reports and deactivated organisations.** Records made at an organisation that was deactivated
afterwards keep counting on the Dashboard, in Event History and in Custom Orders, under the
organisation's real name followed by "(deactivated)". The User Directory marks an assignment to a
deactivated organisation the same way.

**Not built**
- No change of level after creation (only the name).
- No move/re-parent.

---

### 4.4 Event History

**Route** `/EventHistory?tab=…&search=…&fromDate=…&toDate=…&page=…` · **Access** any authenticated
user · **Data** automatically scoped to the viewer's subtree. Four tabs, 25 rows per page, plus a
date range filter shared with the Dashboard's drill-down links.

**Sales tab** — columns: Type badge (green "Sale"), customer name, **Consent** (Yes/No, from
`ConsentGiven`), outlet, country, **Lens range** (the lens set's name, or "Custom"), **Lens power
LE** and **Lens power RE** (each eye in the one lens-power format, e.g. `SPH -1.25 CYL -0.75 × 90
ADD +2.00`; "—" where the record holds no power), absolute local timestamp (`yyyy-MM-dd HH:mm`).
Newest first.

**A "Training" badge** follows the outlet name on every tab, on any row recorded at a training
organisation or beneath one — the rows the Dashboard leaves out, so a reviewer can see why the two
screens disagree. There is no filter for them.

**Tests tab** — Type badge, outlet, country, timestamp. **There is no Name column at all** — `Test`
carries no customer reference of any kind (the unused `CustomerId` field was removed); Tests are
genuinely anonymous records, not just displayed without a name.

**Leads tab** — columns: name, masked phone, outlet, reason not purchased, **Aware of price**
(Yes / No, or "—" for a Lead recorded before the question existed), **Lens range**, **Lens power
LE**, **Lens power RE**, **Consent**, a
**convert-to-sale action** (shows "Converted" once done, otherwise a "Convert to sale" link
opening the admin conversion form described below), relative "Logged" time ("just now", "N
minutes/hours/days ago", falling back to an absolute date beyond a week). Phone masking keeps the
first 4 and last 3 characters and replaces the middle with a fixed 4-character redaction; numbers
of 7 characters or fewer are shown unmasked, and a missing number shows "—". Includes a search box filtering on customer full name, applied at the database level
*before* paging so page numbers stay meaningful. The match is **case-insensitive** (`ILIKE`).

**Referrals tab** — columns: outlet, country, reason, absolute time, preceded by a note that these
are tracked for government reporting, and that each record is listed — a customer referred at
their test and again as a lead appears twice here, where the Dashboard's "Referrals logged" counts
them once. This is a filtered view of the same Tests data
(outcome = *Referred*), not a separate record type — a referred test correctly appears in both
tabs.

The **admin conversion form** (`/Leads/Convert/{id}`) asks for the Sale fields a Lead has no
equivalent for — coating, frame colour, hard case, "order from DOT Glasses", and the lens range
where the Lead captured no preference. **For a Lead whose lens is already ordered** the coatings
and the order tick are replaced by a read-only "This lens is already ordered" block with the
order's status and the coatings ordered; the Sale keeps the Lead's lens and coating set whatever
is posted, and shares its order. Either way the form also asks **referred or treated**, with a referral reason, its
"Other" free text, a treated-in-facility flag and an optional referral location, following exactly
the same rules as every other capture path. That block is the form's last section, after hard case,
as on the Field App. The frame colour dropdown offers the adult list, or the children's list when
"Children's frame" is ticked (the Lead's own answer when its lens carries over), and a colour from
the other list is refused on submit. Frame coverage is **not** asked here,
matching the Field App's Sale form; the sale records the Full frame default. Outside the lens
section every field is rendered unconditionally with its condition stated in the label — the
rules are enforced server-side and reported as a validation summary on submit, not by live
show/hide.

The **lens section** has the Field App's choices in the Field App's order (§5.6): lens range; the
lens (or a Custom prescription); children's frame; pupil distance; coatings; and last, "Order this
lens from DOT Glasses (Custom range only)", which stays unconditional as the form's own rule.
- *Lens set* — "Same lens for both eyes" (ticked to start) with one **Lens** dropdown, or unticked
  a left and a right dropdown with the right one limited to the left's lens type. Lenses are listed
  by their label in the fixed display order, and the chosen lens's power reads underneath
  (`SPH +2.50 · CYL -0.75 × 90 · ADD +2.00`). Pupil distance is a 0–4 dropdown (0–2 with a
  children's frame).
- *Custom prescription* — the shop's dropdowns for sphere, cylinder, axis and add, an axis only
  when the cylinder isn't 0.00, lens type radios only with an add above 0.00 (with a text box for
  "Other"), and pupil distance as a millimetre dropdown.
- *Coatings* — only the coatings both chosen lenses come in. A paired coating is ticked and locked
  ("Comes with `<trigger>` on this lens"); an exclusion is enforced by the rules on submit. Before a
  pair is chosen (Custom, or a lens still to pick) every coating is listed and the rules decide on
  submit.
- *A Lead's own lens* is carried over by matching its power and lens type against the set. One that
  is no longer in the set leaves that eye empty under "The `<power>` on this Lead is no longer in
  `<set>`. Choose a lens.", and that note stands until the eye has a lens.
- The choices are progressive enhancement: `wwwroot/js/lead-conversion.js` shows and hides the
  blocks, refills the dropdowns, and fetches the offered coatings from
  `GET /Leads/Convert/{id}/coatings?left=&right=` (hierarchy-scoped through the Lead; it returns
  what the shared rule says, never restating it). Without the script every block is shown and the
  server validates and re-renders, so changing range or unticking "Same lens" then needs a submit.
  A refused field shows its message under its own control as well as in the summary.

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
- No row detail view — you cannot open the underlying Test, Lead or Sale from here (except Leads'
  convert-to-sale action, which is a write path, not a detail view).
- Training-org data is included here (unlike the Dashboard), marked with the "Training" badge;
  there is no filter for it.
- Lens type and coatings are in the CSV only, not on screen.

**CSV export** follows the open tab, search and date range. The Sales and Leads exports hold the
lens in separate columns so a spreadsheet can sort and filter on them: Lens range; Sphere,
Cylinder, Axis and Add for the left eye, then the right; Lens type; Coatings (an ordering Lead's
full set, otherwise a Lead's one preference). Every tab's export has a **Training org** Yes/No
column, and the Leads export has **Aware of price**. The screen and the export are fed by the same
rows.

---

### 4.5 User Directory

**Route** `/UserDirectory?search=…&role=…&status=…&page=…` · **Access** any authenticated user can
view the list; every write action requires `Users.ManageInScope`, which — unlike seeing the row at
all — checks *every one* of the target user's assignments against the viewer's scope, not just
one (so a country admin can't act on a DGI admin who also happens to hold a retail-point
assignment in that country). Listing applies the scope-paths filter manually, since
`ApplicationUser` is outside the automatic query filter.

**The table** — Name, Role, Organisations, Last login, Sales, Status, actions. A search box (name or
email), a Role filter, a Status filter and Previous/Next paging sit above it.

- **Name** is `FullName`, falling back to username where absent.
- **Organisations** is a set of badges listing the org names from the user's `UserOrgAssignment` rows —
  every assignment counts, there is no primary among them. A viewer sees the name of an assignment
  that's within their own scope; one outside it renders as a badge reading "Outside your scope"
  rather than disclosing an org name the viewer can't otherwise see. A user is listed at all if
  *any one* of their assignments is in the viewer's scope, but every action below needs *all* of
  them to be.
- **Last login** is stamped on both sign-in paths — the Admin Portal cookie login and the Field
  App's API login — so it is populated for field technicians who never open the portal. Shows "—"
  if never.
- **Sales** counts `Sale` rows recorded by that user.
- **Status** is derived, never stored: **Invited** if the account has no password hash at all;
  otherwise **Suspended** if the lockout end date is in the future; otherwise **Active**.

**Actions per row.** An Admin sees **Edit** on every row they can see (below). The other two are
shown only where the viewer passes the all-assignments scope check:

1. **Reset password** — generates a fresh Identity reset token and a `/Account/SetPassword` link,
   attempts email delivery (see below), and displays the link on screen regardless. Does *not*
   clear the existing password; the old one keeps working until the link is used.
2. **Suspend / Unsuspend** — implemented with Identity's own lockout mechanism (lockout end set to
   maximum, or cleared). A suspended user is refused at both the portal and the API login, with
   the generic invalid-credentials message, and — unlike a removed assignment or a role change,
   which bite on the user's next request — a signed-in session is also cut off on its very next
   request, both apps, rather than waiting for the cookie or JWT to expire. Suspension is the only
   way to remove all of a user's access; there is no delete. You can't suspend yourself.

**Edit user** — `/UserDirectory/Edit/{id}`, Admins only. One page for a user's **full name**,
**role** and **organisations**; the email is shown and can't be changed. Invited, Active and
Suspended users can all be edited, and editing never unsuspends or emails anyone.

- **One Save applies only what changed since the page loaded.** The form carries the name, role
  and organisations it was rendered with, and the server works out the differences — so an
  assignment another admin added in the meantime is left alone. Everything is written in one
  transaction.
- **Each kind of change keeps its own permission.** Changing the role or the name acts on the
  user as a whole, so it needs *every* one of their assignments in the admin's scope; adding or
  removing one organisation needs only that organisation in scope.
- **A user partly outside the admin's scope** shows a read-only role and name with the reason,
  and a line "plus N organisations outside your scope" — those are never named and never changed.
  An admin may remove every assignment they can see as long as the user keeps one elsewhere; the
  directory then says the user is no longer in their scope. With none left anywhere, the save is
  refused with the "suspend them instead" message.
- **Confirmation.** Saving asks first when a Retail Point assignment is being removed or an Admin
  is becoming a User, naming the consequence.
- **Editing yourself.** Your own name can be changed. Your own role is read-only ("Another admin
  must change it"). You may add an assignment for yourself, and remove one only when your scope
  is no smaller afterwards — that is, the organisation sits beneath another assignment you keep.
  The same rules apply to assigning and un-assigning yourself on the Organisations screen.
- **The organisation picker**, shared with Invite: the admin's organisations as an indented tree,
  each with its level, a filter box, and a line explaining that only a direct Retail Point
  assignment lets someone record in the Field App. A deactivated organisation is never offered;
  an existing assignment to one is shown ticked, marked "deactivated", and can be unticked.
- Every role, name or assignment change — from here or from the Organisations screen — writes a
  line to the application log naming who made it, on whom, and what changed. There is no history
  screen.

`UserDirectory/ChangeRole` remains as a role-only POST endpoint under the same rules.

**Invite platform user** — a modal, opened from a button that renders for *every* viewer
regardless of permission (the refusal happens on submit). Fields:

| Field | Rules |
|---|---|
| Full name | Required, ≤ 200 characters |
| Email | Required, valid email format, ≤ 256 characters, must not already exist |
| Organisations | The shared organisation picker (see Edit user): every active org in the caller's own scope, indented by depth with its level, and a filter box. At least one required — refused with "Choose at least one organisation." otherwise — and every selection re-validated as in-scope server-side. Ticking more than one box is meaningful: every checked org becomes a real, equal assignment, with no ordering and nothing "primary" among them. |
| Role | Select: **Admin / User**, defaulting to User |

On submit the system creates the account **with no password at all** (which is what "Invited"
means), adds a `UserOrgAssignment` row for every selected org — nothing is written onto the user
row itself, and access is read from these assignment rows fresh on every request rather than from
anything stamped at invite time — generates a reset token, and builds a set-password link.

**Email delivery is real when Azure Communication Services is provisioned** (staging/production,
once the user has run the deploy that provisions `acs.bicep`); locally, and in any environment
where ACS hasn't been provisioned, the email sender is a logging stub. Either way, **the
set-password link is always also shown once in a banner on the page** — real delivery doesn't
remove that fallback, since it's the only way to recover if the email genuinely doesn't arrive.
Reloading the page loses the on-screen copy; the only recovery at that point is Reset password,
which mints a new one.

**Not built**
- No edit of a user's email.
- No history screen for changes to a user (they are in the application log only).
- No delete or deactivate (only suspend).
- No resend of an existing invite without invalidating it.
- No bulk invite or import.
- The invite button is shown to users who cannot invite.

---

### 4.6 Lens Sets

**Routes** `/Catalogues?status=…&search=…` (the list) and `/Catalogues/Details/{id}` (one lens
set) · **Access** `PresetCatalogue.Manage` — Admin at DGI or Country
level. Everyone else is redirected to Access Denied.

Catalogues themselves are **not** hierarchy-scoped: every user who reaches this page sees every
catalogue in the system, regardless of who owns it. The screen has two tabs: **Lens sets** (the
list) and the read-only **Lens powers** described at the end of this section.

**The list.** One row per lens set: its name (a link to its page) with the description beneath,
the org that owns it, how many lenses it has ("No lenses yet" for an empty one, which the Field
App doesn't offer), how many orgs it is directly assigned to (not the outlets those assignments
reach), and its status. A **Status** filter — Active (the default), Retired or All — and a name
search narrow the rows, and combine. A retired row carries **Reactivate** for a caller who may
edit that lens set. **Create lens set** is here; a created lens set opens on its own page.

**A lens set's page.** Reached from the list, with a "Lens Sets › <name>" breadcrumb back instead
of tabs. Every write made from it — adding, editing or removing a lens, renaming, assigning,
unassigning, reactivating — comes back to the same page, and so does a refused lens that reopens
the Add lens dialog. Retiring returns to the list. An id that names no lens set goes back to the
list.

**What a lens is.** A lens set is a list of lenses, and a lens is a **lens power** — a sphere and
optionally a cylinder, axis and add, the same values a Custom prescription uses (ADR-0007) — with a
label the technician chooses it by, a **lens type** when it has an add (Bifocal, Progressive or
Other; a lens with no add is single vision and is never asked), the **coatings** it comes in
(at least one) and its own **pairings**. A set may repeat a power only with a different lens type.
Nothing links a lens to a reference-data list any more.

**What the page shows** (a lens set is a `PresetCatalogue` in the code) — a header with the name
and, where the caller may change the set, **Edit lens set** (name, description) and **Retire**;
a line naming the org that owns it and how many lenses it has, then its description. Below that,
one full-width card, "Lenses in <set>", with a table of the set's lenses —
Label, Lens power (`SPH +2.50 · CYL -0.75 × 90 · ADD +2.00`, only the parts the lens has), Lens
type ("Single vision" when there is none), Coatings (a chip each) and Pairings (a chip each,
`Blue block → Photochromic`, or "—") — in the fixed display order: single vision by
sphere, then Bifocal, Progressive and Other by add, then sphere. Each row has **Edit** and
**Remove**, and the card an **Add lens** button, where the caller may change the set; a set with
no lenses says so. The table scrolls inside its card on a narrow screen. There is no
per-catalogue role: every non-empty lens set assigned at or above a retail point is offered there
(see §5.6, ADR-0005).

**Who may change what.**
- **Editing a lens set** — Edit, add, edit or remove lenses, Retire/Reactivate — is limited to
  admins at or above its **owning org** (`PresetCatalogue.EditInScope`). A DGI-owned lens set
  reaches every country, so a country admin can't change what it contains. On a lens set the
  caller can't edit, those actions aren't shown and the page says "Owned above your organisation —
  you can assign it, but not change it." The server refuses them regardless, with Access Denied.
- **Assigning** any active lens set, and removing an assignment, is open to any admin who reaches
  the screen, but only for orgs within their own scope (`PresetCatalogue.AssignInScope`).

- **Create lens set** / **Edit** — a modal with two fields: `Name` (required, ≤ 200, and
  **unique among active lens sets**, ignoring case — "A lens set with this name already
  exists."; a retired lens set's name is free) and `Description` (≤ 500). The old free-text
  diopter-range field was removed (2026-09-26): nothing read it, the device never saw it, and the
  lens table on the same card already states the range exactly. On create, the owning org
  is chosen from the caller's own DGI-or-Country-level assignments: an **"Owning org"** select
  appears in the modal only when the caller qualifies through more than one such assignment; with
  exactly one, it's used automatically and the field stays hidden. The server independently
  re-checks that a posted choice is really one of the caller's own DGI/Country assignments,
  refusing anything else.
- **Add lens** / **Edit** (one dialog, opened from a card's *Add lens* button or a row's *Edit*).
  It is built the way the online shop's configurator is, with every list read from the lens power
  values (see *Lens powers* below): *Spherical power* (required), *Cylindrical power* (0.00 first,
  never positive), *Axis* (enabled only when the cylinder isn't 0.00, and required then), *Add near
  vision power* (0.00 first, and 0.00 means no add), *Lens type* radios (only with an add above
  0.00; "Other" reveals a text box, ≤ 200), a **Label** shown to technicians (required, ≤ 100, and
  **unique within the set**, ignoring case), the **coatings** the lens comes in (at least one, each
  an active Coating; the global exclusions are listed as a note) and any number of **pairings**
  ("ticking the first coating adds the second, which then can't be unticked" — both must be ticked
  for this lens, not the same coating, not listed twice, and not forbidden by a global exclusion).
  No other lens in the set may have the same power **and** lens type; the same power can appear
  again with a different lens type. Every problem is reported at once, next to its own field, with
  the dialog reopened on what the admin typed; a save that passes returns to the screen with
  `Lens "X" added.` or `Lens "X" saved.` A lens with no add shows "single vision" and takes no
  lens type. Editing a lens removed since the page loaded is refused with a sentence, not an
  error page; editing a lens through another lens set's id is Access Denied. The server owns
  these checks (the same lens power rules a Custom prescription is held to); the dialog's script
  only keeps the controls consistent.
- **Remove lens** (a row's *Remove*, after a confirmation) — a **hard delete** of the lens and its
  coatings and pairings, not a retire. Records never point at a lens — each keeps its own copy of
  the power it was sold with — so no history is touched.
- **Retire** (in the page header, with a confirmation) — a soft delete. The lens set stops being
  offered in the Field App (from its next reference-data refresh). Its **assignments are kept**.
  Records that used it still show its name and lens powers, and the server refuses a *new* record
  on it ("This lens set has been retired — choose another lens range."). A retired lens set's page
  is **read-only**: its lenses and assignments are shown as they were, and the only action is
  **Reactivate**, which restores it exactly as it was. The server refuses every other write to a
  retired lens set — renaming, changing a lens, assigning, unassigning — with "This lens set is
  retired — reactivate it before …".

**Assigned to** (a card on the lens set's page) — the orgs the lens set is directly assigned to, a
chip each with an un-assign action where the caller may unassign there, and an **Assign to another
organisation** picker with an Assign button: one org at a time, from the `Intermediate` and
`RetailPoint` nodes in the caller's scope it isn't assigned to yet. Re-assigning an existing pair
is a silent no-op. **Assignment cascades downward** — assigning to an Intermediate makes the lens
set available to every Retail Point beneath it. A successful assign returns to the page with a
green confirmation ("Lens set assigned.").

**Coatings and pairings live on the lens.** There is no global coating grid and no global pairing
list. A technician choosing lenses from a set is offered only the coatings **both** chosen lenses
come in, and both lenses' pairings apply (§5.6). A lens with **no** coating can't be sold on a lens
set at all — the Add lens dialog requires one, and the API rejects a sale on a lens with none.
Exclusions are the one global coating rule (Reference Data, §4.8) and apply everywhere, including
a Custom prescription; saving a pairing an exclusion forbids is refused, and so is adding an
exclusion a lens's pairing contradicts.

**Lens powers** (`/Catalogues/LensPowers`, the screen's second tab; same policy as the
rest of the screen) — a read-only page listing every value a lens power can take (sphere,
cylinder, axis, add, pupil distance), each as one scrollable column under a one-line summary of
its range and step, and the validity rules (axis only with a cylinder; lens type only with an
add). It renders straight off the definition the Field App, the Admin Portal and the server all
share (`LensPowerValues`), so it can never disagree with them. The values are copied from the DOT
Glasses online shop and change only with a release: sphere -10.00 to +10.00, cylinder 0.00 to
-6.00, add 0.00 to +3.00, all in 0.25 steps; axis 0–180 whole degrees.

**What ships.** Production and staging have **no active lens sets** until DGI builds them on this
screen: the lens-set redesign retired every earlier set and removed the old lens list, and no
lens set is ever created by a migration. Development and the test hosts get two example sets (a
"6-Lens Set" and a "9-Lens Set", DGI-owned and assigned to Kenya) so the screens have something to
show.

**Not built**
- No delete or archive of a catalogue.
- No reordering of lenses within a catalogue — the display order is fixed.
- Catalogues cannot be assigned to Country or DGI nodes from this screen (only Intermediate and
  Retail Point), even though the seeded assignments are at Country level and the cascade logic
  supports it.

---

### 4.7 Custom Orders

**Route** `/CustomOrders?status=…` · **Access** `CustomOrders.View` — **any role**, at DGI
or Country level only. Hidden entirely below that. The same policy gates both viewing and
advancing status.

**A custom order is a record of its own** (ADR-0008). It is placed when a Lead or a Sale is
recorded as a Custom prescription with "Order this lens from DOT Glasses" ticked, and it points
back at that record; the lens, coatings, pupil distance and customer are read from the Lead or
Sale, never copied. The queue lists every order, whichever placed it. Unpaged (custom-order volume
is naturally small), with a status-filter pill row (`Submitted` / `In Lab` / `Ready for Pickup` /
`Fulfilled`) above the list.

An order placed from a Lead that has not yet converted carries a **"Not yet paid"** badge beside
its status: the lens is being made and nobody has paid for it. When that Lead is converted, the
Sale is linked to the same order — there is never a second one — and the badge goes. An unpaid
order advances through the lab like any other. There is no cancel.

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
automatically at the moment the Lead or Sale is recorded. Once Fulfilled the button disappears, and the
service refuses any further advance. There is no way to set an arbitrary status.

A refused advance — the order was already Fulfilled (a colleague got there first, a double click,
a browser resubmit), or it isn't visible to the caller — comes back as a
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

Eight category cards in a fixed display order, each with an explanatory scope note:

| Category | Where the values are consumed |
|---|---|
| Reasons not purchased | Field App Lead form (required) |
| Referral reasons | Every recording form, when "Referred or treated" is ticked |
| Coatings & tints | Lead coating preference and Sale coating, and the coatings each lens set lens is ticked for on Lens Sets |
| Frame colours (adult) | Sale frame-colour swatches, unless "children's frame" is ticked |
| Frame colours (child) | Sale frame-colour swatches when "children's frame" is ticked |
| Hard case colours | Sale, when a hard case is sold |
| Occupations | Optional on Test, Lead and Sale |
| Lens types | Bifocal / Progressive / Other, asked when a lens has an add — on a Custom prescription and on a lens set lens alike |

There is no Lens strengths card and no Pairings section: a lens set lens is a lens power with its
own label, coatings and pairings, all set on Lens Sets (§4.6). The **Coatings & tints** card also
carries **Exclusions (can never be selected together)** — pairs of coatings that no record may
hold together, added and removed here and enforced everywhere, on a lens set and on a Custom
prescription alike. Adding an exclusion is refused if a lens set lens pairs those two coatings
("Can't add this exclusion — a lens in a lens set pairs these two coatings."). None ship.

**Each card shows** its active options as chips — with a circular 18px thumbnail where the option
has a picture, on the two Frame colours cards only — each carrying a **pencil icon to edit** (label
and picture only — category, code and the Other flag stay fixed after creation), **↑/↓ buttons to
reorder**, and a × to retire it, plus a collapsed "Retired (N)" section with a Restore link per
option.

**Removing an option retires it; it is never deleted.** Historical Tests, Leads and Sales may
reference it by ID, and Event History deliberately resolves labels against retired items too.
Retired options disappear from every Field App dropdown immediately but remain restorable.

**The add form** on each card:

| Field | Rules |
|---|---|
| Label | Required, ≤ 200 characters. The machine code is auto-slugified from it (lowercased, non-alphanumeric runs → hyphens). Sort order is assigned as (current maximum in category + 1). |
| Picture | **On the two Frame colours cards only.** A file upload: PNG, JPEG or WebP, up to 1 MB, checked by reading the file's own signature rather than its name. Refused with "Upload a PNG, JPEG or WebP picture." or "Upload a picture of 1 MB or smaller." There is no address to type. |
| "Mark as this category's Other option" | A checkbox. Disabled with the note "(already set — retire it first)" when the category already has an active Other option, and independently enforced server-side. |

**Pictures.** An uploaded picture goes into the private `reference-data-images` storage container
under a newly generated name, and the option's picture address becomes
`/reference-data/pictures/<name>` — a path the Admin Portal serves anonymously (the Field App shows
these with no session) with a year-long cache lifetime, for generated names only; anything else is
a 404. The container itself has no public access. The edit dialog shows the current picture, takes
a replacement file, and has a **Remove picture** tick; replacing or removing deletes the old file.
The six adult colours seeded with pictures on the online shop's website keep those addresses and
keep displaying until DGI uploads replacements.

The Other flag matters functionally: every consuming dropdown in the Field App keys off it to
reveal a free-text "please specify" field, and the API requires that free text whenever an
Other-flagged option is chosen. Two active Others in one category would be ambiguous, hence the
one-per-category rule.

**Out of the box** the system seeds: 12 Occupations, 9 Reasons not purchased, 6 Referral reasons,
5 Coatings (Photochromic, Clear, Blue block, Polarized, Sunglasses), 7 adult Frame colours, one
child Frame colour ("Other" — DGI enters the children's colours), 3 Hard case colours, and 3 Lens
types (Bifocal, Progressive, Other). Every category except Coatings ships with
an "Other" row.

**Not built**
- No hard delete for a mistyped entry (edit covers a mislabel; retire covers removal).
- No picture for any category other than the two Frame colours lists; no resizing or cropping.
- Eight identical forms share a single page-level error banner, so a validation failure does not
  indicate which card produced it.
- Gender, frame coverage, lens range type and fulfilment status are hard-coded enumerations and
  are not editable here or anywhere else.

---

## 5. Field App — screen by screen

A Blazor WebAssembly PWA with no persistent navigation chrome — each screen is full-bleed with its
own back arrow. Authenticated with a JWT (default lifetime 60 minutes).

### 5.1 Login — `/login`

Two fields: **Email** and **PIN** (a password input). Posts to the API's login endpoint; on success
the token is **persisted to IndexedDB**, not just held in memory. Password rules (tightened
2026-08-12, same as the Admin Portal): minimum 8 characters, at least one digit, one uppercase
letter and one non-alphanumeric character.

Errors: "Email or password is incorrect." for a rejected credential, "Couldn't reach the server.
Check your connection and try again." for a network failure.

**Forgot password?** links to `/forgot-password`: one Email field, which calls
`POST /api/v1/auth/forgot-password` directly (it needs a connection, and there is nothing to
queue). The answer is always "If that email has an account, we've sent a link."; with no
connection the screen says "You're offline. Connect to get a reset link." and sends nothing. The
emailed link opens the Admin Portal's set-password page (§4.1) and then returns here, where the
sign-in page shows "Password set. Sign in with your new password." If already signed in, a green banner shows the token
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

The launcher. Redirects to login if no valid token. With a valid token but no **current location**
yet — the very first launch after sign-in, or any later launch where the remembered one is no
longer eligible — it makes one `my-orgs` call and redirects again before rendering anything else:
to `/outlet-select` (§5.7) when more than one retail point qualifies, or `/no-location` (§5.7) when
none do. A token that already carries a location (the ordinary case) skips this entirely.

- **Header** — the signed-in display name, with the current location's name underneath it (falls
  back to "Field agent" if somehow blank). This is the one place the technician's current location
  is always visible, per spec.
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

**Every consultation form's real save action shows "Recording at `<name>`" beside it** — the
bottom Save/Save-test button and the Test→Lead
"Continue as Lead" button (which also saves, before navigating on) — so the technician sees the
current location at the moment they commit a record, not just once on Home.

### 5.3 Record Test — `/consultation/test`

**Client-side validation runs before submit** — the form builds the request it would send and
runs the same shared rules the server runs, so required fields are shown inline before anything
reaches the network (see 6).

**Every recording form uses one order:** Age, Gender and Occupation open it, and "Referred or
treated" closes it. Fields on a Test, in order:

| Field | Control | Rules |
|---|---|---|
| Age | Number input | Optional; 0–120 |
| Gender | Select: Female / Male, defaulting to Female | — |
| Occupation (optional) | Reference dropdown; "Other" reveals a free-text field | Free text required when Other is chosen (≤ 200 chars) |
| Outcome | Select: No glasses needed / Needs glasses, defaulting to *No glasses needed* | — |

**Needs glasses** then shows the shared **lens range selector** (5.6) with "No preference yet"
permitted, and **Coating preference (optional)**.

**Referred or treated** comes next, whatever the outcome — a tick box that reveals:
- *Reason for referral/treatment* — reference dropdown with Other free-text. Required when the box
  is ticked.
- *Treated in facility* — a tick box. When ticked, there is no location to give.
- *Referral location (optional)* — free text (≤ 500 chars), shown when the customer wasn't treated
  in the facility. It may be left blank.

**Needs glasses** then ends with *"Did the customer share contact details?"* — a No / Yes pair of
buttons.
- **Yes** reveals **"Continue as Lead →"**, which saves the Test and opens the Lead form carrying
  `sourceTestId`, the age and gender, and the Test's referral answers as the Lead form's starting
  values (the technician can change them; each record stores its own answer). This is the only
  path that links a Test to a Lead, and therefore the only path that produces a conversion figure
  on the Dashboard.
- **No** shows "Recorded as a test only — not entered into the leads pipeline."

Saving a Test is immediate. A Test carries no customer name or phone at all. A Test opened via
`?fixOutboxId=` (from the failed-records review screen) pre-fills every field from the
originally-queued payload, the referral block included.

### 5.4 Record Lead — `/consultation/lead`

Client-side validation as above. Fields, in order: Age, Gender, Occupation (optional), **Full
name**, **Phone number**, a consent checkbox ("Customer consents to be contacted by DOT Glasses for
follow-ups/marketing"), **Reason not purchased** (reference dropdown + Other free-text), **"Has
the customer been told the price?"** (Yes / No buttons — one must be chosen, either saves, and the
answer is stored on the Lead), the shared **lens range selector** with "No preference yet"
permitted, **Coating preference (optional)**, a radio group with "No preference" first, and last
the **Referred or treated** block described in 5.3. On a lens set it lists only the coatings
both chosen lenses come in (nothing until both eyes have a lens); on a Custom prescription, or
with no range, every active coating. A preference the chosen lenses don't offer is refused by the
server against `CoatingPreferenceRefId`, and choosing different lenses clears one that's no longer
offered.

**Ordering the lens from a Lead.** When the range is Custom, one more control appears, last in the
lens section: *"Order this lens from DOT Glasses"*. Ticking it orders the lens when the Lead is
saved, before the customer pays (ADR-0008), and changes what the Lead must hold: both eyes' power,
the **pupil distance** (otherwise optional on a Lead), a lens type where there is an add, and a
**coating set** — the coating preference radios are replaced by the coating selector a Sale uses,
held to the same rules (at least one, no two that exclude each other). The order appears in the
Admin Portal queue as *Submitted*, marked "Not yet paid". A tick left over from an earlier Custom
choice is ignored once the range is no longer Custom. An order can only be placed when the Lead is
recorded, never added to an existing Lead, and the server refuses the tick on any other range.

Server rules: full name required (≤ 200), phone required (≤ 32), reason not purchased must be an
active option with its free text present if Other, the price question answered, age 0–120, and if
a `sourceTestId` is carried
it must reference an existing Test that has **not already been converted** (a second attempt is
rejected). "Existing" means existing *and visible to the caller* — a Test at another outlet is
hidden by hierarchy scoping and so is refused exactly like one that was never recorded. Either
way the conversion is refused outright: a Lead is never recorded against a source it could not
read.

The customer is matched or created server-side by exact **name + phone within the same outlet** —
a repeat visitor with identical details reuses their existing customer record rather than creating
a duplicate.

Save saves straight away; there is no step after it. The price answer does not carry into a Sale.

### 5.5 Record Sale — `/consultation/sale`

Client-side validation as above. Fields, in order: Age, Gender, Occupation (optional), **Full
name**, Phone number *(optional here)*, consent checkbox, the shared **lens range selector** with
no "no preference" option — a Sale must always have a range — then coatings, frame colour, hard
case, and last the **Referred or treated** block described in 5.3.

**Two ways a Sale gets linked to a Lead** (`SourceLeadId`):
- **Opened from the Leads worklist** (`/leads`, see 5.7a) via `?sourceLeadId=…` — every field the
  Lead actually captured (name, phone, age, gender, occupation, consent, lens/prescription
  preference if any, and its coating preference as the Sale's coating) pre-fills. A Lead's lens is
  found in the set again by matching its power and lens type; one that is no longer there is left
  unchosen under a note (§5.6). Frame colour, hard case and "order from DOT Glasses" still need
  filling in fresh — a Lead has no equivalent fields for any of those.
  **A Lead whose lens is already ordered** opens differently: in place of the lens controls, the
  coatings and the order tick there is a read-only card — *"This lens is already ordered"* with
  the order's status, each eye's power, the pupil distance and the coatings ordered. The Sale is
  sent with exactly that lens and coating set and shares the Lead's order. Frame colour, hard case
  and referral are asked as usual. The server enforces the lock on whatever is sent: a different
  lens, a different coating set or a second order request is refused against the field concerned.
  To sell a different lens, record a new Sale.
- **Automatic match prompt** — for a fresh Sale (not already opened from a specific Lead), the app
  checks once per form visit whether the entered name + phone matches an existing open Lead. If it
  does, a card appears before the Sale is saved: *"Existing lead found — `<name>` already
  has an open lead from an earlier visit. Convert it into this sale instead of creating a separate
  record?"* — accepting sets `SourceLeadId` and saves; declining saves an ordinary unlinked Sale.
  This prompt is the only step between Save and saving. When the matched Lead's lens **is already
  ordered**, the card offers no convert button — the lens just typed can't be swapped for the
  ordered one — and instead says to open that Lead from the Leads list, with "Go to Leads" and
  "Save as a separate sale" (which leaves the order unpaid).

The **Coating** list follows the lens range (5.6): a lens set offers the coatings both chosen lenses
come in, a Custom prescription offers **every** active coating. **When the range is Custom**, one
more control appears, last in the lens section:
- *"Order this lens from DOT Glasses (outlet doesn't have stock)"* — a checkbox. Ticking it is what
  places a Custom Order with the Sale: an order record starting at *Submitted*, which appears in
  the Admin Portal queue. Server-rejected if the range is not Custom. Not shown when converting a
  Lead whose lens is already ordered.

Then, for every sale:
- **Frame colour** — a row of swatches, each with the colour's picture (a "?" placeholder where it
  has none). The swatches come from the **adult** list, or from the **children's** list when
  "Children's frame" is ticked; changing the tick clears a colour already chosen, and the server
  refuses a colour from the wrong list ("Choose a children's frame colour."). The pictures are
  copied onto the device whenever reference data loads, so they show offline; a picture still
  hosted on another website shows online only. Selecting the "Other" swatch reveals a "say which"
  text field. Required server-side.
- **Hard case sold** — a checkbox; ticking it reveals a **Hard case colour** reference dropdown
  with Other free-text. Server-enforced both ways: colour required when sold, and both colour
  fields must be empty when not.

**Frame coverage is never asked** — not here and not on the admin conversion form (§4.4). The
column and the request field remain, and every Sale records the Full frame default; existing
records read back unchanged.

A coating is **always required** on a Sale, and a Sale has **one coating set for the pair**, not one
per eye. On a lens set every coating in it must be one both chosen lenses come in, and both
lenses' pairings must hold (choosing a coating that one lens pairs with another requires the
other — "`<Paired>` comes with `<Trigger>` on these lenses — add `<Paired>`, or remove
`<Trigger>`."); exclusions apply too. For Custom, any active coating is accepted, no pairing
applies, and exclusions still do. A pair on which no coating can be made is refused against the
right eye's lens.

A Sale is not asked about price: the customer has paid. A Sale opened via `?fixOutboxId=`
pre-fills from the originally-queued payload, same as Test/Lead.

### 5.6 The lens range selector (shared by Lead and Sale)

A single dropdown chooses the range (ADR-0005): *No preference yet* (Tests and Leads only) or, on
a Sale, *Select a lens range…*; then **every lens set assigned at or above the technician's retail
point**, alphabetically, with lens sets that have no lens powers left out; then *Custom
prescription*. Switching range clears every field belonging to the previous one.

- **A Sale has no default.** Saving without choosing shows "Choose a lens range." against the
  dropdown.
- **No lens sets reach the retail point:** only *Custom prescription* is offered, with the note
  "No lens sets are assigned to this retail point — ask your administrator."
- **A converted Lead's lens set that no longer reaches the retail point** (retired or unassigned
  since) carries over as-is and shows "This lens set isn't available at your retail point — choose
  another lens range." Nothing is substituted silently.
- **The server enforces the same rule.** A Test, Lead or Sale naming a lens set that isn't
  assigned at or above the record's own location is refused against `PresetCatalogueId` ("This
  lens set isn't available at this retail point — choose another lens range."). The location is
  the caller's for the API, and the Lead's for an Admin Portal conversion. In practice this
  catches a record queued offline while an admin unassigned the set; it lands on Failed records
  against the lens range control. The Field App's list and the server's check share one
  definition of "reaches", `ReferenceDataSnapshot.ReachesLocation`.

The Admin Portal's Lead→Sale conversion screen offers the same choice for a Lead that recorded no
lens preference — the lens sets reaching the *Lead's* retail point, then *Custom prescription* —
and asks "Choose a lens range." if it is left empty. A Lead whose lens set no longer reaches its
retail point gets the same choice, under a note naming the lens set and saying it isn't available
there any more; its other lens preferences don't carry over either, since they belonged to that set.

**Lens set** → in this order:
- *Same lens for both eyes* (ticked to start) with one **Lens** dropdown; or unticked, *Lens — left
  eye* and *Lens — right eye*. Lenses are listed by their label in the fixed display order (single
  vision by sphere, then Bifocal, Progressive and Other by add, then sphere), and the chosen lens's
  power reads underneath (`SPH +2.50 · CYL -0.75 × 90 · ADD +2.00`, only the parts it has) so a
  vague label can be checked against what is behind it. A pair has **one lens type**: once the
  left lens is chosen the right dropdown lists only lenses of its type ("Only lenses of the left
  eye's lens type are listed."), changing the left to another type empties a right lens that no
  longer fits, and re-ticking "Same lens" copies the left lens across. Choosing a lens records its
  power on each eye (and the pair's lens type) — the record never holds the lens itself, so the
  admin later editing or removing that lens changes nothing already recorded.
- *Children's frame*, then *Pupil distance (0–4)*: a coarse frame-fit bucket, **not** millimetres.
  Drops to 0–2 when the children's-frame box is ticked, and a previously chosen out-of-range value
  is cleared.
- *Coating* (a Sale) or *Coating preference* (a Test or Lead): nothing is listed until both eyes
  have a lens; then **only the coatings both lenses come in** — a coating whose paired coating one
  lens lacks is left out, since it could never be sold. A coating a chosen lens pairs with a ticked
  one is ticked with it and locked ("Comes with `<trigger>` on this lens") while that one stays
  ticked (a pairing that runs both ways never locks); a coating an exclusion forbids with a ticked
  one is disabled ("Can't be combined with `<coating>`"). If the pair offers nothing: "No coating
  can be made on both of these lenses, so they can't be sold together. Choose different lenses." A
  chosen lens with no coatings at all says "No coatings are configured for this lens yet — it can't
  be sold on a lens set until DGI adds one on Lens Sets." Changing either lens (or the same-lens
  box) keeps the coatings the new pair still offers and unticks the rest, saying "Removed `<names>`
  — not available on the lens you've now chosen."
- **A lens from a converted Lead or a Failed record** is found again by its power and lens type.
  One no longer in the set is left unchosen with "The `<power>` on this `<Lead / record>` is no
  longer in `<set>`. Choose a lens." and saving without choosing is refused against the dropdown.
- **The server enforces the same choices** on every Test, Lead and Sale naming a lens set, keyed
  on the fields the form shows: an eye with no lens ("Choose a lens for the left eye.", keyed on
  `SphereLeft`; `SphereRight` likewise), a power that is in no lens of the set ("No lens in this
  lens set has the left eye's lens power — choose a lens."), a pair with no shared lens type
  (against `SphereRight`) or a lens type other than the pair's (`LensTypeRefId`). The record is
  stored with the same fields a Custom prescription fills.

**Custom range** → per eye (left and right), each a dropdown of the shop's values in the shop's
order (`LensPowerValues`, the definition the Lens powers page shows):
- *Sphere*: −10.00 to +10.00 in 0.25 steps, 0.00 first. Required for both eyes.
- *Cylinder*: 0.00 first, then −6.00 to −0.25 in 0.25 steps; there is no positive cylinder.
- *Axis*: whole degrees 0–180. It appears **only while that eye has a cylinder** and is cleared when
  the cylinder goes back to 0.00; the server refuses an axis without a cylinder and a cylinder
  without an axis.
- *Add power*: 0.00 to +3.00 in 0.25 steps. A 0.00 add is no add.
- *Lens type*: **Bifocal / Progressive / Other** radios (Other reveals a text box), shown only when
  an add above 0.00 is chosen and cleared when it is removed. A lens with no add is single vision
  and is never asked.

Plus *Pupil distance (mm, 54–74)* — a select of whole millimetres.

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
opens the Sale form pre-filled (see 5.5). A Lead that ordered its lens shows a badge with the
order's status in the Custom Orders screen's wording — "Lens ordered · In Lab" — highlighted once
it is Ready for Pickup, so the technician knows when to call the customer. The list is read when
the screen opens (online only), so a status change shows after reopening it. Empty state: "No open leads at this outlet — everything's
been converted or nothing's been logged yet."

**Failed records — `/failed-records`.** Every permanently-rejected outbox item (see §6), each with
its real server-parsed error message, and three actions: **Fix & re-send** (reopens the originating
Test/Lead/Sale form pre-filled from the stored payload, for a form the record type supports),
**Re-send as is** (useful when the data was fine and only the session had expired), and
**Discard** (behind a confirm). Empty state: "Nothing to review — every record on this device has
been sent."

**Settings — `/settings`.** An **"Active location"** list, populated from the technician's
*eligible* locations — active, Retail-Point-level orgs they're directly assigned to, via
`IUserLocationClient.GetMyOrgsAsync` — not the full assignment list and not hard-coded. Tapping an
unselected one switches the current location (a server round trip re-issuing the JWT with the new
location; nothing is written to the user row — there's no "active org" left to write). The device
also remembers the choice in IndexedDB, so it's offered again automatically the next time this
same device signs in (per-device, so two devices sharing an account each keep their own). Disabled
while anything is queued unsent, with an explanation, for the same reason sign-out is blocked (see
§6). Below that, three "· Coming soon" toolkit items (Talking points & FAQs, Near-vision chart,
Distance-vision chart) remain static placeholders. A **Sign out** button (behind a confirm) is at
the bottom, also disabled while records are queued.

**Outlet select — `/outlet-select`.** The location picker: Home (§5.2) redirects here whenever the
signed-in device has no current location and more than one eligible retail point exists — it
auto-picks instead, with no picker shown, if it turns out there's really only one. Lists a button
per eligible location; picking one calls `switch-org` and returns to Home. Reachable directly too
(e.g. a remembered location stops being eligible mid-session), with a genuine "none eligible" state
here showing a Retry rather than duplicating the dedicated "can't record here" screen below, which
is what Home redirects to for that case.

**Can't record here — `/no-location`.** Shown by Home when the signed-in user has **no** eligible
retail point at all — the case for a DGI or Country admin with no retail-point assignment of their
own. Explains that the account isn't assigned to a retail point and to ask an admin, rather than
rendering a broken form. Carries the same queued-records guard as Settings' sign-out: any unsent
records must be sent first ("Send now (N)"), with the same explanation, before Sign out becomes
available — otherwise they'd be filed under whoever signs in next.

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

**A queued record is also rejected if its location has since stopped being valid** — the
technician's assignment to it was removed, it was deactivated, or (for a token issued before this
capability shipped) it names no location at all. `/failed-records` shows the matching message
("You're no longer assigned to `<name>` — ask your admin.", "`<name>` has been deactivated.", or
"Choose a retail point before recording.") rather than a generic failure — the same reasons the
create endpoints refuse a live submission (§7).

**A lens-set record queued before the lens power redesign is rejected, not lost.** It names a lens by
Id, which the server no longer reads, and a set the redesign retired; it lands on `/failed-records`
against the lens dropdowns. A device whose offline cache predates the new lens shape has its lens sets dropped on an
offline load, rather than showing zero-power lenses, until its next online refresh.

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
| `POST /api/v1/auth/forgot-password` | Anonymous | Anyone | Email → always 200 with the same message; a reset link is emailed to an Active or Invited account, at most once every five minutes. The link is never returned. |
| `POST /api/v1/auth/login` | Anonymous | Anyone | Username + password → JWT (60 min default), plus an optional `PreferredLocationId` (the device's remembered location). Issues a token carrying that location if it's still eligible, the caller's single eligible location if there's exactly one, or none otherwise. Failures count toward lockout; suspended accounts are refused as invalid credentials. |
| `GET /api/v1/auth/my-orgs` · `POST /api/v1/auth/switch-org` | JWT | Any authenticated user | Lists only the caller's *eligible* locations — active, Retail-Point-level orgs they're directly assigned to, never a broader assignment. Switching issues a fresh JWT carrying the chosen eligible location; nothing is written to the user row. Rejects a target that isn't eligible. |
| `GET /api/v1/tests` · `GET /api/v1/tests/{id}` | JWT | Any authenticated user | Hierarchy-scoped list / fetch — for the Field App this is the caller's current location alone, not their whole assignment set. |
| `POST /api/v1/tests` | JWT | Any authenticated user | Idempotent create. Rejected with 400 (keyed on `""`) unless the caller has a **valid current location** — an active Retail Point they're directly assigned to — with one of three messages depending on why not: no location at all, no longer assigned, or deactivated. |
| `GET/POST /api/v1/leads`, `/api/v1/leads/{id}` | JWT | Any authenticated user | As above. |
| `GET /api/v1/leads/open` | JWT | Any authenticated user | The caller's own outlet's open (unconverted) leads — backs the Field App's `/leads` worklist. |
| `GET /api/v1/leads/match?fullName=&phoneNumber=` | JWT | Any authenticated user | An open Lead matching the given name+phone, or 204 — backs the Sale form's automatic conversion prompt. |
| `GET/POST /api/v1/sales`, `/api/v1/sales/{id}` | JWT | Any authenticated user | As above. `SourceLeadId` on create atomically links and marks the source Lead converted; a second attempt against an already-converted Lead is rejected, as is one naming a Lead the caller can't see. |
| `GET /api/v1/reference-data` | JWT | Any authenticated user | All **active** reference items across all categories. Not hierarchy-scoped. |
| `GET /api/v1/preset-catalogues` | JWT | Any authenticated user | Lens sets assigned at or above the caller's current location — non-empty ones only, alphabetically, each with its lenses in the fixed display order. A lens carries its `Label`, its lens power (`Sphere`, `Cylinder`, `Axis`, `Add`), `LensTypeRefId`/`LensTypeOtherText` (null lens type is single vision), the `CoatingIds` it comes in and its own `Pairings` (`TriggerCoatingRefId` → `PairedCoatingRefId`). Empty list (not a 400) if the caller has no valid current location. |
| `GET /api/v1/reference-data/coating-rules` | JWT | Any authenticated user | The global coating exclusions (pairs of coatings that can never be selected together). Fetched and cached alongside the reference data so the rule holds offline. There are no global pairings — each lens carries its own. |
| `POST /api/v1/client-logs` | JWT | Any authenticated user | Accepts a batch of client log entries with a correlation ID; writes them to the server log. |

**Capabilities reachable through the API that no UI exposes:**
- `GET` list and by-ID for Tests, Leads and Sales — nothing in either application reads these
  endpoints; the Field App only writes, and the Admin Portal queries the database directly.

**Restrictions that exist only in the UI, not the API:** a user of any role or level can *attempt*
to create a Test, Lead or Sale through the API — there's no role/level gate on the write
endpoints, and the Admin Portal still has no general-purpose form for it (only the narrower
Lead-conversion screen, see §4.4). What the API *does* enforce, regardless of role or level, is the
current-location rule above: the attempt only succeeds against an active retail point the caller
is directly assigned to. The API applies no separate level restriction on custom orders — a Lead
or Sale posted with `OrderFromDotGlasses` from any eligible Retail Point places an order in the
fulfilment queue regardless. Sending the same record twice (the outbox retrying) places one order.

Cross-origin access is restricted to two hard-coded localhost development origins.

---

## 8. Placeholder and developer-only surfaces

These exist in the running product but are not real product capability:

| Surface | Status |
|---|---|
| Field App **Messages** | Two hard-coded announcements. No backing data or API. |
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

**CSV export** exists on Event History (one per tab, following the current search and date range),
Organisations and Custom Orders. The Dashboard has none, and there are no scheduled or emailed
reports.

**No audit trail is surfaced.** Created/modified user and timestamp are captured on every entity
but no screen displays them.

**No notifications** in either direction — the Field App's Messages screen is static and nothing
server-side can push to it.

**Search and paging are now present on most list screens** (Event History's Leads tab, User
Directory, Lens Sets) but not uniformly — Organisations' tree, Custom Orders' grouped queue
and Dashboard's top-N lists have neither, and only Event History/User Directory support true
server-side paging (Lens Sets' search filters an already-fully-loaded list, proportionate
to its small size).

**A user with no scope paths sees no rows**, not the whole database — the filter is
`patterns.Any(p => EF.Functions.Like(HierarchyPath, p))` over the caller's `ScopePaths`, which is
false for every row when the list is empty (ADR-0006). This replaced an earlier single-path
filter, where a blank `HierarchyPath` on the user would have matched every row's prefix test; that
shape no longer exists.

**Records created before this change may still be stamped above retail-point level** (DGI or
Country) and render as "Unknown outlet" / "Unknown country" throughout reporting, counted in the
Dashboard's totals. Going forward this can no longer happen through the Field App API, which
refuses any create without a valid current-location retail point, or through the Admin Portal's
Lead→Sale conversion, which refuses a deactivated retail point — see §7 and §5.7. Existing
above-retail-point rows are left as they are; migrating them is out of scope.
