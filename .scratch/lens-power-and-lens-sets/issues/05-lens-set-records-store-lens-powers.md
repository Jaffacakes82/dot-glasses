# 05 — Lens-set records store each eye's lens power

**What to build:** A Test, Lead or Sale made from a lens set stores each eye's lens power and one lens
type, exactly as a custom prescription does, with no pointer to a lens in the set. The server accepts
it only when each eye's power and the lens type match a lens in the chosen set (which must still reach
the location), and refuses a mixed pair (eyes with different lens types). Converting a Lead carries
each eye's power and the lens type into the Sale, and the Field App and Admin Portal pre-select the
matching lens. This is the change that lets a +3.00 from a lens set and a +3.00 custom lens be
recognised as the same lens.

**Blocked by:** 02, 04

**Status:** ready-for-agent

**Model:** Opus 5.5 — breaking contract change with load-bearing failure keys, a migration, and the
lens-set branch of the consultation rules.

**Seam:** `DotGlasses.Rules.Tests` (lens-set branch, `SaleAssembly.Seed`); `DotGlasses.Web.Tests`
(create endpoints, Admin Portal lead conversion); `DotGlasses.Infrastructure.Tests` if the migration
needs a data check.

**Don't run alongside:** any other migration-bearing ticket; 06, 09, 10, 11 (consultation form, lead
conversion, `ConsultationRules`, `SaleAssembly`). 07 is safe in parallel (Lens Sets screen only).

## Acceptance criteria

- [ ] `LensOptionLeftId`/`LensOptionRightId` are dropped from the create requests, DTOs, entities and
      columns (migration). `PresetCatalogueId` and `LensTypeRefId` stay; a null lens type means single
      vision.
- [ ] The lens-set branch of `ConsultationRules`: each eye's power plus the lens type must match a lens
      in the chosen set (via the ticket 04 matching helper); both eyes' lenses share the lens type; the
      set must reach the location as today.
- [ ] `SaleAssembly.Seed` carries each eye's power and the lens type instead of lens ids;
      `SaleAssemblyTests`' reflection check still passes.
- [ ] The Field App and the Admin Portal lead conversion send the chosen lens's power and lens type
      with minimal UI change (the redesign is tickets 09 and 11). Seeding from a Lead or Test, and from
      a Failed record, uses the matching helper.
- [ ] An old-shape request (with lens ids) is rejected; an old-shape outbox record lands on Failed
      records (accepted).
- [ ] Rules.Tests: each eye must match by power and lens type; a mixed pair is refused;
      `SaleAssembly.Seed` carries powers and the lens type. Web.Tests: create endpoints store powers for
      both ranges and reject an old-shape request and a lens-set power with no matching lens.

## Notes

- Spec: `../spec.md` — "The lens-set branch of the consultation rules", "Contracts", "Records",
  user stories 1, 36–38, 40. CLAUDE.md on `SaleAssembly` (seed, not override) and failure keys.
- Prior art: `ConsultationRulesTests`, `SaleAssemblyTests`, `LeadConversionLensSetTests`,
  `ConsultationValidationApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
