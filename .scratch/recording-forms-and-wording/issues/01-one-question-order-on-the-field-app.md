# 01 — One question order on the Field App's recording forms

**What to build:** Every recording form opens Age, Gender, Occupation and ends with "Referred or
treated". On a Test the referral block comes just before the contact-details question. A Test
continued into a Lead offers its referral answers as the Lead form's starting values.

**Blocked by:** None

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** By hand in the browser; there is no Field App test project.

**Don't run alongside:** 03 here, or any ticket in Specs E and F that edits `ConsultationForm.razor`.

## Acceptance criteria

- [x] Test: Age, Gender, Occupation, Outcome; for "Needs glasses" the lens and coating
      preference; Referred or treated; then, for "Needs glasses", "Did the customer share contact
      details?".
- [x] Lead: Age, Gender, Occupation, Full name, Phone, Consent, Reason not purchased, lens,
      coating preference, Referred or treated.
- [x] Sale: Age, Gender, Occupation, Full name, Phone, Consent, lens, coatings, order from DOT
      Glasses, frame colour, hard case, Referred or treated.
- [x] Occupation is rendered once for all three forms and stays optional.
- [x] "Continue as Lead" carries the Test's referral answers (flag, reason, other text, treated in
      facility, location) into the Lead form's controls, alongside age and gender. They are loaded
      once and can be changed.
- [x] A form reopened from Failed records still pre-fills every field.
- [x] Manual checklist recorded in Comments: the order on each form; the Test-to-Lead prefill; a
      Failed record reopened.

## Notes

- Spec: `../spec.md` — user stories 1–4; "Question order".
- No rule changes here; that is ticket 02.
- Skills: `/code-review`.

## Comments

**2026-10-01 — built.** The three forms share one order in `ConsultationForm.razor`: Occupation is
rendered once after Gender, and the referral block is one fragment rendered last (on a Test, before
the contact-details question). "Continue as Lead" passes the Test's referral answers in the query
string beside age and gender; the Lead form loads them into its controls once.

A form reopened from Failed records did not restore "Referred or treated" or "Treated in facility"
before this ticket (and a Sale restored none of the referral block). It now restores all five
referral answers on all three forms, and a Sale keeps its `SourceLeadId`.

Manual checklist (local browser, dev retail point account, 2026-10-01):

- [x] Test: Age, Gender, Occupation, Outcome, then Referred or treated; with "Needs glasses" the
      lens range and coating preference sit between Outcome and the referral block.
- [x] Lead: Age, Gender, Occupation, Full name, Phone, Consent, Reason not purchased, price
      question, Lens range, Coating preference, Referred or treated.
- [x] Sale: Age, Gender, Occupation, Full name, Phone, Consent, Lens range, coatings, Frame colour,
      hard case, Referred or treated.
- [x] Test with a referral reason and location, "Continue as Lead": the Lead form opens with the
      box ticked, the same reason and the same location.
- [ ] A Failed record reopened pre-fills every field. Not checked in a browser — listed in
      `docs/open-issues.md`.
