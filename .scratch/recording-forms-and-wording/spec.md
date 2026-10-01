# Spec D — Recording forms and wording

Status: ready-for-agent
Source: wayfinder map `.scratch/ceo-feedback-2026-09-27/` — tickets 13 (question order and optional
fields), 14 ("customer aware of price") and 22 (plain, friendly prompts and error messages).
Vocabulary: `CONTEXT.md` — **Referred or treated**.

## Problem Statement

The Field App's Test, Lead and Sale forms ask "Referred or treated" near the top, before the
technician has finished the consultation, and ask Occupation in a different place on each form. The
referral location is required even when the technician doesn't know it.

Pressing Save on a Lead or a Sale brings up "Has the customer been told the price for this order?".
Answering "Not yet" returns to the form without saving, and the answer is stored nowhere. On a Sale
the question makes no sense: the customer has paid.

Most error messages were written for developers: "ReferralReasonRefId is required when
ReferredOrTreated is true." The sign-in pages say "username" when the sign-in name is an email
address, and switching location says "check your connection" for every kind of failure. The Admin
Portal's own forms use the framework's stock wording.

## Solution

**One question order.** Every form opens Age, Gender, Occupation, and ends with "Referred or
treated". On a Test that block comes just before the contact-details question. A Test continued
into a Lead offers its referral answers as the Lead form's starting values. The Admin Portal's Lead
conversion form follows the same order.

**Referral location is optional.** Nothing else changes from required to optional.

**Price awareness is a Lead question.** The step after Save is removed from both forms. The Lead
form asks "Has the customer been told the price?" as a required Yes or No, and stores it. A Sale
doesn't ask.

**One voice for every error message.** Each says what to do, in the words on the screen. Failures
a person can't cause from the form share one plain message per group. "Username" becomes "email",
and the location-switch message tells being offline apart from a real failure.

## User Stories

1. As a technician, I want Age, Gender and Occupation to open every form, so that the questions about the person are always in the same place.
2. As a technician, I want "Referred or treated" at the end of each form, so that I answer it once the consultation is done.
3. As a technician recording a Test, I want the referral answered before "Did the customer share contact details?", so that it is saved whichever way I finish.
4. As a technician continuing a Test into a Lead, I want my referral answers already filled in on the Lead form, so that I'm not asked the same thing twice.
5. As a technician, I want to leave the referral location blank when I don't know it, so that I can still save.
6. As an admin converting a Lead on the Admin Portal, I want the same question order and the same optional location, so that both ways of recording a Sale agree.
7. As a technician recording a Sale, I want Save to save straight away, so that I'm not asked about price for a customer who has paid.
8. As a technician recording a Lead, I want "Has the customer been told the price?" as a Yes or No on the form, and the Lead to save whichever I pick, so that the answer is a note and not a gate.
9. As someone following up a Lead, I want that answer recorded, so that I know whether price has been discussed.
10. As a technician, I want an error to tell me what to do in the words I see on the form, so that I can fix it without help.
11. As a technician looking at Failed records, I want a record rejected for a reason I couldn't have caused to still say what to do, so that I'm not shown internal field names.
12. As a user, I want the sign-in page to ask for my email, so that I know what to type.
13. As a technician, I want switching location to tell me when I'm offline and when it simply failed, so that I know whether to find a connection or try again.
14. As an admin, I want the Admin Portal's form errors in the same plain voice, so that both apps read alike.

## Implementation Decisions

**Question order (Field App `ConsultationForm.razor`)**
- **Test:** Age, Gender, Occupation, Outcome; for "Needs glasses" the lens and coating
  preference; Referred or treated; then, for "Needs glasses", the contact-details question.
- **Lead:** Age, Gender, Occupation, Full name, Phone, Consent, Reason not purchased, Has the
  customer been told the price?, lens, coating preference, Referred or treated.
- **Sale:** Age, Gender, Occupation, Full name, Phone, Consent, lens, coatings, order from DOT
  Glasses, frame colour, hard case, Referred or treated.
- Occupation is rendered once for all three forms; it stays optional.
- "Continue as Lead" passes the Test's referral answers along with age and gender. They are a
  seed, not an override: the Lead form loads them into its controls and the technician can change
  them. Each record stores its own answer.

**Referral location**
- `ConsultationRules.Referral` stops failing a blank location when the customer is referred and
  not treated in the facility. It still fails a non-blank location when "Treated in facility" is
  ticked, and the reason stays required.
- The Field App labels the field "(optional)". The same rule body serves Test, Lead and Sale.

**Price awareness**
- `ConsultationForm.razor` loses `_showPriceConfirm` and the confirmation step on both forms. The
  Sale's existing-Lead match prompt is then the only step between Save and saving.
- `CreateLeadRequest`, `LeadDto` and `Lead` gain a nullable Yes/No for "customer told the price".
  It is nullable because Leads recorded earlier have no answer.
- A rule in `ConsultationRules.Check(CreateLeadRequest, …)` requires an answer, keyed on the
  request's property name like every other rule. Either answer passes.
- It does not carry into a Sale: `SaleAssembly.Seed` ignores it and `CreateSaleRequest` has no
  such field.
- A Lead queued before this ships has no answer and is rejected at sync; it lands on Failed
  records and is fixed from there. The product isn't live, so this is accepted.

**Admin Portal's Lead conversion form**
- The "Referred or treated" block moves to the end, after hard case. Its location follows the
  relaxed rule.

**Wording**
- The voice: say what to do in a short sentence; use the words on the screen, never a field's
  internal name; no "must", "invalid" or "required when"; no "please"; no blame; limits stated
  plainly; British spelling, sentence case, a full stop.
- Every `RuleFailure` message in `ConsultationRules` is rewritten to it. **Failure keys do not
  change**: they are request property names and `FormErrors`, `ValidationProblemDetails` and
  `LeadConversionController`'s remap all key off them (CLAUDE.md, ADR-0002).
- The scalar messages stop reproducing FluentValidation's generated copy. CLAUDE.md's "reproduced
  verbatim" sentence is revised in the same change.
- Failures that can't be caused from the form (fields that must be empty for the other lens range,
  a lens type that doesn't match the chosen lenses, and the like) share one plain message per
  group, for example "This record's lens details don't match its lens range. Open it and choose
  the lens again."
- The nine Admin Portal validators get messages in the same voice (`WithMessage`), and so do the
  `DomainRuleViolationException` messages that still read as developer copy.
- Both sign-in pages label the field "Email" and say "Email or password is incorrect."
- Location switching distinguishes offline ("You're offline. Connect to switch location.") from
  failure ("Couldn't switch location. Try again.").
- The copy is written during the build. English only.

## Testing Decisions

- **`Application.Tests`** holds the rule tests, with no new dependencies. Update every test that
  pins a message; add cases for: a blank referral location accepted; a location with "Treated in
  facility" still refused; a Lead with no price answer refused, with Yes accepted and with No
  accepted.
- **A guard test on the voice.** One test walks every failure `ConsultationRules` can produce for
  a set of bad requests and asserts no message contains a request property name. This is what
  keeps developer copy from creeping back.
- **`DotGlasses.Web.Tests`** over HTTP: the three create endpoints return the new messages against
  the unchanged keys; a Lead without the price answer is a 400 keyed on its property; the Admin
  Portal's conversion form renders the referral block last and accepts a blank location; sign-in
  shows the new copy.
- **Field App order, the Test-to-Lead prefill, the Lead's price question and the location-switch
  messages are checked by hand.** There is no Field App test project. Each ticket records its
  checklist in its Comments.

## Out of Scope

- The "Aware of price" column on Event History's Leads tab and its CSV. It is built with the other
  Event History columns in Spec F, ticket 05, which is blocked by this spec's ticket 03.
- A review of every label in the Admin Portal.
- Translation into other languages.
- Any other field becoming optional. Phone stays required on a Lead and optional on a Sale.
- Frame colours following the "children's frame" tick (Spec E).
- Ordering a custom lens from a Lead (Spec F), which adds its own tick to the Lead form.

## Further Notes

- **Order of work with the other specs.** Specs E and F also edit `ConsultationForm.razor` and
  `ConsultationRules`. Land this spec's tickets 01–04 first; they move the most markup and set the
  voice the other specs' new messages follow.
- **`docs/functional-capabilities.md`** still describes the Test form with a "Referred" outcome
  and a required location. Ticket 06 rewrites that section from the code.
- **Messages are client-visible**, but only our two apps receive them and the product isn't live,
  so nothing is aliased.
