# 05 — Event History shows the lens and marks training rows

**What to build:** Event History's Sales and Leads tabs show the lens range and each eye's lens
power. Rows from training organisations carry a "Training" badge. The Leads tab shows whether the
customer was told the price. The CSV exports carry each value in its own column.

**Blocked by:** None in this spec. The "Aware of price" column alone waits on Spec D ticket 03
(`../../recording-forms-and-wording/issues/03-price-awareness-is-a-lead-question.md`); see Notes.

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

## Acceptance criteria

- [ ] Sales and Leads tabs gain Lens range, Lens power LE and Lens power RE, left eye first.
- [ ] Lens range is the lens set's name, or "Custom". The Sales tab's "· Custom" badge suffix is
      removed.
- [ ] Each eye uses `LensPowerValues.FormatLensPower`. No formatting is restated in the view.
- [ ] A record with no lens power shows "—" in the power columns.
- [ ] A "Training" badge beside the outlet name on every tab with an Outlet column, when the
      outlet is a training organisation or sits beneath one, resolved through `OrgTreeLookup`.
- [ ] Leads tab: an "Aware of price" column showing Yes, No, or "—" for a Lead with no answer.
- [ ] CSV for Sales and Leads: lens range; sphere, cylinder, axis and add for each eye in separate
      columns; lens type; coatings; "Training org" Yes/No; and "Aware of price" on Leads. The
      Tests and Referrals exports gain "Training org".
- [ ] The list and the export keep sharing one row shape.
- [ ] No filter for training rows is added.
- [ ] Web.Tests cover: a lens-set Sale and a Custom Sale render their range and powers; an old
      record renders "—"; a training retail point's row and a row beneath a training retailer
      carry the badge; the CSV has the separate columns.

## Notes

- Spec: `../spec.md` — user stories 9–12; "Event History".
- If Spec D ticket 03 hasn't landed, build everything else and leave the "Aware of price" column
  for a follow-up commit; say so in Comments.
- Ancestor lookups need `IUnscopedReportQueryService` (CLAUDE.md pitfall).
- Skills: `/tdd`, then `/code-review`.
