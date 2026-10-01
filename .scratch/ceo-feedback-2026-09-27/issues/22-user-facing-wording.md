# 22 — Plain, friendly prompts and error messages

Type: grilling
Status: resolved
Blocked by: 10, 13

## Question

Which prompts and error messages need rewording, and what voice do they take? Note CLAUDE.md:
rewording a scalar rule message is a client-visible change.

## Context

- Feedback doc: "Update user prompts and error messages to be user friendly and clear".

## Answer

Decided by grilling, 2026-10-01.

**What gets reworded**

- Every error message a person can see in either app: the Test, Lead and Sale rules in
  `ConsultationRules`, the Admin Portal's own form validators (organisations, lens sets, reference
  data, invites), the sign-in pages and the Field App's location-switch message.
- The labels and prompts on the Field App's three recording forms, which tickets 13 and 14 are
  reordering anyway.
- Not a review of every label in the Admin Portal.

**The voice, for every message**

- Say what to do, in a short sentence: "Enter the customer's full name." "Choose a reason for the
  referral."
- Use the words on the screen, never a field's internal name.
- No "must", "invalid" or "required when"; no "please"; no blame.
- State limits plainly: "Age is between 0 and 120."
- British spelling, sentence case, a full stop.

**Messages a person can't cause from the form**

- Rules that only fail for a record queued under old rules or tampered with (for example "Lens set
  fields must be empty for a Custom lens range") are met only on the Failed records screen. Each
  group gets one plain message that says what to do, such as "This record's lens details don't
  match its lens range. Open it and choose the lens again."

**Sign-in and connection**

- "Username" becomes "email" on both sign-in pages.
- Switching location tells being offline ("You're offline. Connect to switch location.") apart
  from a real failure ("Couldn't switch location. Try again."). This closes the "Location
  switching only works online" message gap in `docs/open-issues.md`.

**Writing the copy**

- The copy is written as part of the build, following the voice above. No separate review step
  was asked for.

**Changing the messages is safe**

- Only the two apps receive these messages and the product isn't live, so nothing depends on the
  current wording. Failure keys (the request property names) do not change; only the text does.
  Tests that pin the current wording are updated with it, and `CLAUDE.md`'s note that the scalar
  messages reproduce FluentValidation's copy verbatim is revised.

**Other languages**

- English only. Translation is a separate project.

## Comments

- 2026-10-01, found when merging `origin/main`: most of the recording-rule messages had already
  been reworded the same day, under
  `.scratch/lens-set-feedback-2026-10-01/issues/06-developer-wording-in-form-errors.md`. That
  ticket deliberately left the messages no form can cause in technical wording. The decision
  above still stands (one plain message per group), so Spec D's ticket 04 covers only what is
  left: those messages, two on the controllers, the few that don't yet match the voice, and the
  Field App's labels. `CLAUDE.md` also now counts eight Admin Portal validators, not nine.
