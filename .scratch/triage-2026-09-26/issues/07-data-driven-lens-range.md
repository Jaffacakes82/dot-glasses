# 07 — Lens range is "a lens set" or "Custom prescription", driven by assigned lens sets

Status: done (implemented 2026-09-26, branch feat/lens-sets)
Blocked by: None — can start immediately
Category: enhancement

Source: grilling of ticket 04, 2026-09-26, Joe. Decision recorded in
`docs/adr/0005-lens-sets-are-data-driven.md`; vocabulary in `CONTEXT.md` (**Lens set**, **Lens range**).

## Agent Brief

**Category:** enhancement

**Summary:** Replace the fixed three-way lens range (6-Lens Set / 9-Lens Set / Custom
prescription) with a data-driven one. The Field App offers every lens set that reaches the retail
point, and a record's lens range is either one lens set (by id) or a Custom prescription. Remove
`PresetCatalogueKind` entirely.

**Current behavior:**
- `LensRangeType` is `SixLensSet = 0, NineLensSet = 1, Custom = 2`, defined in both
  `Domain/Enums/LensRangeType.cs` and `Contracts/Common/LensRangeType.cs`, and mapped by
  `Application/Common/LensRangeTypeMapping.cs`.
- `PresetCatalogue.Kind` (`Domain/Enums/PresetCatalogueKind.cs`) picks which catalogue backs each
  button. At most one catalogue system-wide may hold each kind. This is enforced by
  `PresetCatalogueAdminService.HasCatalogueWithKindAsync` and the Create/Update catalogue
  validators.
- `App/Pages/LensRangeSelector.razor` hard-codes three options and resolves the 6/9 catalogue with
  `FirstOrDefault(c => c.Kind == …)`. When there is no lens preference, `OnInitialized` defaults a
  Sale to `SixLensSet`.
- `Rules/ConsultationRules.cs`, `Rules/Sales/SaleAssembly.cs`,
  `App/Pages/ConsultationForm.razor`, `App/ReferenceData/LensRangeSelection.cs` and the Admin
  Portal's `LeadConversionController` + `Views/LeadConversion/Convert.cshtml` (a plain
  `GetEnumSelectList<LensRangeType>()`) all branch on `SixLensSet or NineLensSet`.
- `Kind` flows through `PresetCatalogueDto`, `PresetCatalogueSnapshot` and the admin DTOs.

**Desired behavior:**
- `LensRangeType` has two members: a lens set, and `Custom`. Pick the member name to match
  `CONTEXT.md` (e.g. `LensSet`). Every `SixLensSet or NineLensSet` branch becomes the single
  lens-set branch. The specific set remains `PresetCatalogueId`, as today.
- `PresetCatalogueKind` and `PresetCatalogue.Kind` are gone: from Domain, Contracts, the
  snapshot, DTOs, validators, the admin service, the Catalogues screen's "Field App picker role"
  field and card line, and the seed configuration.
- **Field App lens range dropdown:**
  - Lists every lens set in the cached catalogue list (already filtered to those assigned at or
    above the retail point) that has at least one lens power, alphabetically by name, followed by
    **Custom prescription**.
  - Selecting a lens set sets `PresetCatalogueId` to that set.
  - Tests and Leads keep their existing "no preference" option.
- **No preselection** for a Sale. It shows "Select a lens range…", and the field is required
  (a client pre-submit error keyed on `LensRangeType`). Remove the `OnInitialized` default.
  Keep the comment's reasoning about the model matching what is on screen.
- **No lens sets reach the retail point:** offer only Custom prescription, with the note "No lens
  sets are assigned to this retail point — ask your administrator."
- **Admin Portal Lead→Sale conversion:** the lens range control offers the lens sets available at
  the *Lead's* location (the existing `ListAvailableForCallerAsync(lead.HierarchyPath)` call),
  then Custom. It is no longer an enum select list.
- **Migration:** existing rows with `LensRangeType` 0 or 1 become the new lens-set value on
  `Tests`, `Leads` and `Sales`. Drop the `Kind` column. The solution is not live, so **no
  compatibility alias** for the old wire values (ADR-0005).
- Rule messages that say "preset LensRangeType" should say "lens set". The product is not live,
  so this is not a breaking client-visible change, but update any test asserting the old copy.

**Key interfaces:**
- `SaleAssembly` (`Rules/Sales`): per CLAUDE.md, `SaleAssemblyTests` walks `CreateSaleRequest` by
  reflection. Keep `Seed`'s lens-block carry-over. A converted Lead's lens set carries over as-is,
  and if it's no longer available, ticket 09's check flags it; nothing substitutes a set silently.
- `ReferenceDataSnapshot.FromCachedReferenceData` (App adapter) and
  `ReferenceDataSnapshotProvider` (Infrastructure) both build `PresetCatalogueSnapshot`. Update
  both.

**Acceptance criteria:**
- [ ] `PresetCatalogueKind` no longer exists anywhere in `src/` or `tests/`.
- [ ] Rules tests: a lens-set Sale with a valid set and lens options passes. A Custom Sale
      behaves as before. Supplying custom fields with a lens set, or lens-set fields with Custom,
      fails as before.
- [ ] Field App: three lens sets with different names are all offered, alphabetically, plus
      Custom. A lens set with no lens powers isn't offered. With zero lens sets, only Custom
      plus the note is shown.
- [ ] Field App: a new Sale has no lens range selected, and submitting without one produces the
      `LensRangeType` field error.
- [ ] Admin Portal conversion offers the Lead's location's lens sets and posts a valid Sale.
- [ ] Migration maps existing 0/1 values to the lens-set value, verified by an Infrastructure
      test against real Postgres, and drops `Kind`.
- [ ] `docs/functional-capabilities.md` §4.6 / §5.6 updated. Remove the `Kind`/"picker role"
      text.
- [ ] CLAUDE.md's `PresetCatalogue`/`LensOption` domain-model bullet no longer describes
      `PresetCatalogueKind` or "the Field App's two preset-range buttons". Point it at ADR-0005.

**Out of scope:**
- Rejecting a lens set that isn't available at the record's location (ticket 09).
- Retire/reactivate (08), edit/assign permissions and unique names (10), dropping "Diopter
  range" and the remaining screen wording (11).

## Comments

> *This was generated by AI during triage.*
