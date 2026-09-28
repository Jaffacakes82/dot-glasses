# 07 — Tests, Leads and Sales are recorded only at a valid current location

**What to build:** The server accepts a Test, Lead or Sale only at an active retail point the user is
directly assigned to, whatever the client sends. A refusal gives the technician a clear reason, which
the Field App's outbox shows on Failed records: "You're no longer assigned to <name> — ask your
admin.", "<name> has been deactivated." or "Choose a retail point before recording." Records at
training-org retail points are still accepted. On the Admin Portal, converting a Lead whose retail
point has since been deactivated is refused with a message naming it.

**Blocked by:** 06

**Status:** ready-for-agent

**Model:** Sonnet 5 — well-bounded: ticket 06 already tells you whether the current location is
valid and why; this ticket stamps from it and turns each reason into its message.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 06.

## Acceptance criteria

- [ ] The Test, Lead and Sale create endpoints stamp `HierarchyPath` from the validated current
      location.
- [ ] With no valid current location they refuse with a 400 `ValidationProblemDetails` keyed on `""`,
      carrying the matching one of the three messages above.
- [ ] Admin Portal Lead conversion checks the Lead's retail point is still active before creating
      the Sale; if not, it throws `DomainRuleViolationException` naming the retail point.
- [ ] Web.Tests cover the create endpoints refusing when: there is no current location; the location
      is not Retail Point level; the retail point is assigned only indirectly; the assignment was
      removed after the token was issued; the retail point has been deactivated. They accept one at a
      training-org retail point. Admin Portal conversion at a deactivated retail point is refused with
      its message.

## Notes

- Spec: `../spec.md` — "Recording", user stories 34–40. Don't move `SourceTestId`/`SourceLeadId`
  checks (CLAUDE.md: they stay on the controllers, field-keyed).
- Prior art: `ConversionSourceScopingApiTests`, `DomainRuleViolationApiTests`,
  `DomainRuleViolationScreenTests`, `LeadConversionLensSetTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
