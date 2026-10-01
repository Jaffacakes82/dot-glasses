# 04 — Frame pictures show offline in the Field App

**What to build:** The Field App keeps a copy of each frame colour's picture when it loads
reference data, and shows those copies when there is no connection.

**Blocked by:** 03

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** By hand in the browser; there is no Field App test project.

## Acceptance criteria

- [ ] When reference data loads successfully, the app fetches each frame colour's picture and
      stores it on the device, write-through like the cached lists.
- [ ] Offline, the swatches render from the stored copies.
- [ ] A picture that can't be fetched or stored falls back to the existing placeholder and never
      blocks the form or the reference data load.
- [ ] A picture replaced on the server is picked up on the next online load (its name changes),
      and copies no longer referenced are removed.
- [ ] Manual checklist recorded in Comments: load online, go offline, open a Sale and see the
      pictures for an adult and a children's frame; replace a picture and see it update; an old
      shop address that can't be stored shows online and falls back offline.

## Notes

- Spec: `../spec.md` — user story 10; "Offline".
- `App` may reference only `Contracts` and `Rules`.
- Skills: `/code-review`.
