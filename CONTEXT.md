# DOT Glasses

Admin Portal + Field App for DOT Glasses' vision-care distribution operation: recording
consultations (Test/Lead/Sale) in the field, and administering the org hierarchy, reference
data, and lens catalogues centrally.

## Language

**Coating**:
A `ReferenceDataItem` (Category = Coating) describing a lens treatment (e.g. Photochromic, Blue
Block, Clear, Sunglasses). How many Coatings a record carries depends on the record — see
**Coating set** and **Coating preference**, which are different concepts and not interchangeable.

**Coating set**:
The Coatings applied to the lens on a **Sale**, or on a **Lead** that places a **Custom order** —
a set, not a single value, because one lens can
carry more than one at once (e.g. Blue Block + Photochromic together). There is one set for the
pair of glasses, not one per eye. On a **Lens set**, only Coatings that both chosen lenses come in
are offered, and both lenses' pairings apply. On a **Custom prescription**, any active Coating is
offered. Exclusions apply to both (2026-09-28).
_Avoid_: treating a Sale's Coating as a single-select field — that was the pre-2026-08-13 model.

**Coating preference**:
The single Coating a customer expressed interest in on a **Test** or **Lead** — an intention
recorded before any lens exists, and deliberately weaker than a Coating set. Converting to a Sale
seeds the Coating set from it. A Test never carries a set of its own, and a Lead carries one only
when it places a **Custom order**, because the lab makes what was ordered (2026-10-01).
_Avoid_: calling this a Coating set, or assuming ADR-0001's set model extends to every Test and
Lead — it describes the Sale, and a Lead that orders (2026-09-04, revised 2026-10-01).

**Coating pairing**:
A directional rule on one lens in a **Lens set**: selecting one Coating automatically adds a
second (e.g. selecting Blue Block auto-adds Photochromic). It records what DGI manufactures for that
lens, so the partner can't be removed. Not symmetric — the reverse selection does not auto-pair
back. Pairings belong to lens-set entries only. There are no global pairings, and a **Custom
prescription** has none (2026-09-28).
_Avoid_: "coating combination", "coating bundle" — pairing specifically means the one-directional
auto-add behavior, not just "these look good together."

**Coating exclusion**:
A symmetric rule: two Coatings cannot both be present in the same set at once (e.g. Clear
excludes Photochromic and Sunglasses; Photochromic and Sunglasses exclude each other). Exclusions
are global. They apply to lens sets and custom prescriptions alike, and no lens-set pairing may
contradict one.
_Avoid_: "incompatible coatings" as a stand-alone term without reference to this rule — exclusion
is the canonical name for this relationship.

**Current location**:
The one **Retail point** the Field App stamps every Test, Lead and Sale with. It must be an active
retail point the user is directly assigned to; being assigned to an org above it isn't enough. It
is remembered per device. Only the Field App has one; the Admin Portal works across the user's
whole **Scope** instead (2026-09-28).
_Avoid_: "active org" or "primary org" for the Admin Portal, which has no single org.

**Custom order**:
A request for DOT Glasses to make a Custom-range lens for a **Retail point**. It is placed when a
Lead or a Sale is recorded, never afterwards, and a Sale converted from a Lead that already ordered
shares that order. It moves through Submitted, In Lab, Ready for Pickup and Fulfilled. An order
placed from a Lead is unpaid until that Lead converts (2026-10-01).
_Avoid_: treating a custom order as a kind of Sale — it is its own thing, and may exist with no
Sale at all.

**Lens power**:
The prescription of one eye's lens: sphere, cylinder, axis and add. A lens power is a value, not
a stored item. Two lenses with the same four numbers are the same lens power, whether they came
from a **Lens set** or a **Custom prescription**. The allowed values are fixed, copied from the
DOT Glasses online shop, and change only with a release. Sphere is required. A blank cylinder
means 0.00. Axis is required only when cylinder isn't 0. A blank add means no add, and so does
an add of 0.00 (2026-09-28).
_Avoid_: "lens strength", which is retired along with its reference data list (2026-09-28).

**Lens range**:
What a technician picks for the lens on a Test, Lead or Sale: either one **Lens set**, or a
**Custom prescription**, which is any allowed **Lens power** for each eye, made to order. Both
produce the same kind of lens. A record's lens range names the specific lens set. "6-Lens" and
"9-Lens" are not kinds of lens range.
_Avoid_: "6-Lens range" / "9-Lens range" as types (2026-09-26); "preset" as a noun on its own.

**Lens set**:
A named, admin-configured list of ready-made **Lens powers**, built from the allowed values,
owned by an org and assigned to orgs. Each entry has a typed label, unique within the set, which is
what technicians pick by. It is available to every retail point at or beneath an org
it is assigned to, and there may be any number of them.
_Avoid_: "package", "preset catalogue" (outside the code), "picker role"/"kind" — a lens set has
no fixed role; the Field App offers whichever lens sets reach the retail point (2026-09-26).

**Lens type**:
Whether a pair of glasses is single vision, bifocal, progressive or something else. There is one
lens type per pair, never one per eye. A pair is **single vision** when neither eye has an add;
single vision is inferred from that, never asked, and isn't an admin-editable option. When either
eye has an add, the lens type is asked for: Bifocal, Progressive or Other (with free text), taken
from reference data (2026-09-28).

**Org assignment**:
A user's link to an org, which gives the user their **Role** over that org and everything beneath
it. A user has one or more. The last one can't be removed; to take away all of someone's access,
suspend them instead (2026-09-28).
_Avoid_: "primary org", which is no longer a concept. No assignment is special.

**Referred or treated**:
An explicit `bool` flag (`ReferredOrTreated`), independently captured at creation time on each of
`Test`, `Lead`, and `Sale` (2026-09-03) — orthogonal to `TestOutcome`, not tied to any particular
outcome/result. When true, a `ReferralReasonRefId` (FK to `ReferenceDataItem`, Category =
ReferralReason) is required regardless of `TreatedInFacility`. `TreatedInFacility` distinguishes
"treated in-house by the facility's own staff" from "referred out elsewhere": when true, the
`ReferralLocationFreeText` field is suppressed (there's no external location to name); when
false, it may be given but isn't required (optional since 2026-10-01). Because Test/Lead/Sale are
separate create-once events with no update endpoint, the same real-world referral may legitimately
be (re)recorded at more than one stage of a converting Test → Lead → Sale journey. Each record
keeps its own answer; a Test continued into a Lead only offers its answers as the Lead form's
starting values (2026-10-01).
_Avoid_: "Referred" as a `TestOutcome` value — that member was retired; `TestOutcome` now only
distinguishes `NoGlassesNeeded`/`NeedsGlasses`. Also avoid inferring "referred" from
`ReferralReasonRefId != null` — `ReferredOrTreated` is the explicit source of truth.

**Retail point**:
The lowest org level, and the only one where Tests, Leads and Sales are recorded. An org at any
other level that wants to distribute, for example DGI at an outreach event, creates a retail point
for it. That retail point is an ordinary one with a descriptive name, with no special marker.
Nothing new is recorded at a deactivated retail point (2026-09-28).
_Avoid_: "outlet" in new copy, except where a screen already uses it.

**Retailer**:
The nearest `Intermediate`-level ancestor of a retail point in the org hierarchy — the reseller
or distributor that retail point sits under. A retail point need not have one: where the nearest
node above it is a Country, it has no Retailer, and reporting says so rather than substituting
the country.
_Avoid_: "the retail point's parent node" — a retail point's immediate parent is not always
`Intermediate`-level, so the two definitions disagree wherever a retail point hangs directly off
a Country (2026-09-04).

**Role**:
Admin or User. A user has exactly one, and it applies across their whole **Scope**, not per
**Org assignment** (2026-09-28; this may become per-assignment later).

**Scope**:
Everything at or beneath any of a user's **Org assignments**, combined. It is what the Admin Portal
shows the user and lets them act on. Assigning someone to DGI as well as to a retail point gives
them DGI's scope, not the retail point's (2026-09-28).
_Avoid_: reading a user's scope from a single org.
