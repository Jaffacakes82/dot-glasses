# 03 — Upload a picture for a frame colour

**What to build:** On the Reference Data screen an admin uploads a picture for a frame colour, or
removes it. The picture goes into the private blob container and the Admin Portal serves it. The
web address box is gone.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Opus 5.5 — a file upload and an anonymous endpoint that reads from storage.

**Seam:** `DotGlasses.Web.Tests` over HTTP, with a hand-written in-memory storage fake.

## Acceptance criteria

- [ ] The create and edit forms for both frame colour lists take a file in place of the address
      box. The edit form shows the current picture.
- [ ] Accepted: PNG, JPEG or WebP, up to 1 MB. The check reads the content type and the file's
      signature, not the extension. No resizing.
- [ ] A refused upload shows a message in Spec D's voice naming the accepted types or the size
      limit.
- [ ] A new Application interface for picture storage, implemented in Infrastructure over the
      `reference-data-images` container. `Infrastructure` is still referenced only from
      `Program.cs`.
- [ ] Each upload is stored under a new generated name. Replacing or removing a picture deletes
      the previous blob.
- [ ] "Remove picture" clears the item's picture; the item then shows the placeholder.
- [ ] An anonymous GET endpoint streams a picture by its generated name with a long cache
      lifetime. It accepts only names matching the generated pattern and returns 404 otherwise.
- [ ] The container has no public access.
- [ ] The six existing shop addresses still display. No address can be typed for a new picture.
- [ ] The storage service is registered so that local `dotnet run` uses the emulator and the test
      host uses the fake.
- [ ] Web.Tests cover: an accepted upload stored and served back byte for byte; an oversized file
      refused; a non-picture file with a picture extension refused; remove clears it; the endpoint
      returns 404 for a non-matching name.

## Notes

- Spec: `../spec.md` — user stories 7–9, 11; "Upload", "Serving".
- AppHost already declares the container and references it from `Web`. If AppHost changes,
  regenerate `/infra` with `azd infra gen --force`; never hand-edit it.
- The Reference Data screen is a write to reference data: don't read the memoised snapshot in the
  same request (CLAUDE.md).
- Skills: `/tdd`, then `/code-review`, then `/security-review`.
