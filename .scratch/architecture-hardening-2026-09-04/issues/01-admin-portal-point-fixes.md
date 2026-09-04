# 01 — Admin portal point fixes

**What to build:** Three small, user-visible corrections in the Admin Portal, plus one piece of dead
code removed. An admin who advances a custom order that has already been advanced — by a colleague,
a double click, or a browser resubmit — sees the sentence explaining that, not an error page. An
admin converting a Lead into a Sale can record whether the customer was referred or treated, with a
referral reason and location, exactly as the field team can. The frame-coverage question disappears
from the conversion form, so it is uniformly not asked on either write path.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent
**Category:** bug

- [ ] Advancing an already-fulfilled custom order shows an inline message, not a server error page
- [ ] Advancing a custom order that is not visible to the caller fails gracefully, without surfacing a raw persistence error
- [ ] The Lead→Sale conversion form captures "referred or treated", a referral reason, referral "Other" text, treated-in-facility, and a referral location
- [ ] The referral fields on that form follow the same conditional rules as every other capture path
- [ ] A Sale created by conversion persists the referral answers given
- [ ] The frame-coverage control is removed from the conversion form; the value on the record is unchanged
- [ ] The unused unscoped widget-example query is removed from the reporting interface and its implementation
