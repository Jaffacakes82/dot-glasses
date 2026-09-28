# 04 — Creating a lens set means choosing its owning org

**What to build:** An admin with several DGI- or Country-level assignments chooses which of them owns
a new lens set. With only one qualifying assignment it is chosen automatically and no field is shown.
The server refuses an owning org that isn't one of the admin's DGI or Country assignments. The rest of
the Lens Sets screen works on the combined scope from ticket 01.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Sonnet 5 — a bounded form field and server check; the scope-aware permission requirements
already exist after ticket 01.

**Seam:** `DotGlasses.Web.Tests` — the Lens Sets screen's create POST (POST-redirect-GET).

**Don't run alongside:** 01. Spec B's lens-set tickets (B03, B07, B08) build on this one.

## Acceptance criteria

- [ ] Lens set creation takes an explicit owning org, offered from the admin's assignments at DGI or
      Country level; the field shows only when more than one qualifies.
- [ ] The server checks the chosen org is one of those assignments; otherwise the request is refused.
- [ ] Lens set creation no longer reads the old single active org.
- [ ] Web.Tests cover: create with an explicit owning org; a choice outside the admin's DGI/Country
      assignments is refused; a single qualifying assignment is used automatically.

## Notes

- Spec: `../spec.md` — user stories 20–21, "Admin Portal screens".
- Prior art: `RetireLensSetScreenTests`, `CataloguesScreenWordingTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.
