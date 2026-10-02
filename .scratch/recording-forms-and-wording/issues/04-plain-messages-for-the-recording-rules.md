# 04 — Finish the plain wording of the Test, Lead and Sale rules

**What to build:** Most recording-rule messages were already reworded on 2026-10-01
(`.scratch/lens-set-feedback-2026-10-01/issues/06-developer-wording-in-form-errors.md`, which lists
every message changed and every one left technical). This ticket finishes the job to the voice
agreed in map ticket 22: the messages that were left technical get one plain message per group,
the remaining developer copy is reworded, and the Field App's labels on the three forms are tidied.

**Blocked by:** 02, 03

**Status:** resolved

**Model:** Sonnet 5.5 — the hard part is done; keys must still not change.

**Seam:** `DotGlasses.Rules.Tests`; `DotGlasses.Web.Tests` over HTTP for the response shape.

**Don't run alongside:** any ticket in Specs E and F that adds a rule to `ConsultationRules`.

## Acceptance criteria

- [x] The messages the earlier ticket lists under "Left technical" can still reach a technician
      on Failed records (a record queued under older rules). They are grouped, and each group
      gets one plain message saying what to do, for example "This record's lens details don't
      match its lens range. Open it and choose the lens again." The grouping is listed in
      Comments.
- [x] `LeadsController` and `SalesController`'s "SourceTestId must reference an existing Test." and
      "SourceLeadId must reference an existing Lead." are reworded in the same voice. They stay on
      the controllers and stay field-keyed.
- [x] The three rule messages that quote Field App labels which read differently on the Admin
      Portal's Lead conversion form are made to read correctly on both.
- [x] Messages reachable from a form are checked against the voice in the spec (what to do, the
      screen's own words, no "must", "invalid" or "required when", a full stop). The lens power
      sentences shared with the Add lens dialog are brought into line in both places, or the
      reason for leaving them is recorded in Comments.
- [x] No failure key changes. `FormErrors`, the `ValidationProblemDetails` shape and
      `LeadConversionController`'s `Form.{PropertyName}` remap behave as before.
- [x] A guard test walks the failures produced for a set of bad Test, Lead and Sale requests and
      asserts no message contains a request property name or an enum member name.
- [x] Every existing test that pins a message is updated; none is deleted.
- [x] The Field App's labels and prompts on the three forms are reviewed against the same voice.
- [x] `CLAUDE.md`'s "There is no consultation request validator" bullet no longer says some
      messages "stay technical".
- [x] A before-and-after table of every message changed here is added to Comments.

## Notes

- Spec: `../spec.md` — user stories 10–11; "Wording".
- Read ADR-0002 before touching keys.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** No failure key changed. Guard test:
`ConsultationRulesTests.NoMessage_NamesARequestPropertyOrAnEnumMember_OrReadsAsDeveloperCopy`.

**Grouping of the messages that were left technical**

| Group | Keys | One message |
|---|---|---|
| The record itself | `Id` (empty), `Gender`, `Outcome`, `LensRangeType` (Sale), `FrameCoverage` (value outside the list) | This record can't be saved as it was sent. Open it, check each answer and save it again. |
| Lens details that don't fit the lens range | `LensRangeType` (lens fields with no range; a lens set named on a Custom range), `PresetCatalogueId` (missing on a lens set), `PupilDistanceMm` (millimetres on a lens set), `PresetPupilDistanceBucket` (bucket on Custom), `LensTypeRefId` and `LensTypeOtherText` (not the chosen lenses' own) | This record's lens details don't match its lens range. Open it and choose the lens again. |
| Pupil distance not a whole millimetre | `PupilDistanceMm` | Choose a pupil distance in whole millimetres. |
| A coating listed twice | `CoatingRefIds` | A coating is ticked twice on this record. Choose the coatings again. |

The out-of-list messages no longer quote the number back; the key still says which answer it was.

**Other messages changed**

| Key | Before | After |
|---|---|---|
| `SphereRight` | Both eyes' lenses must be the same lens type — choose a right-eye lens of the left eye's type. | Choose a right-eye lens of the same lens type as the left eye's. |
| `CoatingRefIds` | Every coating must be configured as available for the chosen lenses (see Lens Sets). | One of the chosen coatings isn't made on these lenses — choose the coatings again. |
| sphere, cylinder, add (each eye) | {Label} must be between {min} and {max} in {step} increments. | {Label}: choose a value between {min} and {max}, in steps of {step}. |
| axis | {Axis} is required when {Cylinder} isn't 0.00 — choose an axis from 0 to 180. | {Axis}: choose an axis from 0 to 180 — {Cylinder} isn't 0.00. |
| axis | {Axis} must be empty when {Cylinder} is 0.00 — an axis only applies to a cylinder. | {Axis}: clear the axis — it only applies when {Cylinder} isn't 0.00. |
| axis | {Axis} must be a whole number of degrees between 0 and 180. | {Axis}: choose a whole number of degrees from 0 to 180. |
| sphere (Add lens dialog only) | {Sphere} is required. | {Sphere}: choose a value. |
| `SourceTestId` | SourceTestId must reference an existing Test. | The test this lead continues from can't be found at this location. Discard this record and record the lead again. |
| `SourceTestId` | This Test has already been converted into a Lead. | This test has already been continued into a lead. Discard this record. |
| `SourceLeadId` | SourceLeadId must reference an existing Lead. | The lead this sale converts can't be found at this location. Discard this record and record the sale again. |
| `SourceLeadId` | This Lead has already been converted into a Sale. | This lead has already been converted into a sale. Discard this record. |

The lens power sentences are shared with the Add lens dialog, so they changed in both places
(`LensDialogTests`, `LensPowerWordingTests` updated).

**The three messages that quote Field App labels.** "Tick "Referred or treated"…", "…untick
"Treated in facility"." and the hard case message read wrongly on the Admin Portal's conversion
form because its labels differed. The form's labels now match the Field App's: "Referred or
treated", "Treated in facility (rather than referred elsewhere)" and "Did this person buy a hard
case?". The messages are unchanged.

**Field App labels and prompts.** "Please fix the highlighted fields before saving." is now "Fix the
highlighted fields, then save."; the "Other" free-text label "— please specify" is "— say which";
the referral location carries its hint as a placeholder.
