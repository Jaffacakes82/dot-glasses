# 03 — One org tree picker for Invite and Edit

**What to build:** The Invite dialog and the Edit user page share one organisation picker: an
indented tree showing each organisation's level, a filter box, and a line explaining what an
assignment grants. The misaligned checkbox and the stale email note go.

**Blocked by:** 01, 05

**Status:** ready-for-agent

**Model:** Sonnet 5.5 — a shared partial and a small filter script.

**Seam:** `DotGlasses.Web.Tests` over HTTP for what is offered; the filter by hand.

## Acceptance criteria

- [ ] One partial used by Invite and Edit, labelled "Organisations", with the checkbox beside its
      label.
- [ ] Organisations are indented by depth and show their level label (from ticket 05).
- [ ] A filter box narrows the list as the admin types; it works without a round trip.
- [ ] One help line: only a direct retail-point assignment lets someone record in the Field App;
      an assignment higher up gives Admin Portal access only.
- [ ] Ticking an organisation and one beneath it is allowed.
- [ ] A deactivated organisation is not offered. On Edit, an existing assignment to one is shown
      ticked, marked "deactivated", and can be unticked.
- [ ] The invite dialog's "No email sending is wired up yet" note is removed.
- [ ] Web.Tests cover: a deactivated org is absent from Invite; an existing assignment to one is
      present on Edit.

## Notes

- Spec: `../spec.md` — user stories 12–14; "Org picker (Invite and Edit)".
- Skills: `/tdd`, then `/code-review`.
