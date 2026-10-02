# 05 — Plain messages for the Admin Portal's forms and both sign-in pages

**What to build:** The Admin Portal's own forms report errors in the same voice as the recording
forms. Both sign-in pages ask for an email. Switching location on the Field App tells being offline
apart from a real failure.

**Blocked by:** None

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP; the Field App by hand.

## Acceptance criteria

- [x] The eight Admin Portal validators (Organisations, Lens Sets, Reference Data, User Directory)
      carry messages in the spec's voice.
- [x] `DomainRuleViolationException` messages that still read as developer copy are reworded.
      Those already written as plain sentences are left alone.
- [x] Both sign-in pages label the field "Email" and show "Email or password is incorrect." on a
      failed sign-in.
- [x] Field App location switching shows "You're offline. Connect to switch location." with no
      connection, and "Couldn't switch location. Try again." for any other failure. The outlet
      picker shows the same two.
- [x] Web.Tests that pin the old wording are updated; a sign-in failure test asserts the new copy.
- [x] `docs/open-issues.md`: the "Location switching only works online" entry drops its
      "a clearer message is the honest fix, not yet done" sentence.
- [x] Manual checklist recorded in Comments: the two location-switch messages.

## Notes

- Spec: `../spec.md` — user stories 12–14; "Wording".
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** All eight validators carry their own messages (no FluentValidation stock
copy is left on a field a person fills in). Rejections reworded: no organisation assigned (four
services), "Choose at least one organisation.", "Choose a role.", the lens set owner level, the
organisation child level, the coating exclusion's retired coating, and the custom order with no
order behind it. Both sign-in pages already labelled the field "Email"; the failure message is now
"Email or password is incorrect." and the Admin Portal's blank-field messages are "Enter your email
address." / "Enter your password.". `UserLocationClient.SwitchOrgAsync` returns the message to show
(null on success), so Settings and the outlet picker show the same two.

Tests: `SignInWordingTests`; pinned copies updated in `UserDirectoryScopeTests`,
`CreateLensSetOwningOrgTests`, `LensSetNameTests`, `LensDialogTests`, `LensSetAvailabilityTests`,
`CustomOrderAdvanceStatusTests` and the three service tests.

Manual checklist — not done in a browser; listed in `docs/open-issues.md`:

- [ ] Settings, device offline: "You're offline. Connect to switch location."
- [ ] Settings, server failing or refusing: "Couldn't switch location. Try again."
- [ ] The outlet picker shows the same two.
