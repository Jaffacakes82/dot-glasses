# 04 — Plain messages for the Test, Lead and Sale rules

**What to build:** Every error a technician can meet on the recording forms, on Failed records or
on the Admin Portal's Lead conversion form says what to do, in the words on the screen. Failures
that can't be caused from the form share one plain message per group. The Field App's labels on the
three forms are tidied in the same voice.

**Blocked by:** 02, 03

**Status:** ready-for-agent

**Model:** Opus 5.5 — every message in `ConsultationRules` changes while every key must not.

**Seam:** `Application.Tests`; `DotGlasses.Web.Tests` over HTTP for the response shape.

**Don't run alongside:** any ticket in Specs E and F that adds a rule to `ConsultationRules`.

## Acceptance criteria

- [ ] Every `RuleFailure` message in `ConsultationRules` follows the voice in the spec: what to
      do, the screen's own words, no internal field names, no "must", "invalid" or "required
      when", British spelling, a full stop.
- [ ] No failure key changes. `FormErrors`, the `ValidationProblemDetails` shape and
      `LeadConversionController`'s `Form.{PropertyName}` remap behave as before.
- [ ] Failures that can't be caused from the form are grouped, each group with one message that
      says what to do. The grouping is listed in this ticket's Comments.
- [ ] A guard test walks the failures produced for a set of bad Test, Lead and Sale requests and
      asserts no message contains a request property name.
- [ ] Every existing test that pins a message is updated; none is deleted.
- [ ] The Field App's labels and prompts on the three forms are reviewed against the same voice.
- [ ] `CLAUDE.md`'s "There is no consultation request validator" bullet no longer says the scalar
      messages reproduce FluentValidation's copy verbatim.
- [ ] A before-and-after table of every message is added to this ticket's Comments.

## Notes

- Spec: `../spec.md` — user stories 10–11; "Wording".
- Read ADR-0002 before touching keys.
- Skills: `/tdd`, then `/code-review`.
