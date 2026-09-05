# 10 — Lens range rules move to the shared module

**What to build:** The second migration batch, and the largest. The preset and custom lens branches,
the axis and power constraints, the lens-type requirement when an eye carries two powers, and the
pupil distance rules are expressed once in the shared module. Behaviour is unchanged.

**Blocked by:** 08 — Shared rules project and reference-data snapshot.

**Status:** ready-for-agent
**Category:** refactor

- [x] Preset branch rules exist once: catalogue and lens options must be consistent with each other, and the pupil-distance bucket must be in range for the frame size
- [x] Custom branch rules exist once: both spheres required, axis whole and within range, powers within range and on the correct increment
- [x] A lens type is required exactly when an eye carries two distinct powers, with its "Other" text rule
- [x] Pupil distance is required for a Sale and optional for a Test or Lead, with the sellable range and whole-millimetre rules applied
- [x] Fields belonging to the branch not chosen must be empty
- [x] All three validators call the shared rules for this topic and keep the rest
- [x] Numeric boundaries are covered by tests at the shared module's interface
- [x] Externally observable validation behaviour is identical to before
