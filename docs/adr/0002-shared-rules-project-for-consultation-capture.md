# Consultation capture rules live in a shared `DotGlasses.Rules` project

The rules governing what a valid `Test`/`Lead`/`Sale` looks like were written out four times: once
per entity in `DotGlasses.Web.Validation` (≈990 lines, with two helper methods byte-identical
across all three validators after normalising the DTO type name), and again in
`ConsultationForm.razor`'s own ~212 lines of pre-submit checks. Nothing but hand-synchronisation
held the copies together, and the client's copy was already a strict subset of the server's —
length caps, power-step increments and several conditional rules existed server-side only.

Client-side pre-submit validation is a **hard requirement**, not a convenience: a technician
working offline for hours must not discover at sync time that a batch of records is bad. That
forces the rules to be genuinely shared rather than server-only, which in turn forces a home for
them that `DotGlasses.App` is allowed to reference.

**Decision.** A new `DotGlasses.Rules` project holds pure rule functions plus a reference-data
snapshot type. `App` may reference `Contracts` and `Rules`, and nothing else. Rules are composed
internally from per-topic functions (referral, lens range, coating set, frame, hard case) but
exposed with a DTO-shaped surface — `Check(CreateSaleRequest, snapshot)` and its two siblings —
because failure keys are request-DTO property names, and three separate things already depend on
that: `FormErrors`, ASP.NET's `ValidationProblemDetails`, and `LeadConversionController`'s
`Form.{PropertyName}` remap. The three FluentValidation validators are deleted outright rather
than thinned; controllers call the module directly. FluentValidation stays for the other seven
validators, which still use async rules — so the standing decision not to use
`AddFluentValidationAutoValidation()` is unaffected.

The snapshot carries reference-data items with their `IsActive`/`IsOtherOption` state, preset
catalogues, and the coating pairing/exclusion rules. Two adapters fill it: the server loads
everything from the database, the Field App fills it from its existing IndexedDB-cached API
response, which returns active items only. The rule "present **and** active" is correct under both
fillings — a retired item is absent from the client's copy and present-but-inactive in the
server's, and both reject it. Because the server's copy carries retired items, it also becomes the
single label resolver, replacing seven separate `Guid`→label implementations that had four
different fallback strings between them.

**Considered and rejected:** putting the rules in `Contracts` alongside the DTOs they validate. It
needs no new project and breaks no reference rule as written — but `Contracts` is deliberately a
pure wire-shape layer, and quietly stretching that is worse than amending it openly. Also
rejected: leaving the rules server-only and deleting the client's copy, which is cheaper than
either alternative but forfeits offline pre-submit validation entirely — the one thing the whole
outbox design exists to protect.

**Consequences.** The snapshot is loaded once per request server-side (one query in place of the
7 + 3n + n(n−1)/2 sequential lookups a preset-range Sale currently costs — 14 at two coatings, 19
at three). Caching it across requests is deliberately deferred: `Web` runs on Container Apps and
can scale to multiple replicas, so an in-memory cache is per-replica and an admin's reference-data
edit would be live on one replica and stale on the others until invalidation crosses them. No API
change is required.
