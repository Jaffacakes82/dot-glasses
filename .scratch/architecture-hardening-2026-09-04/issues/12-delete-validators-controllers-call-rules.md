# 12 — Delete the three validators; controllers call the shared module

**What to build:** The contract phase. With every rule now living in the shared module, the three
consultation request validators have nothing left of their own and are deleted outright rather than
thinned into delegating shells. Controllers call the shared module directly and turn its failures
into the same validation response clients already receive. Per ADR-0002 the remaining validators keep
using FluentValidation — several still need async rules, so that standing decision is untouched.

**Blocked by:** 09, 10 and 11 — the three rule-migration batches.

**Status:** ready-for-agent
**Category:** refactor

- [x] The three consultation request validators no longer exist
- [x] Each create endpoint calls the shared module and returns a validation response on failure
- [x] Failure keys and messages reaching a client are unchanged
- [x] The reference-data snapshot is loaded once per request and passed in
- [x] Validating one Sale costs a single reference-data read rather than one per referenced field
- [x] FluentValidation remains in use for the other validators, unchanged
- [x] An API-level test confirms a rejected create returns the expected field keys
