# 10 — The Field App's coating choices

**What to build:** The Field App offers only the coatings both chosen lenses come in. A paired coating
is ticked and locked with the note "Comes with <trigger> on this lens". When a lens change removes a
coating that was ticked, it's unticked with a note, so nothing surprises the technician at save time.
A custom prescription offers any active coating.

**Blocked by:** 06, 09

**Status:** resolved

**Model:** Sonnet 5 — Field App markup over the Rules offered-coatings helper.

**Seam:** manual browser checklist (no Field App test project). Keep `dotnet build` green.

**Don't run alongside:** 09, 11 (consultation form and coating selector).

## Acceptance criteria

- [x] The coating multi-selector takes the offered coatings and required pairings from the Rules helper.
- [x] Paired coatings are ticked, locked, and carry "Comes with <trigger> on this lens".
- [x] A lens change that removes an offered coating unticks it and shows a note.
- [x] Coating preference (Tests and Leads) is limited to the offered coatings on a lens set.
- [x] Manual checklist, recorded in Comments when done: offered coatings follow both lenses; locked
      pairing and its note; the removal note; a Sale saves without a server rejection.

## Notes

- Spec: `../spec.md` — "The coating multi-selector", user stories 27–29, 35.
- Skills: `/implement`, then `/code-review`.

## Comments

**2026-09-28 — resolved (`dotnet build DotGlasses.sln` green; Field App behaviour unverified in a browser).**

- **What changed** (all under `src/DotGlasses.App`; no Rules/server code touched):
  - New `ReferenceData/CoatingSelection.cs` — pure device-side handling of Rules' answer:
    `Reconcile` (keep still-offered ticks, report the removed, then tick the pair of every ticked
    trigger), `WithPairedCoatings` (closure, so Clear → Anti-glare → Photochromic ticks both) and
    `LockedBy` (the ticked trigger holding a paired coating in place).
  - `CoatingMultiSelector.razor`: new `RequiredPairings` parameter. A paired coating whose trigger is
    ticked renders ticked and disabled with "Comes with <trigger> on this lens". Ticking a trigger
    ticks its paired coating(s) with it (blocked with a message if the pair conflicts with another
    tick). A coating excluded by a ticked one is now **disabled** with "Can't be combined with X"
    (was: only blocked with a message after the click; that message remains as the fallback).
  - `ConsultationForm.razor`: `AvailableCoatingIdsForSelectedPreset` replaced by `PairCoatings`
    (`LensSetLenses.CoatingsFor(left, right)`, null until both eyes have a lens). The Sale selector
    gets `Offered` and `RequiredPairings` from it as-is; when the pair is complete but offers
    nothing, a note says so instead of an empty gap. Coating preference (Test/Lead) is limited to
    `Offered` on a lens set (Custom / no range: every active coating). `OnLensSelectionChanged` no
    longer clears coatings: `ReconcileCoatings` keeps still-offered ticks, unticks the rest (and
    clears a no-longer-offered coating preference) and shows "Removed X, Y — not available on the
    lens you've now chosen." (`_coatingRemovedNote`, replaced on the next lens change).
    `ApplyLensRange` calls `ReconcileCoatings` too, so a converted Lead's preference-as-coating-set
    and a Failed record's coatings meet the chosen lenses on load (unoffered ones removed with the
    note, a trigger's pair ticked).
- **Server agreement, by path.** Pair offered: the selector only offers `Offered`, which B06's server
  rule checks against. Triggers with pairs: every ticked trigger's paired coating is ticked and
  locked (`RequiredPairings` is the same list the server enforces; reconciled on load/lens change,
  and on tick). Exclusions: disabled in the list; a trigger whose pair would clash is refused with a
  message. Custom: any active coating, no pairings, exclusions disabled. Coating preference:
  `Offered` only, no pairing asked (as the server). At-least-one and the lens rules are still
  caught by `ConsultationRules.Check` before queuing.
- **Deviations / judgement calls.**
  1. Reconciliation is **deferred while a lens-set pair is incomplete** (e.g. the right eye emptied
     because the left changed lens type): the ticks are kept, hidden, and reconciled the moment both
     eyes have a lens. Wiping them mid-change would leave a note about coatings the technician never
     saw removed by a lens they hadn't finished choosing.
  2. A paired coating stays ticked (unlocked) when its trigger is unticked — not auto-removed. The
     ticket only asks for lock-while-trigger-ticked, and the server accepts it.
  3. A pairing that runs both ways (A → B and B → A) never locks (they'd hold each other ticked
     forever); untick one and the server-rule message ("add A, or remove B") says what's missing.
     The admin validator doesn't forbid that shape today.
  4. Switching to Custom / no range keeps ticks (every active coating is offered), where the old
     code cleared them on any range change.
  5. Small fix in passing: a Failed **Test** now restores its coating preference on correction
     (`LoadFailedRecordAsync` never read `CoatingPreferenceRefId` for a Test, so re-saving dropped it).
  6. The removal note names removed coatings but not "which lens" — wording is deliberately generic
     because the pair, not one lens, decides what is offered.
- **Manual checklist — confirmed by hand on staging, 2026-10-01; every item works as expected.** (`Selector` =
  `CoatingMultiSelector.razor`, `Form` = `ConsultationForm.razor`.) Needs a lens set with at least
  two lenses that differ in coatings, and one lens with a pairing (e.g. Blue block → Photochromic).
  - [x] Offered coatings follow both lenses: Sale, lens set, "Same lens" ticked — the coating list
        shows only that lens's coatings; untick, choose a different right lens — only the coatings
        both come in remain (`Form.PairCoatings` → `LensSetLenses.CoatingsFor(...).Offered` →
        Selector `RestrictToIds`). A trigger coating whose pair one lens lacks isn't listed. Nothing
        is listed (and a note appears) until both eyes have a lens; if no coating is common, the
        "No coating can be made on both of these lenses" note shows.
  - [x] Locked pairing and its note: tick the trigger (Blue block) — its paired coating
        (Photochromic) ticks itself, its checkbox is disabled, and "Comes with Blue block on this
        lens" shows under it (`CoatingSelection.WithPairedCoatings` in Selector `ToggleAsync`;
        `CoatingSelection.LockedBy` in the render). Untick Blue block — Photochromic unlocks (stays
        ticked, can be unticked). A coating that an exclusion forbids with a ticked one is disabled
        with "Can't be combined with …" (`Selector` `FindConflict`).
  - [x] Removal note: tick a coating only the current lens has, then change a lens so it's no longer
        offered — it is unticked and "Removed <names> — not available on the lens you've now
        chosen." appears; coatings still offered stay ticked (`Form.OnLensSelectionChanged` →
        `ReconcileCoatings` → `CoatingSelection.Reconcile`). The same note appears for a Test/Lead
        coating preference cleared by a lens change. Changing the left lens to another type
        (emptying the right) keeps the ticks until the right lens is chosen (deviation 1).
  - [x] Custom prescription offers any active coating: Sale, Custom — the full active coating list,
        no locks, no pairing behaviour; exclusions still disable (`Form.ReconcileCoatings` non-lens-set
        branch; the Custom `CoatingMultiSelector` has no `RestrictToIds`/`RequiredPairings`).
  - [x] Coating preference on a lens set (Test/Lead): only the offered coatings are radios (with "No
        preference" first) once both eyes have a lens; on Custom, every active coating
        (`Form.RenderCoatingPreference`).
  - [x] Converting a Lead / correcting a Failed record: a Lead whose preference is a trigger opens the
        Sale with its pair ticked and locked; a preference the chosen lenses don't offer opens
        unticked with the removal note; a Failed Test reopens with its coating preference
        (`Form.ApplyLensRange` → `ReconcileCoatings`; `LoadFailedRecordAsync`).
  - [x] A Sale saves without a server rejection: build a Sale on a lens set with the paired trigger
        ticked and, separately, with a pair from the other lens's pairings; save (online) — no 400,
        nothing on Failed records (`ConsultationRules.Check` before queuing uses the same
        `CoatingsFor`; the server applies the same rule, B06).
- **Stale docs for B12** (not edited): `docs/functional-capabilities.md` ~796–799 (Field App coating
  list follows the left lens) — now both lenses, paired coatings locked, removal note, coating
  preference limited to the offered set; CLAUDE.md's `Offline sync`/UI sections don't cover the
  selector, but the `Rules` bullet could mention that the Field App feeds
  `LensSetLenses.CoatingsFor` into `CoatingMultiSelector`. The ADR-0001 mention in the selector's
  doc comments (auto-added global pairings) is gone — nothing else references it.
