# 05 — Docs for frame colours and pictures

**What to build:** The repo's documents describe what tickets 01–04 built.

**Blocked by:** 01, 02, 03, 04

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** None — documents only.

## Acceptance criteria

- [x] `docs/functional-capabilities.md`: the Reference Data screen (two lists, upload, remove),
      the Sale form's frame colour section, the conversion form, and offline pictures.
- [x] `CLAUDE.md`: the `ReferenceDataItem` bullet lists both frame colour categories.
- [x] `docs/open-issues.md`: "No upload feature for reference-data images" is deleted. The
      go-live items are added: DGI enters the children's colours and re-uploads the six adult
      pictures. Ticket 04's manual checks are listed until a person has done them.
- [x] The comment above the storage declaration in `AppHost.cs` no longer says the upload is
      unbuilt.
- [x] The map's decision lines for tickets 15 and 16 say "shipped" in place of "Not yet built".

## Notes

- Spec: `../spec.md`.

## Comments

**2026-10-01 — done.** `docs/functional-capabilities.md` §4.4, §4.8 and §5.5; `CLAUDE.md`
(`ReferenceDataItem` bullet, offline cache); `docs/open-issues.md` (upload entry removed, go-live
and unverified items added); the `AppHost.cs` comment; the map's lines for tickets 15 and 16.
