# 09 — Referral, occupation, frame and hard-case rules move to the shared module

**What to build:** The first migration batch. The rules governing "referred or treated", occupation,
frame colour, hard case and the "Other" free-text requirement are expressed once in the shared
module and called by all three consultation validators, which keep their remaining rules for now.
Nothing changes for a user — this is the same behaviour with one expression instead of three.

**Blocked by:** 08 — Shared rules project and reference-data snapshot.

**Status:** ready-for-agent
**Category:** refactor

- [ ] Referral rules exist once: reason required when referred or treated, "Other" text required for an "Other" reason, location required unless treated in facility, and all referral fields empty otherwise
- [ ] Occupation, frame colour and hard-case rules exist once, including their "Other" free-text requirements
- [ ] Hard-case colour fields must be empty when no hard case was sold
- [ ] A referenced reference-data item must exist, be active, and belong to the expected category
- [ ] All three validators call the shared rules for these topics and keep the rest
- [ ] Failure keys are unchanged request-property names
- [ ] Every rule in this batch is covered by tests at the shared module's interface, with a snapshot supplied as a literal value
- [ ] Externally observable validation behaviour is identical to before
