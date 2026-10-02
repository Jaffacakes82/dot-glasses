# 03 — Upload a picture for a frame colour

**What to build:** On the Reference Data screen an admin uploads a picture for a frame colour, or
removes it. The picture goes into the private blob container and the Admin Portal serves it. The
web address box is gone.

**Blocked by:** 01

**Status:** resolved

**Model:** Opus 5.5 — a file upload and an anonymous endpoint that reads from storage.

**Seam:** `DotGlasses.Web.Tests` over HTTP, with a hand-written in-memory storage fake.

## Acceptance criteria

- [x] The create and edit forms for both frame colour lists take a file in place of the address
      box. The edit form shows the current picture.
- [x] Accepted: PNG, JPEG or WebP, up to 1 MB. The check reads the content type and the file's
      signature, not the extension. No resizing.
- [x] A refused upload shows a message in Spec D's voice naming the accepted types or the size
      limit.
- [x] A new Application interface for picture storage, implemented in Infrastructure over the
      `reference-data-images` container. `Infrastructure` is still referenced only from
      `Program.cs`.
- [x] Each upload is stored under a new generated name. Replacing or removing a picture deletes
      the previous blob.
- [x] "Remove picture" clears the item's picture; the item then shows the placeholder.
- [x] An anonymous GET endpoint streams a picture by its generated name with a long cache
      lifetime. It accepts only names matching the generated pattern and returns 404 otherwise.
- [x] The container has no public access.
- [x] The six existing shop addresses still display. No address can be typed for a new picture.
- [x] The storage service is registered so that local `dotnet run` uses the emulator and the test
      host uses the fake.
- [x] Web.Tests cover: an accepted upload stored and served back byte for byte; an oversized file
      refused; a non-picture file with a picture extension refused; remove clears it; the endpoint
      returns 404 for a non-matching name.

## Notes

- Spec: `../spec.md` — user stories 7–9, 11; "Upload", "Serving".
- AppHost already declares the container and references it from `Web`. If AppHost changes,
  regenerate `/infra` with `azd infra gen --force`; never hand-edit it.
- The Reference Data screen is a write to reference data: don't read the memoised snapshot in the
  same request (CLAUDE.md).
- Skills: `/tdd`, then `/code-review`, then `/security-review`.

## Comments

**2026-10-01 — built.** `IReferenceDataPictureStore` (Application), `BlobReferenceDataPictureStore`
(Infrastructure, over the container AppHost already references from Web),
`ReferenceDataPicturesController` (anonymous GET, generated names only, `nosniff`, a year's cache),
and file fields on the Reference Data forms. The item's `ImageUrl` holds the path
`/reference-data/pictures/<name>` rather than a full address, so the same row works on every host;
the Field App resolves it against its API address.

- The type comes from the file's signature (`ReferenceDataPictures.Detect`); the file name and the
  browser's content type are ignored. SVG and GIF are refused.
- With no storage connection string (a bare `dotnet run` outside AppHost, design-time tooling) an
  upload is refused with a message; tests swap in an in-memory store.
- The upload actions cap the request at 8 MB so a slightly-too-big picture still gets the 1 MB
  message rather than a bare "request too large".

**Found in the browser:** the picture endpoint has to send `Vary: Origin` on every response. The
Field App loads a picture in an `<img>` (no Origin) and then fetches it to keep an offline copy;
without the header the browser answered the fetch from its cache with no CORS header and the copy
was never made.

Checked against the local storage emulator (2026-10-01): upload, serve back byte for byte, replace
(old blob gone, 404), through the real `BlobReferenceDataPictureStore`. A test colour "Yellow
(upload check)" was created in the local dev database for this and retired afterwards.

No `/security-review` skill is installed in this session; the points it would look at are covered
above (anonymous endpoint limited to generated names, signature check, size cap, private
container, DGI-admin-only upload with antiforgery).
