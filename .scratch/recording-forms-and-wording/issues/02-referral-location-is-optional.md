# 02 — Referral location is optional

**What to build:** A Test, Lead or Sale for a customer referred elsewhere saves with the referral
location left blank, on the Field App and on the Admin Portal's Lead conversion form. The Admin
Portal's form also moves "Referred or treated" to the end.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `Application.Tests` for the rule; `DotGlasses.Web.Tests` over HTTP for the endpoints and
the conversion form.

## Acceptance criteria

- [ ] `ConsultationRules.Referral` no longer fails a blank location when the customer is referred
      and not treated in the facility.
- [ ] It still fails a non-blank location when "Treated in facility" is ticked, and still requires
      the reason.
- [ ] The Field App labels the field "Referral location (optional)" and its pre-submit check
      follows the shared rule.
- [ ] On the Admin Portal's Lead conversion form the "Referred or treated" block is the last
      section, after hard case, and a blank location is accepted.
- [ ] Rule tests: blank location accepted on Test, Lead and Sale; location with "Treated in
      facility" refused; missing reason refused.
- [ ] Web.Tests: the three create endpoints accept a referral with no location; the conversion
      form renders the block last and converts with no location.
- [ ] `CONTEXT.md` already says the location is optional; no glossary change is needed.

## Notes

- Spec: `../spec.md` — user stories 5–6; "Referral location", "Admin Portal's Lead conversion
  form".
- This removes a message clients receive today. Only our two apps receive it.
- Skills: `/tdd`, then `/code-review`.
