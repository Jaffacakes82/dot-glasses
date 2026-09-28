# 01 — Rename the per-eye lens fields so they don't depend on the lens range

**What to build:** A prefactor. The per-eye sphere, cylinder, axis and add fields on Tests, Leads and
Sales (requests, DTOs, entities, columns) lose their "Custom" prefix and take range-neutral names
(e.g. sphere/cylinder/axis/add for left and right), so later tickets can store a lens-set lens's power
in the same fields. Behaviour is unchanged: a custom prescription records exactly as today, and every
server rejection still lands on the right form control in the Field App and the Admin Portal.

**Blocked by:** multi-org-access-and-retail-points 08 (migrations run one at a time; Spec B follows
Spec A)

**Status:** resolved

**Model:** Opus 5.5 — a wide rename across Contracts, Domain, Infrastructure, Rules, Web and App,
and the rule failure keys are load-bearing (CLAUDE.md).

**Seam:** the existing `DotGlasses.Rules.Tests`, `DotGlasses.Web.Tests` and
`DotGlasses.Infrastructure.Tests` suites stay green with the new names; add a Web.Tests case that a
custom-prescription rejection comes back keyed on the new property name.

**Don't run alongside:** any other migration-bearing ticket; anything touching the consultation
form, `ConsultationRules`, `SaleAssembly` or lead conversion.

## Acceptance criteria

- [x] `CreateTestRequest`, `CreateLeadRequest`, `CreateSaleRequest` and their DTOs use the
      range-neutral per-eye names. `LensOptionLeftId`/`LensOptionRightId` stay for now (ticket 05
      removes them).
- [x] Entities and columns are renamed by a migration that renames (not drops and re-adds) the columns,
      so existing values survive.
- [x] Rule failure keys follow the new names; `FormErrors`, `ValidationProblemDetails` and lead
      conversion's `Form.{PropertyName}` remap all move with them.
- [x] `SaleAnswers`/`SaleAssembly` are renamed in step and `SaleAssemblyTests`' reflection check passes.
- [x] Event History and Custom Orders keep working with the renamed columns.
- [x] No behaviour change: all existing tests pass after being updated only for names.

## Notes

- Spec: `../spec.md` — "Contracts", "Records". Decision: ADR-0007.
- Prior art: `ConsultationValidationApiTests`, `SaleAssemblyTests`, `LensRangeMigrationTests`.
- EF gotchas (handoff): Infrastructure as the startup project; placeholder
  `ConnectionStrings__dotglassesdb`; check the generated migration really renames.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

- **Names:** `CustomSphere{Left,Right}` → `Sphere{Left,Right}`, `CustomCylinder*` → `Cylinder*`,
  `CustomAxis*` → `Axis*`, `CustomAddPower*` → `Add{Left,Right}` (CONTEXT.md's "sphere, cylinder,
  axis and add"). Same names on requests, DTOs, entities, columns, `SaleAnswers`, the Admin Portal's
  `LeadConversionFormModel` and the Field App's `LensRangeSelection`. Private helpers
  `CustomPower`/`CustomAxis` in `ConsultationRules` kept their names — later tickets rewrite that
  branch.
- **Migration:** `20260928105332_RenameLensPowerFieldsRangeNeutral` — 24 `RenameColumn`s (Tests,
  Leads, Sales), no drops; follows `RemoveUserActiveOrg`. Pinned by `LensPowerRenameMigrationTests`
  (a Custom prescription written before the migration reads back intact on all three tables).
- **Client-visible copy:** messages that quote the property name move with it, e.g. "SphereLeft and
  SphereRight are required for a Custom LensRangeType." and "SphereLeft must be between -10 and 10
  in 0.25 increments." — intended, since the name is the key.
- **Outbox finding:** the outbox stores each create request as JSON and `SyncService` posts that
  string as-is; the Failed-records reload deserializes it into the request DTO. So a Custom record
  queued before the release (or created by a device still running the previous cached build) would
  arrive as `customSphereLeft`…, System.Text.Json would ignore the unknown names, and the record
  would be refused ("SphereLeft and SphereRight are required") with its prescription gone — and
  reloading it on Failed records would show blank lens fields, so it couldn't be recovered on the
  device either. Handled with `Contracts.Common.PreRenameLensPowerNames`, a JSON type-info modifier
  that accepts the eight old names as read-only aliases on the three create requests (fills the new
  property only if it's empty; never written out). Registered on the API's MVC JSON options
  (`Program.cs`) and on `ConsultationForm`'s Failed-record reload options. Pinned by
  `ConsultationValidationApiTests.AQueuedPayloadWithThePreRenameFieldNames_StillBindsItsPrescription`
  (verified red without the registration). Not covered: an *old* app build reading `LeadDto` with the
  new names (lead-match seeding) — it self-heals when the device picks up the new build. Delete the
  modifier once no device can hold a pre-rename payload; note ticket 05 drops
  `LensOptionLeftId`/`RightId`, which will break queued lens-set payloads regardless.
- **Deviations:** `LensRangeMigrationTests` and `ActiveOrgMigrationTests` wrote their "before" rows
  through the current model, which breaks on any later rename of a Tests/Leads/Sales column (EF
  inserts every mapped column). They now write them in raw SQL via a new test helper,
  `Postgres/PreMigrationRows` — same pattern `ActiveOrgMigrationTests` already used for users.
- **Stale docs for B12:** none found — no doc names the old properties; `functional-capabilities.md`
  only uses UI labels ("Add power"), which are unchanged.
- **Tests:** full `dotnet test DotGlasses.sln` — 475 passed, 0 failed (Rules 225, Application 96,
  Infrastructure 55, Web 99); baseline 472 + 3 new (two in `ConsultationValidationApiTests`, one
  migration test).
