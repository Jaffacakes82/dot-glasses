# 01 — Rename the per-eye lens fields so they don't depend on the lens range

**What to build:** A prefactor. The per-eye sphere, cylinder, axis and add fields on Tests, Leads and
Sales (requests, DTOs, entities, columns) lose their "Custom" prefix and take range-neutral names
(e.g. sphere/cylinder/axis/add for left and right), so later tickets can store a lens-set lens's power
in the same fields. Behaviour is unchanged: a custom prescription records exactly as today, and every
server rejection still lands on the right form control in the Field App and the Admin Portal.

**Blocked by:** multi-org-access-and-retail-points 08 (migrations run one at a time; Spec B follows
Spec A)

**Status:** ready-for-agent

**Model:** Opus 5.5 — a wide rename across Contracts, Domain, Infrastructure, Rules, Web and App,
and the rule failure keys are load-bearing (CLAUDE.md).

**Seam:** the existing `DotGlasses.Rules.Tests`, `DotGlasses.Web.Tests` and
`DotGlasses.Infrastructure.Tests` suites stay green with the new names; add a Web.Tests case that a
custom-prescription rejection comes back keyed on the new property name.

**Don't run alongside:** any other migration-bearing ticket; anything touching the consultation
form, `ConsultationRules`, `SaleAssembly` or lead conversion.

## Acceptance criteria

- [ ] `CreateTestRequest`, `CreateLeadRequest`, `CreateSaleRequest` and their DTOs use the
      range-neutral per-eye names. `LensOptionLeftId`/`LensOptionRightId` stay for now (ticket 05
      removes them).
- [ ] Entities and columns are renamed by a migration that renames (not drops and re-adds) the columns,
      so existing values survive.
- [ ] Rule failure keys follow the new names; `FormErrors`, `ValidationProblemDetails` and lead
      conversion's `Form.{PropertyName}` remap all move with them.
- [ ] `SaleAnswers`/`SaleAssembly` are renamed in step and `SaleAssemblyTests`' reflection check passes.
- [ ] Event History and Custom Orders keep working with the renamed columns.
- [ ] No behaviour change: all existing tests pass after being updated only for names.

## Notes

- Spec: `../spec.md` — "Contracts", "Records". Decision: ADR-0007.
- Prior art: `ConsultationValidationApiTests`, `SaleAssemblyTests`, `LensRangeMigrationTests`.
- EF gotchas (handoff): Infrastructure as the startup project; placeholder
  `ConnectionStrings__dotglassesdb`; check the generated migration really renames.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
