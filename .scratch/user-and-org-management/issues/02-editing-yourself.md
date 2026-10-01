# 02 — Editing yourself: role locked, assignments only if scope doesn't shrink

**What to build:** An admin can open their own Edit page and correct their name. Their own role is
read-only and self-suspend is refused. They can assign themself to an organisation in their scope,
and can remove one of their own assignments only when their scope is no smaller afterwards. The
same rules hold on the Organisations screen's assign and unassign.

**Blocked by:** 01

**Status:** resolved

**Model:** Opus 5.5 — a scope comparison that must not lock the last admin out.

**Seam:** `DotGlasses.Web.Tests` over HTTP; the scope comparison in `Application.Tests`.

**Don't run alongside:** 01, 04.

## Acceptance criteria

- [x] Edit appears on the admin's own row. Role is read-only with "another admin must change it".
- [x] The server refuses a change to your own role and refuses suspending yourself, each as a
      `DomainRuleViolationException` with plain copy.
- [x] Your own full name is editable.
- [x] Adding your own assignment is accepted on the Edit page and on the Organisations screen.
- [x] Removing your own assignment is accepted only when your scope after the save is no smaller
      (the org sits beneath another assignment you keep). Otherwise refused with "ask another
      admin". Evaluated on the resulting assignment set, on both screens.
- [x] A self-removal at a retail point gets the same confirmation as any other.
- [x] Tests cover: own role change refused; self-suspend refused; self-add accepted; self-remove of
      a nested retail point accepted; self-remove of the outer assignment refused; each on both
      screens where it applies.

## Notes

- Spec: `../spec.md` — user stories 8–11; "Editing yourself (both screens)".
- `HierarchyPath.Outermost` already collapses nested paths; compare the collapsed sets.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** The rules live in `UserAdminService`, so both screens get them: own role
and self-suspend are refused; removing one of your own assignments is refused unless another you
keep covers it (`OwnAssignments.RemovalShrinksScope`, evaluated on the resulting set). The
directory hides Suspend on your own row. Tests: `EditUserTests` ("Editing yourself"),
`UserEditPlanTests`.
