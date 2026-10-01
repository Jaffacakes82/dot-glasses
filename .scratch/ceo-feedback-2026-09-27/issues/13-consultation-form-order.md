# 13 — Question order and optional fields on the consultation forms

Type: grilling
Status: resolved
Blocked by: None

## Question

Settle the question order on all event-recording forms and which fields become optional.

## Context

- Feedback doc: "referred or treated" at the end of every event page; occupation earlier, with age
  and gender; referral location optional for now.
- `ConsultationRules` holds these rules for both apps; the Admin Portal's lead conversion form is a
  second place the order shows.

## Answer

Decided by grilling, 2026-10-01.

**Question order on the Field App**

- Every form opens **Age, Gender, Occupation**. Occupation stays optional.
- **"Referred or treated"** and its follow-ups (reason, "Treated in facility", referral location)
  move to the end.
- **Test:** Age, Gender, Occupation, Outcome; for "Needs glasses" the lens and coating
  preference; then Referred or treated; then, for "Needs glasses", "Did the customer share
  contact details?" last. The referral is answered before the Test is saved on either path.
- **Lead:** Age, Gender, Occupation, Full name, Phone, Consent, Reason not purchased, lens,
  coating preference, Referred or treated.
- **Sale:** Age, Gender, Occupation, Full name, Phone, Consent, lens, coatings, order from DOT
  Glasses, frame colour, hard case, Referred or treated.

**Test continuing into a Lead**

- "Continue as Lead" carries the Test's referral answers into the Lead form as starting values,
  alongside Age and Gender. The technician can change them, and each record keeps its own answer.

**Optional fields**

- **Referral location** becomes optional when the customer is referred elsewhere. The form labels
  it "(optional)" and `ConsultationRules` stops refusing a blank one, for Test, Lead and Sale.
- The reason for referral stays required, and the location must still be empty when "Treated in
  facility" is ticked.
- Nothing else changes: Phone stays required on a Lead and optional on a Sale; Age and Occupation
  stay optional.

**Admin Portal's Lead conversion form**

- "Referred or treated" moves to the end there too, after hard case, and its referral location is
  optional in the same way.

**Note for the spec**

- Removing the "location is required" rule removes a message clients receive today. Treat it as
  the client-visible change it is.
