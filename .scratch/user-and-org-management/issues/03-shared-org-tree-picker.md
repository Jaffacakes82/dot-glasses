# 03 — One org tree picker for Invite and Edit

**What to build:** The Invite dialog and the Edit user page share one organisation picker: an
indented tree showing each organisation's level, a filter box, and a line explaining what an
assignment grants. The misaligned checkbox and the stale email note go.

**Blocked by:** 01, 05

**Status:** resolved

**Model:** Sonnet 5.5 — a shared partial and a small filter script.

**Seam:** `DotGlasses.Web.Tests` over HTTP for what is offered; the filter by hand.

## Acceptance criteria

- [x] One partial used by Invite and Edit, labelled "Organisations", with the checkbox beside its
      label.
- [x] Organisations are indented by depth and show their level label (from ticket 05).
- [x] A filter box narrows the list as the admin types; it works without a round trip.
- [x] One help line: only a direct retail-point assignment lets someone record in the Field App;
      an assignment higher up gives Admin Portal access only.
- [x] Ticking an organisation and one beneath it is allowed.
- [x] A deactivated organisation is not offered. On Edit, an existing assignment to one is shown
      ticked, marked "deactivated", and can be unticked.
- [x] The invite dialog's "No email sending is wired up yet" note is removed.
- [x] Web.Tests cover: a deactivated org is absent from Invite; an existing assignment to one is
      present on Edit.

## Notes

- Spec: `../spec.md` — user stories 12–14; "Org picker (Invite and Edit)".
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** `Views/Shared/_OrgPicker.cshtml` with `OrgPickerViewModel.Build`, used by
the Invite dialog and the Edit page. The filter is `data-dg-filter` in `site.js`; a ticked row is
never hidden by it. The misaligned checkbox came from `.dg-auth-field input { width: 100% }`; the
picker's rows use the new `.dg-check-row`.

Checked in a local browser (2026-10-01): the tree is indented by depth with each level as words;
typing in the filter narrows the list and clearing it restores it; the checkbox sits beside its
label.
