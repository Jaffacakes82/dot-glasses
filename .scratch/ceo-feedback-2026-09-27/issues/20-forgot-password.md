# 20 — Forgot password on the sign-in pages

Type: grilling
Status: resolved
Blocked by: None

## Question

Add "forgot password" to the sign-in pages. How is the reset link delivered, given invites still
hand the admin a link to send by hand?

## Context

- Feedback doc, Field App: "add forgot password to login page."
- The invite form says "No email sending is wired up yet"; ACS is provisioned in publish mode.

## Comments

- 2026-10-01, from the ticket 03 grilling: the user confirmed a forgot-password button is wanted
  on the sign-in pages (both, not only the Field App's). Ticket 03 keeps the admin's Reset
  password as a row button on the User Directory and removes the invite dialog's stale "No email
  sending is wired up yet" note, so the second context bullet above will stop being true.

## Answer

Decided by grilling, 2026-10-01.

**Where it appears**

- A "Forgot password?" link on both sign-in pages.
- The Admin Portal gets a forgot-password page that asks for the email address.
- The Field App gets its own small forgot-password screen that does the same through the API. It
  needs a connection.

**Delivery**

- By email only, to the account's address, with its own wording ("Reset your Dot Glasses
  password") and a line saying the link works for 24 hours.
- The link opens the existing set-password page on the Admin Portal, for both apps. There is one
  place where passwords are set.
- The link is never shown on screen. If the email doesn't arrive, the admin's Reset password
  button in the User Directory is the fallback.

**After the password is set**

- The link remembers which app the request came from, and the person is sent back to that app's
  sign-in page. Today everyone lands on the Admin Portal sign-in.

**What the person is told**

- The same message whether or not the address has an account: "If that email has an account,
  we've sent a link."

**Which accounts get an email**

- An active account: yes.
- An invited account that never set a password: yes. It works as a way to resend their invite.
- A suspended account: no email. The same message is shown.

**Limits**

- The link works for one day and stops working once the password has been changed.
- At most one email per account every five minutes, recorded on the account so it holds across
  servers. A request inside that window shows the same message and sends nothing.

**Known dependency**

- Email is sent from Azure's free shared domain, so messages are more likely to land in spam.
  Moving to a `dotglasses.com` sender is in `docs/open-issues.md` (ACS custom domain) and matters
  more once people rely on this.
- Local development only logs the link.
