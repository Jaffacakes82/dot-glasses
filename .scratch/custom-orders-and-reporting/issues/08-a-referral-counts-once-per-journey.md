# 08 — A referral counts once per customer journey

**What to build:** The dashboard's "Referrals logged" counts a referral once when the same customer
journey recorded it more than once: a Test and the Lead it became, or a Lead and the Sale it became.

**Blocked by:** None

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** `Application.Tests` for the chain logic; `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 06, 07.

## Acceptance criteria

- [x] Records are grouped into chains through `Test.ConvertedToLeadId` and `Lead.SaleId`. A chain
      with any record marked "Referred or treated" counts once.
- [x] A record with no link counts on its own.
- [x] A chain is counted when any of its referred records falls in the date range, and counted
      once even when several do.
- [x] Training organisations stay excluded.
- [x] The tile's link to Event History's Referrals tab still works. If that tab lists records and
      the tile counts journeys, a line on the tab says so.
- [x] Tests: a referred Test continued into a referred Lead counts one; with the Sale it became
      also referred, still one; two unlinked referred records count two; a chain with only its
      Lead referred counts one.

## Notes

- Spec: `../spec.md` — user story 18; "Dashboard".
- Spec D's Test-to-Lead prefill makes the double count more common, so this should land no later
  than that.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-02 — built.** `DashboardCalculator.ReferredJourneys` names each record's journey by its earliest step (the
Test, else the Lead, else the Sale) and counts distinct journeys with a referred record in range. Training organisations
stay out. The Referrals tab's opening note now says it lists records where the tile counts customers.

Tests: `DashboardCalculatorTests` (the four cases in the criteria, plus the date-range ones), `DashboardScreenTests`.
