# 04 — Finish the plain wording of the Test, Lead and Sale rules

**What to build:** Most recording-rule messages were already reworded on 2026-10-01
(`.scratch/lens-set-feedback-2026-10-01/issues/06-developer-wording-in-form-errors.md`, which lists
every message changed and every one left technical). This ticket finishes the job to the voice
agreed in map ticket 22: the messages that were left technical get one plain message per group,
the remaining developer copy is reworded, and the Field App's labels on the three forms are tidied.

**Blocked by:** 02, 03

**Status:** ready-for-agent

**Model:** Sonnet 5.5 — the hard part is done; keys must still not change.

**Seam:** `DotGlasses.Rules.Tests`; `DotGlasses.Web.Tests` over HTTP for the response shape.

**Don't run alongside:** any ticket in Specs E and F that adds a rule to `ConsultationRules`.

## Acceptance criteria

- [ ] The messages the earlier ticket lists under "Left technical" can still reach a technician
      on Failed records (a record queued under older rules). They are grouped, and each group
      gets one plain message saying what to do, for example "This record's lens details don't
      match its lens range. Open it and choose the lens again." The grouping is listed in
      Comments.
- [ ] `LeadsController` and `SalesController`'s "SourceTestId must reference an existing Test." and
      "SourceLeadId must reference an existing Lead." are reworded in the same voice. They stay on
      the controllers and stay field-keyed.
- [ ] The three rule messages that quote Field App labels which read differently on the Admin
      Portal's Lead conversion form are made to read correctly on both.
- [ ] Messages reachable from a form are checked against the voice in the spec (what to do, the
      screen's own words, no "must", "invalid" or "required when", a full stop). The lens power
      sentences shared with the Add lens dialog are brought into line in both places, or the
      reason for leaving them is recorded in Comments.
- [ ] No failure key changes. `FormErrors`, the `ValidationProblemDetails` shape and
      `LeadConversionController`'s `Form.{PropertyName}` remap behave as before.
- [ ] A guard test walks the failures produced for a set of bad Test, Lead and Sale requests and
      asserts no message contains a request property name or an enum member name.
- [ ] Every existing test that pins a message is updated; none is deleted.
- [ ] The Field App's labels and prompts on the three forms are reviewed against the same voice.
- [ ] `CLAUDE.md`'s "There is no consultation request validator" bullet no longer says some
      messages "stay technical".
- [ ] A before-and-after table of every message changed here is added to Comments.

## Notes

- Spec: `../spec.md` — user stories 10–11; "Wording".
- Read ADR-0002 before touching keys.
- Skills: `/tdd`, then `/code-review`.
