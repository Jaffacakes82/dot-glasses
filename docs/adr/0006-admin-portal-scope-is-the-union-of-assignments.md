# Admin Portal scope is the union of a user's assignments; the Field App records at one location

A user can be assigned to several orgs, but until 2026-09-28 only one of them was *active*.
Sign-in stamped that one org's path and level into the claims, the scope filter matched that one
path, and both permission checks tested it. The other assignments were only a list to switch
between, and only the Field App had a switcher. A user assigned to DGI and to a retail point stayed
scoped to whichever was active, with no way on the Admin Portal to change it. The CEO read this as
"access drops to the lowest org". On 2026-09-28 we decided the two apps scope differently:

- **The Admin Portal works across the user's whole scope** (see `CONTEXT.md`): everything at or
  beneath *any* of their org assignments, combined. A screen gated by level opens on the user's
  highest assigned level.
- **The Field App records at one current location.** Every Test, Lead and Sale must be stamped with
  exactly one place, so the Field App keeps a single location, chosen from the user's assignments.
  It is remembered per device, not on the user row, and shown on every form.

"Primary org" no longer exists. The user row stops carrying an active org, and any assignment can be
removed except the last; suspension is how all access is removed.

**Access is rechecked on every request.** The server re-reads the user's assignments, role and
suspension state on each request, on both apps, instead of trusting what the sign-in cookie or JWT
said. A removed assignment, a changed role or a suspension takes effect on the user's very next
request. Before this, changes were delayed by up to 30 minutes on the Admin Portal (the cookie's
security-stamp refresh) and up to 60 minutes on the Field App (the token's lifetime). That applied
to suspension too.

**Considered and rejected:**
- **One active org, with a switcher on both apps.** This is the smallest change, but it keeps the
  trap that caused the bug. A user who forgets to switch sees less than they are entitled to, and
  nothing tells them why.
- **Union on both apps.** A record can't be stamped with a union, so the Field App needs one
  location regardless.
- **One role per assignment** (e.g. Admin at Kenya, User at a Ugandan retail point). Nothing asks
  for it yet, and it would put a role lookup inside every scope decision. A user has one role across
  their scope. This may be revisited.
- **Accepting the refresh delay.** Rejected as a security risk. An assignment removed or a user
  suspended should not keep working for up to an hour.

**Consequences:**
- The claims no longer settle scope. The scope filter and the permission checks work from the
  user's current set of assignment paths, looked up per request. That costs one small lookup per
  request.
- A user can be managed by another admin only if *all* of the user's assignments sit in that
  admin's scope. Seeing the user needs only one. Without this rule, a country admin could suspend a
  DGI admin who also holds a retail-point assignment in that country.
- "Instant" reaches a Field App device only when it next connects. Records queued offline for a
  location the user has since lost are rejected at sync, not accepted, because the server has no
  trustworthy creation time for them (it stamps records on sync). They surface on Failed records
  and can be resent if the assignment is restored.
- Anything that used to read "the caller's org" needs an explicit choice instead. For example, a new
  lens set's owning org is picked from the admin's Country-or-above assignments.
- A current location must be an active retail point the user is *directly* assigned to (decided
  the same day). A broad scope does not widen where someone can record. A DGI or country admin
  with no retail-point assignment cannot record from the Field App at all. This is deliberate:
  where a technician records is an explicit admin decision, not a side effect of seniority.
