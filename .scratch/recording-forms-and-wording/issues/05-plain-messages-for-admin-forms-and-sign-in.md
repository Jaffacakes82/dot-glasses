# 05 — Plain messages for the Admin Portal's forms and both sign-in pages

**What to build:** The Admin Portal's own forms report errors in the same voice as the recording
forms. Both sign-in pages ask for an email. Switching location on the Field App tells being offline
apart from a real failure.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP; the Field App by hand.

## Acceptance criteria

- [ ] The nine Admin Portal validators (Organisations, Lens Sets, Reference Data, User Directory)
      carry messages in the spec's voice.
- [ ] `DomainRuleViolationException` messages that still read as developer copy are reworded.
      Those already written as plain sentences are left alone.
- [ ] Both sign-in pages label the field "Email" and show "Email or password is incorrect." on a
      failed sign-in.
- [ ] Field App location switching shows "You're offline. Connect to switch location." with no
      connection, and "Couldn't switch location. Try again." for any other failure. The outlet
      picker shows the same two.
- [ ] Web.Tests that pin the old wording are updated; a sign-in failure test asserts the new copy.
- [ ] `docs/open-issues.md`: the "Location switching only works online" entry drops its
      "a clearer message is the honest fix, not yet done" sentence.
- [ ] Manual checklist recorded in Comments: the two location-switch messages.

## Notes

- Spec: `../spec.md` — user stories 12–14; "Wording".
- Skills: `/tdd`, then `/code-review`.
