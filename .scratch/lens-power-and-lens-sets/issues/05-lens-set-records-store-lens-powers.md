# 05 — Lens-set records store each eye's lens power

**What to build:** A Test, Lead or Sale made from a lens set stores each eye's lens power and one lens
type, exactly as a custom prescription does, with no pointer to a lens in the set. The server accepts
it only when each eye's power and the lens type match a lens in the chosen set (which must still reach
the location), and refuses a mixed pair (eyes with different lens types). Converting a Lead carries
each eye's power and the lens type into the Sale, and the Field App and Admin Portal pre-select the
matching lens. This is the change that lets a +3.00 from a lens set and a +3.00 custom lens be
recognised as the same lens.

**Blocked by:** 02, 04

**Status:** resolved

**Model:** Opus 5.5 — breaking contract change with load-bearing failure keys, a migration, and the
lens-set branch of the consultation rules.

**Seam:** `DotGlasses.Rules.Tests` (lens-set branch, `SaleAssembly.Seed`); `DotGlasses.Web.Tests`
(create endpoints, Admin Portal lead conversion); `DotGlasses.Infrastructure.Tests` if the migration
needs a data check.

**Don't run alongside:** any other migration-bearing ticket; 06, 09, 10, 11 (consultation form, lead
conversion, `ConsultationRules`, `SaleAssembly`). 07 is safe in parallel (Lens Sets screen only).

## Acceptance criteria

- [x] `LensOptionLeftId`/`LensOptionRightId` are dropped from the create requests, DTOs, entities and
      columns (migration). `PresetCatalogueId` and `LensTypeRefId` stay; a null lens type means single
      vision.
- [x] The lens-set branch of `ConsultationRules`: each eye's power plus the lens type must match a lens
      in the chosen set (via the ticket 04 matching helper); both eyes' lenses share the lens type; the
      set must reach the location as today.
- [x] `SaleAssembly.Seed` carries each eye's power and the lens type instead of lens ids;
      `SaleAssemblyTests`' reflection check still passes.
- [x] The Field App and the Admin Portal lead conversion send the chosen lens's power and lens type
      with minimal UI change (the redesign is tickets 09 and 11). Seeding from a Lead or Test, and from
      a Failed record, uses the matching helper.
- [x] An old-shape request (with lens ids) is rejected; an old-shape outbox record lands on Failed
      records (accepted).
- [x] Rules.Tests: each eye must match by power and lens type; a mixed pair is refused;
      `SaleAssembly.Seed` carries powers and the lens type. Web.Tests: create endpoints store powers for
      both ranges and reject an old-shape request and a lens-set power with no matching lens.

## Notes

- Spec: `../spec.md` — "The lens-set branch of the consultation rules", "Contracts", "Records",
  user stories 1, 36–38, 40. CLAUDE.md on `SaleAssembly` (seed, not override) and failure keys.
- Prior art: `ConsultationRulesTests`, `SaleAssemblyTests`, `LeadConversionLensSetTests`,
  `ConsultationValidationApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

- **Contract shape.** `LensOptionLeftId`/`LensOptionRightId` are gone from `CreateTestRequest`,
  `CreateLeadRequest`, `CreateSaleRequest`, `TestDto`/`LeadDto`/`SaleDto`, the `Test`/`Lead`/`Sale`
  entities and `SaleAnswers`. A lens-set record is now `LensRangeType = LensSet` +
  `PresetCatalogueId` + the same per-eye `Sphere/Cylinder/Axis/Add{Left,Right}` and one
  `LensTypeRefId`/`LensTypeOtherText` a Custom prescription fills (null lens type = single
  vision). No lens id travels anywhere.
- **Migration:** `20260928170015_DropRecordLensOptionIds` — a plain `DropColumn` of both columns
  on `Tests`, `Leads` and `Sales`, no backfill (B03 already emptied the old lens sets, so an old
  lens-set record's power isn't recoverable; it reads back with empty powers and its set's name —
  `LensSetResetMigrationTests` pins that). Not applied to any real database.
- **Rules.** `ConsultationRules`' lens-set branch (after the retired / reaches-location checks,
  unchanged) asks `LensSetLenses.Match` per eye: an eye with no sphere → "Choose a lens for the
  left/right eye." keyed `SphereLeft`/`SphereRight`; a power in the set under no lens type → "No
  lens in this lens set has the left/right eye's lens power — choose a lens." on that eye's
  sphere; both eyes real but no shared lens type (mixed pair) → refused on `SphereRight`; a shared
  lens type other than the one sent → `LensTypeRefId`. The coating rules scope by the lens
  matching the left eye (like-for-like with the old left lens id; 06 widens to both). New
  `LensSetLenses.RecordedAs(left, right)` → `LensPairPowers` is the one place a chosen pair
  becomes what a record stores (left lens decides the lens type; a mixed pair is recorded as
  chosen so the rules refuse it rather than it being silently "fixed").
- **How an old-shape request is rejected.** Nothing special-cased: the JSON binder ignores the
  unknown `lensOptionLeftId`/`lensOptionRightId`, the request reaches the rules with no spheres,
  and it's refused on `SphereLeft`/`SphereRight` — a field-keyed 400, so an old-shape outbox record
  lands on Failed records against the lens dropdowns (which render those keys).
- **Carry-over / seeding.** `SaleAssembly.Seed` carries powers + lens type (reflection test green).
  Admin Portal lead conversion: form-only `LensLeftId`/`LensRightId` (the dropdowns) →
  `LeadConversionFormModel.ApplyLensRange(snapshot)` → `RecordedAs`; a Lead's lens is found with
  `Match`, and a Lead whose set still reaches it but no longer holds its lens gets a "no longer in
  the lens set" note with the set and whichever eye still matches pre-selected. Field App:
  `LensRangeSelection.ChooseLenses` (UI-only `LensLeftId`/`LensRightId`, via `RecordedAs`);
  `ConsultationForm.ApplyLensRange` seeds from a Lead and from a Failed record via `Match`.
- **Deviations / extras.** (1) `ConsultationForm.ApplyLensRange` now also carries
  `LensTypeRefId`/`LensTypeOtherText` into the controls — it silently dropped them before (a
  converted Custom Lead lost its lens type). (2) Reloading a Failed **Test** record now restores
  its lens block too (only Leads/Sales did). (3) `ReferenceDataSnapshot.ResolveLensOptionLabel`
  and `LensOptionBelongsToCatalogue` removed — no record holds a lens id to resolve any more.
- **Stale docs for B12** (not edited here): CLAUDE.md's `Rules` bullet should mention the
  lens-set branch matching by power + lens type (`LensSetLenses.Match`) and `RecordedAs` as the
  one pair → record mapping; `docs/functional-capabilities.md`'s lens-set record/API shape still
  describes lens ids on Test/Lead/Sale.
- **Tests.** Rules: `ConsultationRulesTests` (per-eye match, mixed pair, wrong lens type,
  old shape), `LensSetLensesTests` (`RecordedAs`), `SaleAssemblyTests`. Web: new
  `LensSetRecordApiTests` (Test/Lead/Sale on a lens set store powers + lens type, Custom Sale
  stores powers, old-shape JSON → 400 on `SphereLeft`/`SphereRight`, a power matching no lens →
  400 on that eye, mixed pair → `SphereRight`); `LeadConversionLensSetTests` posts
  `Form.LensLeftId`/`Form.LensRightId`, asserts the Sale's powers, and adds the "lens no longer in
  the set" case. Full `dotnet test DotGlasses.sln` — 613 passed, 0 failed (Application 96,
  Rules 328, Infrastructure 62, Web 127).
