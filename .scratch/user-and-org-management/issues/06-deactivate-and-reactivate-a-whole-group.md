# 06 — Deactivate and reactivate an organisation with everything beneath it

**What to build:** Deactivating an organisation takes every organisation beneath it, after a
confirmation with counts. Reactivating restores what went with it and nothing else. An organisation
can't be reactivated while the one directly above it is deactivated. The Deactivated orgs strip
shows each group once.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Opus 5.5 — soft-delete across a subtree, with the filter-bypass pitfalls.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

## Acceptance criteria

- [ ] Deactivate soft-deletes the organisation and all active descendants in one transaction. The
      "deactivate the child orgs first" refusal is gone.
- [ ] The group is recorded so reactivation restores exactly it: a group id on each organisation
      deactivated together, added by a migration, or the shared deactivation timestamp if that
      proves reliable. Record the choice in Comments. A descendant deactivated separately
      beforehand stays deactivated.
- [ ] Before deactivating, a confirmation states: how many organisations go with it, how many
      people lose access, and that unsent Field App records for those retail points will be
      refused.
- [ ] Reactivate is refused with "reactivate the organisation above it first" when the parent is
      deactivated.
- [ ] Assignments inside the group are untouched; they give no access while deactivated and work
      again afterwards.
- [ ] The strip lists each group's top organisation with "and N beneath it" and one Reactivate
      button. An organisation deactivated on its own is listed as today. The strip is absent when
      there is nothing to list.
- [ ] Reactivate leaves the page in a sound state (the reported glitch): retest it by hand and
      note the result in this ticket's Comments.
- [ ] Web.Tests cover: a parent with children deactivates the whole group; reactivating restores
      the group and leaves a separately deactivated child off; reactivating under a deactivated
      parent is refused with its message.

## Notes

- Spec: `../spec.md` — user stories 22–25; "Organisations screen".
- CLAUDE.md pitfall: un-hiding a soft-deleted row needs `IgnoreQueryFilters()` in both the
  authorization lookup and the mutation.
- Paths are never reused or changed; nothing is re-parented.
- Skills: `/tdd`, then `/code-review`.
