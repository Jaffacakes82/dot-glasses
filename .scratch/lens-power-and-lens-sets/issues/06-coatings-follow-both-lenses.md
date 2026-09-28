# 06 — Coatings follow both lenses

**What to build:** The server enforces coatings per lens, not per label or per left eye. On a lens set,
a Sale's coatings must be ones both chosen lenses come in, and every coating paired with a trigger
coating (from either lens's pairings) must be present too. Global exclusions still apply. On a custom
prescription any active coating is allowed, subject to exclusions, with no pairings. A Test's or Lead's
coating preference, when both lenses come from a lens set, must be one both lenses come in.

**Blocked by:** 05

**Status:** resolved

**Model:** Opus 5.5 — the coating rules in the shared consultation rules.

**Seam:** `DotGlasses.Rules.Tests` (main); `DotGlasses.Web.Tests` for the create-endpoint rejections.

**Don't run alongside:** 05, 11 (`ConsultationRules`). 09 is safe in parallel (it doesn't touch the
server rules or the coating selector).

## Acceptance criteria

- [x] Lens set: coatings outside the two lenses' intersection are refused; a trigger without its paired
      coating is refused, whichever lens the pairing is on; exclusions enforced.
- [x] Custom prescription: any active coating, subject to exclusions; no pairing enforcement.
- [x] At least one coating on a Sale, as today.
- [x] Coating preference on a Test or Lead is limited to the intersection on a lens set.
- [x] Uses the ticket 04 offered-coatings helper — no second implementation.
- [x] Rules.Tests cover each case above. Web.Tests: a create is rejected for a coating outside the
      intersection and for a missing paired coating.
- [x] Server-only: the Field App's coating selector is updated in ticket 10 and Admin Portal lead
      conversion in ticket 11.

## Notes

- Spec: `../spec.md` — "The coating rules", user stories 27, 35, 40. Decision: ADR-0007 "Coatings".
- Prior art: `ConsultationRulesTests`, `ConsultationValidationApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

- **Decision: `CoatingsFor.Offered` leaves out a coating that could never be sold on the pair**
  (answers ticket 04's open question). The left lens pairs Blue block → Photochromic, the right
  comes in Blue block but not Photochromic: Blue block is in both lenses, but any set holding it
  would fail the pairing, so it isn't offered. Any coating whose paired coating (from either lens's
  pairings) isn't offered is dropped, repeated until stable (a chain Clear → Anti-glare →
  Photochromic drops both when Photochromic is missing). `RequiredPairings` stays literal (every
  pairing from either lens, each once). Because the Field App will offer from the same helper
  (ticket 10), the device can't show a coating the server refuses, and the server's pairing
  message ("add Photochromic") always names a coating that is on offer. Pinned in
  `LensSetLensesTests` (trigger dropped whichever lens carries the pairing; kept when its pair is
  offered; chained pairings).
- **Rule shapes** (`ConsultationRules`). `LeftEyesLensSetLens` is replaced by `LensSetPair` →
  private `ChosenPair(Applies, Left, Right)`: each eye matched with `LensSetLenses.Match` (power +
  the request's lens type), and `ChosenPair.Coatings` = `LensSetLenses.CoatingsFor(left, right)`,
  or null when either eye matched no lens. `Applies` is false exactly where `PresetBranch`
  short-circuits (as before).
  - **Sale, lens set:** a matched lens with no coatings → reported against its own eye's sphere
    (either eye now, was left only); else a pair offering nothing → `SphereRight`; else the set is
    checked: at least one → no duplicates → active → in `Offered` → every `RequiredPairings`
    trigger present has its paired coating → exclusions. One failure at a time, as before.
  - **Sale, Custom:** any active coating, exclusions, at least one; no pairing.
  - **Sale, lens set with an eye matching no lens (or wrong lens type / mixed pair):** coatings
    checked for everything that doesn't depend on the lenses (was: narrowed by the left lens when
    the left matched). The eye/lens-type failure is already reported.
  - **Test/Lead preference:** on a lens set where both eyes matched, must be in `Offered`. No
    pairing asked of a single value (the Sale's set carries the paired coating). Unchanged
    elsewhere, including the Test/Lead ordering drift.
- **Failure keys / messages.**
  - `CoatingRefIds` — kept verbatim: "Choose at least one coating.", duplicates, active-item,
    "Every coating must be configured as available for the chosen lens option (see Lens Sets)."
    (now means "offered on the pair"), the exclusion message.
  - `CoatingRefIds` — **new**: "{Paired} comes with {Trigger} on these lenses — add {Paired}, or
    remove {Trigger}." (labels via `ReferenceDataSnapshot.ResolveLabel`, e.g. "Photochromic comes
    with Blue block on these lenses — add Photochromic, or remove Blue block.").
  - `SphereLeft`/`SphereRight` — kept: "This lens has no coatings configured yet, so it can't be
    sold on a lens set." (now for either eye).
  - `SphereRight` — **new**: "No coating can be made on both of these lenses, so they can't be sold
    together on a lens set — choose another lens for the right eye." (same reasoning as the
    mixed-pair refusal: the Field App narrows the right eye to the left's).
  - `CoatingPreferenceRefId` — kept: "CoatingPreferenceRefId is not configured as available for the
    chosen lens option (see Lens Sets)." (now "offered on the pair").
- **Deviations / notes.** (1) Existing availability wording kept per the brief even though "the
  chosen lens option" is now two lenses — B12 may want "the chosen lenses". (2) A retired set / a
  set not reaching the location still has its lenses matched for the coating check (pre-existing,
  unchanged). (3) `CreateSaleRequest`'s doc comment updated (comment only). No App, Web source,
  B07 file or migration touched.
- **For ticket 10:** use `LensSetLenses.CoatingsFor(left, right)` — `Offered` is already safe to
  show as-is (no trigger without its pair); lock each `RequiredPairings` paired coating whose
  trigger is ticked.
- **Stale docs for B12** (not edited): `docs/functional-capabilities.md` ~754–758 (Sale coating
  "available for the chosen *left eye* lens's strength … resolved against the left eye's
  configuration") and ~796–799 (Field App coating list follows the left lens; B10 will change the
  behaviour too). `CONTEXT.md` already describes the both-lenses rule. CLAUDE.md's `Rules` bullet
  could mention that the coating rules come from `LensSetLenses.CoatingsFor`.
- **Tests.** Rules: `LensSetLensesTests` +3; `ConsultationRulesTests` — `CoatingSet_AvailabilityIsScopedByTheLeftLensOnly`
  replaced by both-lenses cases (either eye narrowing; trigger without pair, either lens; trigger
  with pair; pair alone; trigger whose pair one lens lacks is not offered; no coating sellable on
  both → `SphereRight`; right lens with no coatings; Custom has no pairings; unmatched eye either
  side) and preference cases (either eye narrowing, Test and Lead; unsellable trigger refused;
  sellable trigger accepted); two lenses added to the literal snapshot (`LensA6Paired`,
  `LensA7NoPhotochromic`). Web: new `LensSetCoatingApiTests` (4) — Sale refused for a coating only
  one lens comes in, refused for a trigger without its paired coating when the pairing is on the
  other lens, stored with both, Lead preference outside the pair refused. Full
  `dotnet test DotGlasses.sln` — 634 passed, 0 failed (Application 96, Rules 345, Infrastructure
  62, Web 131); baseline 613.
