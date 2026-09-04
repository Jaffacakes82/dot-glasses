# DOT Glasses

Admin Portal + Field App for DOT Glasses' vision-care distribution operation: recording
consultations (Test/Lead/Sale) in the field, and administering the org hierarchy, reference
data, and lens catalogues centrally.

## Language

**Coating**:
A `ReferenceDataItem` (Category = Coating) describing a lens treatment (e.g. Photochromic, Blue
Block, Clear, Sunglasses). A Sale or Lead's lens carries a **set** of Coatings, not a single one
— a lens can have more than one applied at once (e.g. Blue Block + Photochromic together).
_Avoid_: treating Coating as a single-select field — that was the pre-2026-08-13 model, no
longer accurate once coating pairing/exclusion rules land.

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
