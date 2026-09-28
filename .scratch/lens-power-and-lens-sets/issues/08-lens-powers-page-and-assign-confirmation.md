# 08 — The Lens powers page, and a success message after assigning

**What to build:** Next to Lens Sets, admins can open a read-only Lens powers page showing each allowed
value list (sphere, cylinder, axis, add) and the validity rules, rendered from the Rules definition.
Assigning a lens set to an org shows a confirmation message after the redirect.

**Blocked by:** 02

**Status:** ready-for-agent

**Model:** Sonnet 5 — a read-only Razor view and a TempData message.

**Seam:** `DotGlasses.Web.Tests` — the rendered page and the post-assign redirect.

**Don't run alongside:** 03, 07 (Lens Sets controller and view).

## Acceptance criteria

- [ ] A read-only Lens powers tab or section of the Lens Sets screen, behind the same policy, rendered
      from the Rules allowed values and display format (nothing hard-coded in the view).
- [ ] It states the validity rules (axis only with a cylinder; lens type only with an add).
- [ ] Assigning a lens set shows a success message after the redirect.
- [ ] Web.Tests: the page renders the allowed values (e.g. cylinder stops at -6.00, no positive
      cylinder); a user without the policy can't reach it; the success message appears after assigning.
- [ ] Any new style uses the design tokens (both `dot-glasses.css` copies if a token is added).

## Notes

- Spec: `../spec.md` — "The Lens powers page", "Assigning a lens set", user stories 3 and 18.
- Prior art: `AccessControlPolicyTests`, `CataloguesScreenWordingTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
