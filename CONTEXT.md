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
The Coatings applied to the lens on a **Sale** — a set, not a single value, because one lens can
carry more than one at once (e.g. Blue Block + Photochromic together). Pairing and exclusion
rules govern which combinations are valid.
_Avoid_: treating a Sale's Coating as a single-select field — that was the pre-2026-08-13 model.

**Coating preference**:
The single Coating a customer expressed interest in on a **Test** or **Lead** — an intention
recorded before any lens exists, and deliberately weaker than a Coating set. Converting to a Sale
seeds the Coating set from it; a Test or Lead never carries a set of its own.
_Avoid_: calling this a Coating set, or assuming ADR-0001's set model extends to Test/Lead — it
describes the Sale only (2026-09-04).

**Coating pairing**:
A directional rule: selecting one Coating automatically adds a second Coating to the set (e.g.
selecting Blue Block auto-adds Photochromic). Not symmetric — the reverse selection does not
auto-pair back.
_Avoid_: "coating combination", "coating bundle" — pairing specifically means the one-directional
auto-add behavior, not just "these look good together."

**Coating exclusion**:
A symmetric rule: two Coatings cannot both be present in the same set at once (e.g. Clear
excludes Photochromic and Sunglasses; Photochromic and Sunglasses exclude each other).
_Avoid_: "incompatible coatings" as a stand-alone term without reference to this rule — exclusion
is the canonical name for this relationship.

**Referred or treated**:
An explicit `bool` flag (`ReferredOrTreated`), independently captured at creation time on each of
`Test`, `Lead`, and `Sale` (2026-09-03) — orthogonal to `TestOutcome`, not tied to any particular
outcome/result. When true, a `ReferralReasonRefId` (FK to `ReferenceDataItem`, Category =
ReferralReason) is required regardless of `TreatedInFacility`. `TreatedInFacility` distinguishes
"treated in-house by the facility's own staff" from "referred out elsewhere": when true, the
`ReferralLocationFreeText` field is suppressed (there's no external location to name); when
false, it's required. Because Test/Lead/Sale are separate create-once events with no update
endpoint, the same real-world referral may legitimately be (re)recorded at more than one stage of
a converting Test → Lead → Sale journey — nothing carries forward automatically between them.
_Avoid_: "Referred" as a `TestOutcome` value — that member was retired; `TestOutcome` now only
distinguishes `NoGlassesNeeded`/`NeedsGlasses`. Also avoid inferring "referred" from
`ReferralReasonRefId != null` — `ReferredOrTreated` is the explicit source of truth.

**Retailer**:
The nearest `Intermediate`-level ancestor of a retail point in the org hierarchy — the reseller
or distributor that retail point sits under. A retail point need not have one: where the nearest
node above it is a Country, it has no Retailer, and reporting says so rather than substituting
the country.
_Avoid_: "the retail point's parent node" — a retail point's immediate parent is not always
`Intermediate`-level, so the two definitions disagree wherever a retail point hangs directly off
a Country (2026-09-04).
