# 03 — Reset lens sets so each lens is a lens power with its own coatings and pairings

**What to build:** A lens in a lens set stops being a pointer to a "Lens strength" label and becomes a
lens power with a typed label, a lens type when it has an add, the coatings it comes in and its own
coating pairings. Existing lens-set data is reset rather than converted: every existing lens set is
retired and emptied, and Lens strengths, the global coating availability grid and the global Pairings
section disappear (exclusions stay). Old records keep their lens set's name. Local dev and tests get
example 6-Lens and 9-Lens sets in the new shape. The Lens Sets screen and the lens-set API show each
lens's label, power, lens type, coatings and pairings. Coating availability now comes from the lens's
own coatings, with today's meaning (ticket 06 changes the meaning).

**Blocked by:** 02 (shared Rules files), multi-org-access-and-retail-points 04 (the Lens Sets
controller)

**Status:** resolved

**Model:** Opus 5.5 — the reset migration has a seed-data trap, and it removes three concepts across
every layer.

**Seam:** `DotGlasses.Infrastructure.Tests` for the migration; `DotGlasses.Web.Tests` for the Lens
Sets screen, Reference Data screen and lens-set API; `DotGlasses.Rules.Tests` for coating availability.

**Don't run alongside:** any other migration-bearing ticket; 07 and 08 (Lens Sets controller and view).

## Acceptance criteria

- [x] A lens-set lens holds label, sphere, cylinder, axis, add and lens type reference, plus two new
      child tables: its coatings, and its pairings (lens, trigger coating, paired coating). It drops
      the Lens strength reference and sort order.
- [x] `LensStrengthCoatingOption`, global `CoatingPairing` and the `LensStrength` reference category
      are removed. The enum's numeric value is retired, not reused. Coating exclusions are unchanged.
- [x] One migration resets the data in the same change as the schema: every existing lens set is
      retired (`IsDeleted`, `DeletedAtUtc`); their lenses and assignments are deleted; Lens strength
      items, the grid and global pairings are deleted; exclusions kept. **Edit the generated
      migration** so the two seeded lens sets are retired, not deleted (removing their `HasData`
      makes EF emit `DeleteData`).
- [x] Old lens-set records keep `LensRangeType` and `PresetCatalogueId` and show their set's name.
      Custom records keep their values, even outside the new ranges.
- [x] The dev-only seeder and the test fixtures create example 6-Lens and 9-Lens sets in the new
      shape: real powers, labels, coatings, one example pairing, the same assignments as today.
      Staging and production get no active lens sets.
- [x] The lens-set DTO carries per lens: id, label, sphere, cylinder, axis, add, lens type id, coating
      ids, and pairings as trigger/paired id pairs. Global pairings leave the reference-data payload.
- [x] Admin Portal: the coating availability grid and its save action are gone; the old add-a-lens-
      strength flow is removed (ticket 07 adds the new dialog); each set's lenses show in a read-only
      table (label, lens power, lens type, coatings, pairings). Reference Data no longer shows Lens
      strengths or the Pairings section; exclusions stay.
- [x] Coating availability is read from the lens's own coatings (like-for-like with today's
      left-lens check); global pairing enforcement is removed (ticket 06 adds per-lens pairings).
- [x] Infrastructure.Tests: existing sets end retired and empty; Lens strength items, grid and global
      pairings gone; exclusions intact; an old lens-set record still resolves its set's name; an old
      custom record keeps out-of-range values. Web.Tests: the lens-set API returns the new shape;
      Reference Data shows neither Lens strengths nor Pairings.

## Notes

- Spec: `../spec.md` — "Domain and schema", "Admin Portal" (grid removal, Reference Data), user
  stories 19–20, 41–42. Decision: ADR-0007 including "Coatings". Map ticket 09 for why reset.
- Prior art: `LensRangeMigrationTests`, `PresetCatalogueSnapshotTests`, `LensSetAvailabilityApiTests`,
  `DevUserSeederTests`.
- EF gotchas (handoff) apply. Migrations are applied only by CI.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

- **Schema** (Domain/Infrastructure):
  - `LensOption` (table `LensOptions`): `Id`, `PresetCatalogueId`, `Label` (≤100), `Sphere`
    (`decimal`, required), `Cylinder`/`Axis`/`Add` (`decimal?`), all `numeric(5,2)` like the
    records' per-eye columns, `LensTypeRefId` (`Guid?`, null = single vision) and
    `LensTypeOtherText` (≤200). `LensStrengthRefId` and `SortOrder` are gone.
  - New child tables, both FK-cascading from `LensOptions` (removing a lens takes them with it):
    `LensOptionCoating` (`LensOptionCoatings`: `LensOptionId`, `CoatingRefId`, unique pair) and
    `LensOptionCoatingPairing` (`LensOptionCoatingPairings`: `LensOptionId`, `TriggerCoatingRefId`,
    `PairedCoatingRefId`, unique triple). DbSets `LensOptionCoatings`, `LensOptionCoatingPairings`.
  - Removed: `LensStrengthCoatingOption`, `CoatingPairing`, their configurations and DbSets, and the
    lens-set seed configurations. `ReferenceDataCategory.LensStrength` removed from Domain and
    Contracts, with `// 6 — retired … do not reuse` in both enums; the sixteen seeded item ids
    (`…043`–`…058`) are noted as spent in `ReferenceDataSeedConfiguration`, which now exposes
    `CoatingBlueBlockId` and `LensTypeBifocalId` too.
- **Migration:** `20260928161950_ResetLensSetsToLensPowers` (follows
  `RenameLensPowerFieldsRangeNeutral`). Hand-edited: the scaffolded `DeleteData` for the two seeded
  lens sets (and for the seeded lenses/assignments/Lens strength items) is replaced by one SQL
  block that runs first — retire every lens set (`IsDeleted`, `DeletedAtUtc = now()`,
  `DeletedBy = 'Lens set reset (ADR-0007)'`), delete every lens and every assignment, delete every
  category-6 item (admin-added ones too) — then the scaffolded schema changes (drop the grid and
  global pairing tables, reshape `LensOptions`, create the two child tables). Exclusions untouched.
  `Down` is best effort (restores schema and seeds; un-retires the two seeded sets in place rather
  than re-inserting them; empties new-shape lenses). The Infrastructure migration tests migrate the
  template *down* through it, so `Down` is exercised on every run.
- **Rules API** (`ReferenceDataSnapshot`): constructor is now `(items, presetCatalogues,
  coatingExclusions)`; `FromCachedReferenceData(items, catalogues, exclusions)`; `CoatingPairings`
  and `PairedCoatingsFor` are gone. `LensOptionSnapshot(Id, Label, Sphere, CoatingIds, Cylinder?,
  Axis?, Add?, LensTypeRefId?, LensTypeOtherText?, Pairings?)` — `Pairings` is
  `IReadOnlyList<CoatingPairingRule>` (never null). `IsCoatingAvailableForLensOption` reads the
  lens's own `CoatingIds`. The server fills lenses in an *interim* order (add, then sphere, then
  label) and each lens's coatings/pairings in the Coating list's sort order — ticket 04's order
  helper should replace the lens order.
- **Contracts:** `LensOptionDto { Id, Label, Sphere, Cylinder?, Axis?, Add?, LensTypeRefId?,
  LensTypeOtherText?, CoatingIds, Pairings: LensCoatingPairingDto[] { TriggerCoatingRefId,
  PairedCoatingRefId } }` (`SortOrder`/`AvailableCoatingIds` gone). `CoatingRulesDto` carries
  `Exclusions` only; `CoatingPairingDto` deleted.
- **Admin Portal:** Lens Sets lists each set's lenses in a table (label, lens power as
  `SPH +2.50 · CYL -0.75 × 90 · ADD +2.00` via `LensPowerValues.FormatPower`, lens type or "Single
  vision", coatings, pairings as `Blue block → Photochromic`). `IPresetCatalogueAdminService`'s
  `PresetCatalogueLensOptionAdminDto` carries power, lens type id/label and coatings/pairings with
  ids + labels (for ticket 07's dialog), built off the snapshot. Grid, `SaveCoatingAvailability`,
  `AddLensOption`, both their validators and the grid/strength service methods are deleted.
  Reference Data loses the Lens strengths card and the Pairings section (and its two actions).
- **Example sets:** `Infrastructure/Persistence/ExampleLensSets.EnsureSeededAsync` — "6-Lens Set"
  (`c1000000-…-001`, 8 lenses) and "9-Lens Set" (`…-002`, 12 lenses) with the original rosters as
  real powers, labels `+2.50` / `Bifocal +2.50`, single vision in Clear/Blue block/Photochromic,
  bifocals Photochromic-only (Bifocal lens type), the one pairing on the 6-Lens +2.50
  (`ExampleLensSets.SixLensPlus250Id`, Blue block → Photochromic), DGI-owned, assigned to Kenya.
  Idempotent per set. Called from Program.cs's `IsDevelopment()` migrate block and from both test
  fixtures (`PostgresContainerFixture` template, `CustomWebApplicationFactory`). Never in a migration.
- **Deviations / judgement calls:**
  - Dev seeding lives in Program.cs's development-only startup block, not `DevUserSeeder` —
    `DevUserSeeder` is gated on DevSeed secrets (possibly set outside dev), and keeping it
    untouched avoids the concurrent multi-org edits.
  - Added `LensTypeOtherText` to the lens (the dialog's "Other with text" needs somewhere to live,
    and a record's lens type Other needs the text) — saves ticket 07 a migration.
  - The table keeps the existing **Remove** action per lens (the old × on the badge); Edit/Add come
    with ticket 07.
  - `ReferenceDataAdminService.AddCoatingExclusionAsync` now refuses an exclusion contradicting any
    lens set lens's pairing (was: a global pairing); message "Can't add this exclusion — a lens in a
    lens set pairs these two coatings."
  - `ReferenceDataController.Create` also checks `ModelState`: MVC binds an undefined category
    number (e.g. a stale page posting 6) to the default, Occupation, and the validator accepted it —
    a Lens strength item would have landed in Occupations (verified red first).
  - Client-visible copy: the two coating-availability messages now end "(see Lens Sets)" instead of
    "(see Reference Data > Lens Strength)"; the Field App's no-coatings note says "until DGI adds one
    on Lens Sets".
  - Field App: `CoatingMultiSelector` loses its `Pairings` parameter and the auto-add (ticket 10 adds
    per-lens locked pairings); lens dropdowns use the server's order instead of `SortOrder`.
- **Outbox / cache finding:** the IndexedDB reference-data cache is read, never rejected. A payload
  cached before this change carries `coatingPairings` and lenses with `sortOrder`/
  `availableCoatingIds`: System.Text.Json ignores the unknown names, each lens loads with its label,
  sphere 0 and no coatings, and the null-guards in `FromCachedReferenceData` cover an explicit
  `null` collection — pinned by `PresetCatalogueSnapshotTests.ALensSetCachedBeforeTheLensPowerShape_StillLoads`.
  The cached sets are all retired anyway; the next online load replaces the payload. Enums are
  cached as numbers, so a cached category-6 item still deserializes (and is never asked for). A
  queued lens-set record names a now-retired set, so it is Rejected at sync ("This lens set has been
  retired — choose another lens range.", keyed on `PresetCatalogueId`) and lands on Failed records —
  accepted per spec. Not covered: a device still running the *previous app build* reads the new
  lens DTO with no `availableCoatingIds`, so every lens shows "No coatings are configured" until
  the PWA updates — self-heals, and there are no active real sets in staging/prod anyway.
- **Tests deleted/rewritten:** `PresetCatalogueSnapshotTests.PairedCoatingsFor_IsDirectional`
  deleted (replaced by per-lens pairing/power theories and the old-cache test);
  `DomainRuleViolationScreenTests.ReferenceData_PairingACoatingWithItself…` rewritten to the
  exclusion's self-exclusion refusal; `AccessControlPolicyTests` now checks `RemoveLensOption`
  instead of `AddLensOption`; `LensSetAvailabilityTests`, `LensSetAvailabilityApiTests`,
  `FieldAppCurrentLocationApiTests`, `LeadConversionLensSetTests` build lenses in the new shape
  (Web.Tests helper `LensSetTestData.AddSellableLens`) and use `ExampleLensSets` ids. New:
  `LensSetResetMigrationTests` (4), `ExampleLensSetsTests` (3), `LensSetLensTableTests` (5),
  `ReferenceDataScreenTests` (4), `LensSetApiShapeTests` (3). `PreMigrationRows.InsertAsync` is
  public now for tables the model no longer maps.
- **Stale docs for B12:**
  - `CLAUDE.md` ~124–133: "ten remaining validators" is now eight, and the
    `AddLensOptionRequestValidator`/`SetCoatingAvailabilityRequestValidator` sentence is stale
    (`IReferenceDataLookupService` has no caller until ticket 07).
  - `CLAUDE.md` ~142: `LensStrengthCoatingOption` in the not-hierarchy-scoped list → `LensOptionCoating`/
    `LensOptionCoatingPairing`.
  - `CLAUDE.md` ~190–203: the `PresetCatalogue`/`LensOption` bullet (roster of `LensStrength` items,
    `LensStrengthCoatingOption`) and the `ReferenceDataItem` category list ("Lens strengths").
  - `CLAUDE.md` Rules bullet: snapshot now carries per-lens coatings/pairings, no global pairings.
  - `docs/functional-capabilities.md` ~47 (`LensStrengthCoatingOption`), ~442 and ~452 (Add lens
    strength dropdown), ~474–480 (coating availability grid), ~559 (Coatings & tints feeds the grid;
    Pairings section), ~563 (Lens strengths row), ~589–596 (16 Lens strengths / only bifocals
    configured), ~739/755–757/796–799 (coatings "configured for the lens strength"), ~914
    (`GET /api/v1/preset-catalogues` shape), plus: staging/prod have no active lens sets; dev and
    tests get the example sets; Lens Sets shows a read-only lens table.
  - `docs/open-issues.md` ~142–155: the two Lens strength items (12 of 16 unconfigured; label-only
    list) are resolved by removal.
  - ADR-0002 line 27 mentions "coating pairing/exclusion rules" in the snapshot — exclusions only now.
- **Tests:** full `dotnet test DotGlasses.sln` — 581 passed, 0 failed (Rules 308, Application 96,
  Infrastructure 62, Web 115); baseline 559.
