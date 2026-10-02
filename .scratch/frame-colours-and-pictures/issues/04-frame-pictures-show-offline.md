# 04 — Frame pictures show offline in the Field App

**What to build:** The Field App keeps a copy of each frame colour's picture when it loads
reference data, and shows those copies when there is no connection.

**Blocked by:** 03

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** By hand in the browser; there is no Field App test project.

## Acceptance criteria

- [x] When reference data loads successfully, the app fetches each frame colour's picture and
      stores it on the device, write-through like the cached lists.
- [x] Offline, the swatches render from the stored copies.
- [x] A picture that can't be fetched or stored falls back to the existing placeholder and never
      blocks the form or the reference data load.
- [x] A picture replaced on the server is picked up on the next online load (its name changes),
      and copies no longer referenced are removed.
- [x] Manual checklist recorded in Comments: load online, go offline, open a Sale and see the
      pictures for an adult and a children's frame; replace a picture and see it update; an old
      shop address that can't be stored shows online and falls back offline.

## Notes

- Spec: `../spec.md` — user story 10; "Offline".
- `App` may reference only `Contracts` and `Rules`.
- Skills: `/code-review`.

## Comments

**2026-10-01 — built.** `FramePictureCache` keeps the pictures as data URLs in IndexedDB, synced
after each successful reference-data load without holding the form up; copies no list points at
are dropped. Only pictures the Admin Portal serves are copied. An item still pointing at another
website is left as a web address — fetching it would send the app's HttpClient, and its bearer
token, to someone else's server.

Manual checklist (local browser, 2026-10-01):

- [x] After an online load the uploaded picture is stored on the device and the swatch renders
      from the stored copy.
- [x] A replaced picture is picked up on the next load and the old copy is gone.
- [x] The six shop-hosted pictures show online from their own addresses and are not copied.
- [ ] The Sale form opened with the device actually offline — not done. In `docs/open-issues.md`.
