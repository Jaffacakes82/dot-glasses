# 08 — Forgot password: Admin Portal page, API endpoint and email

**What to build:** The Admin Portal sign-in page gets "Forgot password?", leading to a page that
takes an email address and always answers the same way. An equivalent anonymous API endpoint serves
the Field App. A reset email is sent when appropriate, linking to the existing set-password page,
which returns the person to the app they came from.

**Blocked by:** None

**Status:** resolved

**Model:** Opus 5.5 — an anonymous endpoint that must not leak or be abusable.

**Seam:** `DotGlasses.Web.Tests` over HTTP, with a hand-written recording `IEmailSender` fake.

## Acceptance criteria

- [x] An anonymous Admin Portal page and an anonymous `api/v1/auth` endpoint, both taking an email
      address and both always answering "If that email has an account, we've sent a link."
- [x] An email is sent for an Active or Invited account. None for a Suspended account, an unknown
      address, or an account emailed within the last five minutes. The last-sent time is stored on
      the user row (a migration), so the limit holds across replicas.
- [x] A "Forgot password?" link on the Admin Portal's sign-in page.
- [x] A new `IEmailSender` method with the subject "Reset your Dot Glasses password" and its own
      wording, saying the link works for 24 hours. `AzureEmailSender` and `LoggingEmailSender`
      both implement it; neither throws.
- [x] The link works for one day and stops working once the password has been changed. Confirm
      the token lifetime is one day (set it explicitly if it relies on a framework default).
- [x] The email is sent after the last-sent time is committed, never before (CLAUDE.md: anything
      an operation emits is produced after the commit).
- [x] An Invited account that never set a password gets the email, and the link lets them set
      one.
- [x] The link opens the existing set-password page and carries which app asked. After a
      successful reset the page redirects to that app's sign-in; the Field App's address comes from
      configuration, never from the link itself.
- [x] The link is never rendered on screen or returned by the API.
- [x] A reset doesn't unsuspend anyone.
- [x] Web.Tests cover: identical responses for a known, unknown and suspended address; one email
      for an active account and for an invited one; none for a suspended one; none on a second
      request within five minutes; a link used once no longer works; the redirect target for each
      app.

## Notes

- Spec: `../spec.md` — user stories 27–29; "Forgot password".
- The redirect target must be a fixed, configured address: an open redirect on a password page is
  a phishing aid.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** `IPasswordResetService`/`PasswordResetService`, `PasswordResetRequester`,
`AccountController.ForgotPassword`, `POST api/v1/auth/forgot-password`,
`IEmailSender.SendPasswordResetAsync`, migration `AddPasswordResetEmailSentAt`.

- The five-minute limit is one conditional `UPDATE` on the account's last-sent time, so exactly one
  of any simultaneous requests sends, on any replica; the token is minted and the email sent after
  it commits.
- The token lifetime is set explicitly to one day in `Program.cs`.
- The Field App's address is `FieldApp` in `appsettings.json`, looked up by the Admin Portal host
  the request arrived on (one build serves both environments), with a localhost fallback. The link
  carries only `app=field`.

Tests: `ForgotPasswordTests` with a recording `IEmailSender`.
