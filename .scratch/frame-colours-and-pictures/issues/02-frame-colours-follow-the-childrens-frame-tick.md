# 02 — Frame colours follow the "children's frame" tick

**What to build:** On the Field App's Sale form and the Admin Portal's Lead conversion form, the
colours offered are the child list when "children's frame" is ticked and the adult list when not.
Changing the tick clears the chosen colour. The server refuses a colour from the wrong list.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Opus 5.5 — one rule shared by the device, the server and the conversion form.

**Seam:** `DotGlasses.Rules.Tests` for the rule; `DotGlasses.Web.Tests` over HTTP; the Field App by hand.

**Don't run alongside:** Spec D tickets 01, 03 and 04, which edit the same form and rules file.

## Acceptance criteria

- [ ] `ConsultationRules.FrameColour` takes `ChildrensFrame` and requires an active item of the
      matching category. The failure is keyed on `FrameColourRefId`, with a message in Spec D's
      voice.
- [ ] Field App: the swatches come from the matching list. Changing the tick clears the chosen
      colour and its "Other" text. The pre-submit check uses the shared rule.
- [ ] Admin Portal's conversion form: the same, following the form's existing pattern (the script
      shows the matching list; without it the server validates and re-renders).
- [ ] Sales already recorded are unchanged and still show their colour's name.
- [ ] With "children's frame" ticked and no child list in an old offline cache, the form offers no
      colours and says to connect to load them. It never falls back to the adult list. No cache
      shape marker is bumped.
- [ ] Rule tests: each list against each tick value; "Other" with its text from the right list.
- [ ] Web.Tests: the Sale endpoint and the conversion form each refuse a colour from the wrong
      list.
- [ ] Manual checklist recorded in Comments: swatches switch with the tick; the choice clears; a
      children's frame with only "Other" available can be saved.

## Notes

- Spec: `../spec.md` — user stories 2–6; "The rule", "The forms".
- CLAUDE.md pitfall: a `bool` bound as a whole attribute value renders wrongly in Razor; write the
  string.
- Skills: `/tdd`, then `/code-review`.
