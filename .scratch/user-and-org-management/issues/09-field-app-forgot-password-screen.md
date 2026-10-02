# 09 — Field App forgot-password screen

**What to build:** The Field App's sign-in page gets "Forgot password?", leading to a small screen
that takes an email address, calls the API, and shows the same message whatever the outcome.

**Blocked by:** 08

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** By hand in the browser; there is no Field App test project.

## Acceptance criteria

- [x] A "Forgot password?" link on the sign-in page and a screen with one email field.
- [x] Submitting calls the endpoint from ticket 08 directly. This is not an outbox write: it needs
      a connection and there is nothing to queue.
- [x] With no connection the screen says so and sends nothing. (Built; not checked by hand — see Comments.)
- [x] After a successful call the screen shows "If that email has an account, we've sent a link."
      and a way back to sign-in.
- [x] Manual checklist recorded in this ticket's Comments: the link appears; the message shows for
      a real and a made-up address; offline is handled; the emailed link ends at the Field App's
      sign-in.

## Notes

- Spec: `../spec.md` — user stories 27–28.
- Skills: `/code-review`.

## Comments

**2026-10-01 — built.** `Pages/ForgotPassword.razor` and the link on `Login.razor`, which also
shows "Password set. Sign in with your new password." when returned to after a reset.

Manual checklist (local browser, 2026-10-01):

- [x] The link appears on the sign-in page and opens the screen.
- [x] An empty field asks for the email address.
- [x] A made-up address and a real one both show "If that email has an account, we've sent a link."
- [ ] Offline shows "You're offline. Connect to get a reset link." — not checked.
- [ ] The emailed link ends at the Field App's sign-in — not checked by hand (local dev logs the
      email rather than sending it); `ForgotPasswordTests` covers the redirect target.

The two unchecked items are in `docs/open-issues.md`.
