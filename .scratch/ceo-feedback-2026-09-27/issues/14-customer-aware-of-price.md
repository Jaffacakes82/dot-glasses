# 14 — "Customer aware of price": leads only, and never blocking

Type: grilling
Status: resolved
Blocked by: None

## Question

The price-awareness question appears when recording a sale. It should only be asked for leads, and
whatever the answer the lead still saves — it's a note. What does it record and where?

## Context

- Feedback doc: "When recording a sale it asks if customer is aware of price. Should only ask for
  leads. And regardless of the answer it should still submit/save the lead".

## Answer

Decided by grilling, 2026-10-01.

**Sale**

- The price-awareness step is removed. Save on a Sale saves straight away.

**Lead**

- The step after Save is removed here too. In its place the Lead form has an ordinary question,
  "Has the customer been told the price?", with Yes and No buttons.
- It sits straight after "Reason not purchased". With ticket 13's order the Lead form runs: Age,
  Gender, Occupation, Full name, Phone, Consent, Reason not purchased, **Has the customer been
  told the price?**, lens, coating preference, Referred or treated.
- The technician must pick one before the Lead saves. Either answer saves.
- The answer is stored on the Lead as a Yes or No. It is a rule in `ConsultationRules` like the
  Lead's other required answers.
- It does not carry across when the Lead converts; a Sale has no such question.

**Where it shows**

- Event History's Leads tab gets an "Aware of price" column, and so does its CSV export. A Lead
  recorded before this change shows "—".
- Nothing on the dashboard.

**Note for the spec**

- The Test form never had the step and is unchanged.
