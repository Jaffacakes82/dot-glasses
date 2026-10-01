# 03 — Price awareness is a question on the Lead, not a step after Save

**What to build:** Save on a Sale saves straight away. The Lead form asks "Has the customer been
told the price?" as a Yes or No after "Reason not purchased"; the technician must pick one, either
answer saves, and the answer is stored on the Lead.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `Application.Tests` for the rule; `DotGlasses.Web.Tests` over HTTP for the Lead
endpoint; the form by hand.

**Don't run alongside:** 01.

## Acceptance criteria

- [ ] The confirmation step after Save is removed from the Lead and Sale forms.
- [ ] The Lead form has "Has the customer been told the price?" with Yes and No buttons, straight
      after "Reason not purchased".
- [ ] `CreateLeadRequest`, `LeadDto` and `Lead` gain a nullable Yes/No for it, with a migration.
      Existing Leads have no answer.
- [ ] A rule in `ConsultationRules.Check(CreateLeadRequest, …)` requires an answer, keyed on the
      request property's name. Yes and No both pass.
- [ ] The Field App's pre-submit check uses the same rule and shows the message under the
      buttons.
- [ ] Nothing carries into a Sale: `SaleAssembly.Seed` ignores it, and `SaleAssemblyTests` still
      passes with no new entry.
- [ ] A Lead reopened from Failed records pre-fills the answer when the queued payload has one.
- [ ] Rule tests: no answer refused; Yes accepted; No accepted. Web.Tests: a Lead with no answer
      is a 400 keyed on the property; a Lead with either answer is created and returns it.
- [ ] Manual checklist recorded in Comments: a Sale saves with no extra step; the Lead question
      appears, blocks when unanswered and saves on either answer.

## Notes

- Spec: `../spec.md` — user stories 7–9; "Price awareness".
- The "Aware of price" column on Event History is Spec F, ticket 05.
- Skills: `/tdd`, then `/code-review`.
