# 07 — Tests, Leads and Sales are recorded only at a valid current location

**What to build:** The server accepts a Test, Lead or Sale only at an active retail point the user is
directly assigned to, whatever the client sends. A refusal gives the technician a clear reason, which
the Field App's outbox shows on Failed records: "You're no longer assigned to <name> — ask your
admin.", "<name> has been deactivated." or "Choose a retail point before recording." Records at
training-org retail points are still accepted. On the Admin Portal, converting a Lead whose retail
point has since been deactivated is refused with a message naming it.

**Blocked by:** 06

**Status:** resolved

**Model:** Sonnet 5 — well-bounded: ticket 06 already tells you whether the current location is
valid and why; this ticket stamps from it and turns each reason into its message.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 06.

## Acceptance criteria

- [x] The Test, Lead and Sale create endpoints stamp `HierarchyPath` from the validated current
      location.
- [x] With no valid current location they refuse with a 400 `ValidationProblemDetails` keyed on `""`,
      carrying the matching one of the three messages above.
- [x] Admin Portal Lead conversion checks the Lead's retail point is still active before creating
      the Sale; if not, it throws `DomainRuleViolationException` naming the retail point.
- [x] Web.Tests cover the create endpoints refusing when: there is no current location; the location
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

## Comments

- Resolved on `feat/multi-org-access-07-record-at-location`. `CurrentLocationCheck` (ticket 06)
  gained `RefusalMessage` (`Application/Common/CurrentLocation.cs`): null when valid, else one of
  the three messages, keyed off `Status`. `NotRetailPoint` was mapped to the same "Choose a retail
  point before recording." copy as `NoLocation` — telling a technician "that's not a retail point"
  is no more actionable than telling them to pick one, and `CurrentLocationCheck.Of` only ever
  produces `NotRetailPoint` from a tampered/stale token, never from anything the Field App itself
  offered (as instructed, documented at the property).
- `TestsController`/`LeadsController`/`SalesController.Create` each gate on
  `currentUser.CurrentLocation.RefusalMessage` before anything else — including before
  `ConsultationRules.Check` — so an otherwise-incomplete body never matters when the location
  itself is the problem. The refusal is returned as `ValidationProblem(refusal.ToModelStateDictionary())`,
  a new `string` extension in `Web/Validation/ValidationResultExtensions.cs` that keys one message
  on `""`, the same slot `DomainRuleViolationFilter` uses. `SourceTestId`/`SourceLeadId` checks
  stayed exactly where they were, untouched.
- Lead conversion's retail-point-active check needed a lookup that can see a *deactivated* org
  node by its exact `HierarchyPath` — `IUnscopedReportQueryService` was the obvious first choice
  but it deliberately excludes soft-deleted rows (`!x.IsDeleted`), which is precisely the row this
  check needs to see. Added a small purpose-built `IOrganisationNodeLookup`
  (`Application/Organisations`, impl in `Infrastructure/Persistence/OrganisationNodeLookup.cs`,
  `IgnoreQueryFilters()`) rather than widening `IUnscopedReportQueryService`'s contract or reusing
  `IOrganisationAdminService` (scoped to the Organisations admin screen). `LeadConversionController`
  now injects it and checks right after the "already converted" guard, before any lens-range work,
  throwing `DomainRuleViolationException($"{name} has been deactivated.")` — same copy as the
  Field App's Deactivated message, for consistency. Checked on POST only (not the GET that renders
  the form), matching the ticket's "before creating the Sale" framing — prior art
  (`DomainRuleViolationScreenTests`) only ever gates the POST for this kind of refusal too.
- Left `VisionTestService`/`LeadService`/`SaleService`'s own `string.IsNullOrEmpty(hierarchyPath)`
  guard (and its "Your account has no org assignment..." message) untouched — the controllers now
  never call the service with an empty path, so it's unreachable through the API and stands as
  defence in depth, same relationship as the SourceTestId/SourceLeadId service guards. Its own
  Application.Tests keep asserting the old message; nothing there needed to change.
- New tests: `tests/DotGlasses.Web.Tests/RecordingLocationApiTests.cs` (8 tests — every
  `CurrentLocationStatus` reason on the Tests endpoint, the same guard pinned on Leads/Sales via
  the no-location case, and one accepted at a training-org retail point) and
  `tests/DotGlasses.Web.Tests/LeadConversion/LeadConversionRetailPointStatusTests.cs` (1 test).
  Each `RecordingLocationApiTests` client builds its JWT directly via `IJwtTokenService` rather
  than through `/api/v1/auth/login`, because sign-in only ever issues a Valid-or-NoLocation token
  — `NotRetailPoint`/`Deactivated`/an indirectly-assigned retail point can only be produced the
  way a stale or tampered device token would reach the server.
- Full suite: `dotnet test DotGlasses.sln` — 468 passed, 0 failed (225 Rules.Tests + 96
  Application.Tests + 51 Infrastructure.Tests + 96 Web.Tests), up from the 459 baseline by exactly
  the 9 new tests.
- No deviations from the ticket beyond the `NotRetailPoint` message choice called out above, which
  the ticket asked to be recorded here.
