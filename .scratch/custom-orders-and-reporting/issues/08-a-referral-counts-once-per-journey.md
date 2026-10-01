# 08 — A referral counts once per customer journey

**What to build:** The dashboard's "Referrals logged" counts a referral once when the same customer
journey recorded it more than once: a Test and the Lead it became, or a Lead and the Sale it became.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `Application.Tests` for the chain logic; `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 06, 07.

## Acceptance criteria

- [ ] Records are grouped into chains through `Test.ConvertedToLeadId` and `Lead.SaleId`. A chain
      with any record marked "Referred or treated" counts once.
- [ ] A record with no link counts on its own.
- [ ] A chain is counted when any of its referred records falls in the date range, and counted
      once even when several do.
- [ ] Training organisations stay excluded.
- [ ] The tile's link to Event History's Referrals tab still works. If that tab lists records and
      the tile counts journeys, a line on the tab says so.
- [ ] Tests: a referred Test continued into a referred Lead counts one; with the Sale it became
      also referred, still one; two unlinked referred records count two; a chain with only its
      Lead referred counts one.

## Notes

- Spec: `../spec.md` — user story 18; "Dashboard".
- Spec D's Test-to-Lead prefill makes the double count more common, so this should land no later
  than that.
- Skills: `/tdd`, then `/code-review`.
