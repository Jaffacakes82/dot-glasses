# 09 — Field App forgot-password screen

**What to build:** The Field App's sign-in page gets "Forgot password?", leading to a small screen
that takes an email address, calls the API, and shows the same message whatever the outcome.

**Blocked by:** 08

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** By hand in the browser; there is no Field App test project.

## Acceptance criteria

- [ ] A "Forgot password?" link on the sign-in page and a screen with one email field.
- [ ] Submitting calls the endpoint from ticket 08 directly. This is not an outbox write: it needs
      a connection and there is nothing to queue.
- [ ] With no connection the screen says so and sends nothing.
- [ ] After a successful call the screen shows "If that email has an account, we've sent a link."
      and a way back to sign-in.
- [ ] Manual checklist recorded in this ticket's Comments: the link appears; the message shows for
      a real and a made-up address; offline is handled; the emailed link ends at the Field App's
      sign-in.

## Notes

- Spec: `../spec.md` — user stories 27–28.
- Skills: `/code-review`.
