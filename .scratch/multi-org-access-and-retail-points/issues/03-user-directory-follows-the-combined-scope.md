# 03 — User Directory rules follow the combined scope, with no primary org

**What to build:** A country admin sees every user who has any assignment in their scope, but is
refused when suspending, resetting the password of, or changing the role of a user who also has
assignments outside that scope. Admins can add or remove any single assignment within their scope,
except a user's last one, which is refused with a message telling them to suspend the user instead.
"Primary org" disappears: invite takes any number of orgs with no ordering meaning, and the form no
longer says the first ticked org becomes primary.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Opus 5.5 — a new permission rule (all of the target's assignments in scope) plus atomic
Identity writes.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 06 (both touch the service that manages user org assignments — merge
carefully if they overlap).

## Acceptance criteria

- [ ] User Directory lists a user if any of their assignment paths is in the actor's scope (the
      existing manual prefix query for Identity users, now against assignment paths and any scope path).
- [ ] A new user-target requirement: suspend, reset password and change role need *all* of the target
      user's assignment paths within the actor's scope. Adding or removing one assignment keeps the
      existing per-org check.
- [ ] Removing a user's last assignment throws `DomainRuleViolationException` with copy telling the
      admin to suspend the user instead. The old "primary org" refusal is gone.
- [ ] Invite takes one or more orgs with no primary. The "first checked becomes primary/active
      location" hint is removed from the invite form. Invite still commits atomically (CLAUDE.md,
      `InviteAtomicityTests`).
- [ ] Web.Tests cover: listing needs any assignment in scope; suspend/reset/role change refused when
      the target has an assignment outside scope; removing the last assignment is refused with its
      message; invite with several orgs creates every assignment, none special.

## Notes

- Spec: `../spec.md` — user stories 10–15, "Schema and user management". The rest of the User
  Directory form redesign is map ticket 03 and out of scope.
- Prior art: `AccessControlPolicyTests`, `InviteAtomicityTests`, `DomainRuleViolationScreenTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
