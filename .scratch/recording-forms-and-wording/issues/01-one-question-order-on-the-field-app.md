# 01 — One question order on the Field App's recording forms

**What to build:** Every recording form opens Age, Gender, Occupation and ends with "Referred or
treated". On a Test the referral block comes just before the contact-details question. A Test
continued into a Lead offers its referral answers as the Lead form's starting values.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** By hand in the browser; there is no Field App test project.

**Don't run alongside:** 03 here, or any ticket in Specs E and F that edits `ConsultationForm.razor`.

## Acceptance criteria

- [ ] Test: Age, Gender, Occupation, Outcome; for "Needs glasses" the lens and coating
      preference; Referred or treated; then, for "Needs glasses", "Did the customer share contact
      details?".
- [ ] Lead: Age, Gender, Occupation, Full name, Phone, Consent, Reason not purchased, lens,
      coating preference, Referred or treated.
- [ ] Sale: Age, Gender, Occupation, Full name, Phone, Consent, lens, coatings, order from DOT
      Glasses, frame colour, hard case, Referred or treated.
- [ ] Occupation is rendered once for all three forms and stays optional.
- [ ] "Continue as Lead" carries the Test's referral answers (flag, reason, other text, treated in
      facility, location) into the Lead form's controls, alongside age and gender. They are loaded
      once and can be changed.
- [ ] A form reopened from Failed records still pre-fills every field.
- [ ] Manual checklist recorded in Comments: the order on each form; the Test-to-Lead prefill; a
      Failed record reopened.

## Notes

- Spec: `../spec.md` — user stories 1–4; "Question order".
- No rule changes here; that is ticket 02.
- Skills: `/code-review`.
