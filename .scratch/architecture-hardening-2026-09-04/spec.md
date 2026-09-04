# Architecture hardening before MVP handover — 2026-09-04

Source: architecture review of `main` at `c4df99c`, then a full grilling session settling every
open decision. Decisions are recorded in ADRs 0002–0004, the ADR-0001 scope-correction note, and
the new `CONTEXT.md` entries — this spec is the work, not the reasoning.

Status: ready-for-agent
Category: refactor

**Published as tickets (2026-09-04), via `/to-tickets`.** The nine changes below are broken into 17
tickets under `issues/`, numbered in dependency order with an explicit `Blocked by` line each. The
rules migration is sequenced as expand–contract (08 expands, 09–11 migrate, 12 contracts) rather than
as tracer bullets, because no single vertical slice of a 990-line rule move can land green. Tickets
01, 02, 03, 05 and 08 have no blockers and can start immediately.

## Problem Statement

The system works, but three groups of people are being let down by how it is put together.

**A technician recording consultations in the field** relies on the app catching mistakes before a
record is queued — that is the entire promise of working offline. It does not reliably do that.
The checks the app runs are a hand-maintained subset of the checks the server runs, so a record can
pass on the device and be refused hours later at sync time, landing on the failed-records screen
instead of being caught while the customer was still standing there. Some of those refusals arrive
with the explanation attached to a field the form has no control for, so the message appears in a
summary band rather than next to the thing that is wrong.

**An admin in the portal** hits two dead ends. Converting a Lead into a Sale offers no way to record
that the customer was referred or treated, even though the same question is asked at every other
stage of the journey — so that information is simply lost on the admin path. And advancing a custom
order that someone else has already advanced produces a raw error page rather than the sentence the
system had ready to explain what happened. The same admin sees a customer's Retailer reported one
way on the Dashboard and a different way on the Custom Orders screen, because the two screens do not
agree on what a Retailer is.

**Whoever maintains this next** faces the underlying cause. A single rule such as "a referral reason
is required when the customer was referred or treated" is written out five times — once per entity
in three server-side validators, again in the Field App form, and partially again in the admin
conversion form. Roughly 990 lines of validator exist, of which two helper methods are byte-identical
across all three and about 100 of one validator's 133 substantive lines reappear verbatim in another.
Nothing holds the copies in step but care. Meanwhile the test suite exercises the `WidgetExample`
reference pattern and two cross-cutting infrastructure concerns, and nothing else — so a green build
says nothing about whether a Sale can be recorded, and there is no net under any change to the rules.

## Solution

Give each of these responsibilities one home.

The consultation rules move into a single shared module that both the Field App and the server call,
so the device and the server cannot disagree — a rule changes in one place or not at all. The
reference-data facts those rules need arrive as one snapshot loaded once, rather than as a dozen
separate database questions asked mid-validation. Domain rejections travel as their own kind of
failure, handled once, so no screen can forget to catch one. The org hierarchy gets a type that
knows which direction it is being asked about, and a single definition of Retailer that every screen
uses.

Before any of that moves, the behaviour that already exists gets pinned down by tests — the
conversion paths and the access-control policies first, because they are testable today and because
they are what the refactor could silently break.

The technician's experience changes in one visible way: mistakes are caught on the device, in the
same words the server would have used, against the right field. The admin's changes in three: the
referral question appears on the conversion form, an already-advanced order explains itself, and
Retailer means the same thing everywhere.

## User Stories

**Field technician**

1. As a technician working offline, I want the app to catch every mistake the server would catch, so that I never discover at sync time that a morning's records were refused.
2. As a technician, I want a refused record to explain itself in the same words the app would have used, so that I am not learning two different vocabularies for the same rule.
3. As a technician, I want a validation message to appear next to the field it is about, so that I can fix it without hunting.
4. As a technician recording a Sale, I want the coating rules enforced identically on the device and on the server, so that a combination the app let me pick is never refused later.
5. As a technician, I want free-text length limits enforced on the device, so that a long occupation or referral note is not silently accepted and then rejected.
6. As a technician entering a custom prescription, I want out-of-range or fractional values caught as I enter them, so that the record does not queue in a state the server will refuse.
7. As a technician, I want a pupil distance outside the sellable range flagged immediately, so that I can re-measure while the customer is present.
8. As a technician, I want the app to tell me when a chosen lens has no coatings configured, so that I understand why I cannot complete the Sale.
9. As a technician, I want my queued records to sync as quickly as possible, so that a batch built up over a day drains before I lose signal again.
10. As a technician, I want a record that genuinely cannot be saved to reach the failed-records screen with an actionable reason, so that I can correct and resend it rather than lose it.

**Admin portal user**

11. As an admin converting a Lead into a Sale, I want to record whether the customer was referred or treated, so that the referral is captured on the admin path as it is everywhere else.
12. As an admin, I want to give a referral reason and location on the conversion form, so that a referral recorded this way carries the same detail as one recorded in the field.
13. As an admin advancing a custom order, I want a clear message when it has already been advanced, so that I understand what happened instead of seeing an error page.
14. As an admin, I want a second click or a colleague's simultaneous action to be handled gracefully, so that a shared fulfilment queue is safe to work in.
15. As an admin, I want an action refused by a business rule to explain itself inline, so that every refusal behaves the same way regardless of which screen I am on.
16. As an admin, I want Retailer to mean the same thing on the Dashboard and on Custom Orders, so that I can compare what the two screens tell me.
17. As an admin below the top of the org hierarchy, I want outlet and country names resolved correctly, so that reports do not show "Unknown" for places I can plainly see.
18. As an admin, I want a retail point with no reseller above it to be reported honestly as having no Retailer, so that a country is never presented as though it were one.
19. As an admin, I want Event History's CSV export to contain exactly the rows the screen is showing, so that an export can never quietly diverge from the list I filtered.
20. As an admin, I want a historical record to still show a readable label after the underlying option is retired, so that old consultations remain interpretable.
21. As an admin, I want to never be asked a question the field team is not asked, so that the two ways of recording a Sale capture the same thing.
22. As an admin, I want inviting a user to either fully succeed or fully fail, so that I never end up with an account that has no role or no location.

**Maintainer**

23. As a maintainer, I want each consultation rule expressed once, so that changing it cannot leave a stale copy behind.
24. As a maintainer, I want the Field App and the server to share the same rule code, so that client and server drift becomes impossible rather than merely unlikely.
25. As a maintainer, I want the consultation rules testable without a database or an HTTP request, so that covering a rule permutation costs seconds.
26. As a maintainer, I want the conversion paths covered by tests before the rules move, so that the largest refactor in this programme happens under a net.
27. As a maintainer, I want the access-control policies covered by tests, so that a scoping regression fails a build rather than exposing data quietly.
28. As a maintainer, I want conversion atomicity actually asserted, so that the guarantee currently claimed only in comments is verified.
29. As a maintainer, I want a business-rule rejection to be distinguishable from a missing row, so that the two can never be handled as though they were the same thing.
30. As a maintainer, I want rejection handling to live in one place, so that a new screen cannot forget to catch one.
31. As a maintainer, I want to know which direction a hierarchy comparison is asking about, so that the ancestor-versus-descendant mistake stops recurring.
32. As a maintainer, I want the trailing-slash invariant enforced by a type, so that the sibling-prefix bug cannot be reintroduced by a new call site.
33. As a maintainer, I want one way to turn a reference-data identifier into a label, so that retired items and the "Other" free-text override behave consistently.
34. As a maintainer, I want adding a field to a Sale to be a single change, so that a new field cannot reach one write path and miss the other.
35. As a maintainer, I want validating one Sale to cost a single database read, so that draining a queued batch does not multiply into hundreds of round trips.
36. As a maintainer, I want the Event History interface to stop growing two methods per screen tab, so that adding a tab is a small change.
37. As a maintainer, I want to understand why the query filter deliberately differs from application code, so that I do not "fix" something intentional.

## Implementation Decisions

**Shared rules module (ADR-0002).** A new `DotGlasses.Rules` project holds the consultation rules
and a reference-data snapshot type. `DotGlasses.App` may reference `Contracts` and `Rules`, and
nothing else — this amends the standing App-reference rule, and the amendment lands in the same
commit that creates the project so the rule and the reference never disagree. Rules are composed
internally from per-topic functions (referral, lens range, coating set, frame, hard case, occupation)
but exposed with a request-DTO-shaped surface, one entry point per consultation type. Failure keys
remain request-DTO property names, because three separate mechanisms already depend on that: the
Field App's field-error bag, the server's validation problem response, and the admin conversion
form's model-state remap.

**Disposition of the existing validators.** The three consultation request validators are deleted
outright, not thinned into delegating shells. Controllers call the rules module directly and map its
failures to a validation response. FluentValidation is retained for the remaining seven validators,
which still use async rules — so the standing decision not to use automatic FluentValidation model
binding is unaffected and its rationale unchanged.

**Reference-data snapshot.** One snapshot type carries reference-data items with their active and
"Other option" state, the preset catalogues, and the Coating pairing and exclusion rules. Two
adapters fill it: the server loads every item from the database, the Field App fills it from the
existing cached API response, which returns active items only. The rule "present and active" is
correct under both fillings, so no API change is required. Because the server's copy carries retired
items, it also becomes the single label resolver, replacing seven separate implementations that had
four different fallback strings between them; the fallback standardises on the em-dash, the only one
of the four that is honest about an item that was not found. The snapshot is loaded once per
request. Caching it across requests is deliberately deferred — the web app can scale to multiple
replicas, so an in-memory cache would be per-replica and an admin's reference-data edit would be live
on one replica and stale on the others until invalidation crossed them.

**Sale assembly.** A builder in `DotGlasses.Rules` produces a Sale creation request from a Lead plus
the answers supplied, and both write paths use it — the Field App and the admin conversion form. The
missing referral fields on the admin path are point-fixed first, ahead of the builder, so a live data
gap is not left open for the length of the programme; the builder then subsumes that fix.

**Domain rejections (ADR-0003).** Business-rule rejections throw a dedicated exception type mapped
by a single filter to a validation response. Messages stay as user-facing copy rather than becoming
codes. This replaces the current pattern where rejections and missing-row failures share a type, and
where only some screens catch them.

**Hierarchy (ADR-0004).** A `HierarchyPath` value type in the domain layer owns the trailing-slash
invariant and exposes the ancestor and descendant questions as separately named operations. A
companion org-tree lookup module absorbs the two near-identical private lookup classes in the
Dashboard and Event History query services and becomes the single definition of **Retailer** — the
nearest `Intermediate`-level ancestor of a retail point, per `CONTEXT.md`. Custom Orders adopts that
definition, replacing its divergent immediate-parent resolution. Persistence keeps a plain string
column and the global query filter continues to operate on it; the value type wraps at the
application edges only. The Field App never sees the type — hierarchy paths are stamped server-side
from claims.

**Coating vocabulary (ADR-0001 scope correction).** A **Coating set** belongs to a Sale. A **Coating
preference** — a single value — belongs to a Test or a Lead, and seeds the Sale's Coating set on
conversion. The code already matched this; the ADR and glossary overstated it and have been
corrected. No model change follows from this decision.

**Event History.** The query interface collapses from eight methods to four: list and export unify
behind an optional paging parameter, which removes the risk the two queries drift apart. It does not
collapse to one — the four screen tabs return genuinely different row shapes and the language offers
no union type to return them from a single method without a cast.

**Frame coverage.** The field is retained on the Sale record but removed from the admin conversion
form, so it is uniformly non-editable and the two write paths stop disagreeing about whether the
question is asked. Removing the column is rejected as a migration against real data for no benefit.

**Atomicity.** Inviting a user becomes a single atomic operation rather than three independent
writes. The two conversion paths already commit atomically; that guarantee becomes asserted rather
than assumed.

**Landing order.** Nine changes, delivered as one branch and pull request each, matching the repo's
existing convention:

1. Point fixes — the custom-order double-advance failure, the admin referral fields, frame coverage
   off the admin form, and removal of the unused unscoped widget query.
2. Test-harness migration to real Postgres, plus tests for the three consultation services and the
   access-control policies.
3. The `DotGlasses.Rules` project and the reference-data snapshot; label resolution collapses onto it.
4. The rules module; the three validators deleted; controllers call it. *(needs 3)*
5. The Field App onto the shared rules; its client-side rule copies deleted. *(needs 4)*
6. The Sale-assembly builder. *(needs 4)*
7. The rejection seam, the silently half-completing conversion, and invite atomicity. *(needs 2)*
8. The hierarchy value type and org lookup; Custom Orders adopts the Retailer definition.
9. The Event History interface collapse.

Changes 1, 2, 8 and 9 have no dependencies and may land in any order. **Change 2 must precede change
4** — moving roughly 990 lines of rules with no test on either side of the move is the single
riskiest step in the programme, and it is avoidable.

The behavioural contract document is amended per change, as each rule becomes true, rather than up
front — with the single exception of the App-reference rule noted above.

## Testing Decisions

A good test here asserts external behaviour at an interface and says nothing about how that
behaviour is produced. It should survive the implementation being rewritten. Tests are named for the
guarantee they protect, not the method they call. Where a rule has a boundary — an age of 120, a
pupil distance of 54, an axis of 180, a hierarchy path of `/1/40/` against a prefix of `/1/4/` — the
boundary is the case worth writing.

Four seams, in order of how much behaviour they carry:

**The rules module interface** *(new).* Nearly all consultation behaviour is tested here: conditional
requiredness, numeric ranges and increments, cross-field emptiness rules, the "Other" free-text
requirement, Coating set exclusions, and per-lens coating availability. Tests supply a snapshot as a
literal value and assert the returned failures by field key and count. This is the seam both the
Field App and the server cross, so a test written once covers both. Testing below it — at the
per-topic functions — would pin implementation; testing above it would make several hundred rule
permutations slow and would not touch the client path at all.

**The Application service interfaces** *(existing).* Conversion back-links in both directions,
idempotent upsert on replay, find-or-create Customer matching, and the case where a source Test or
Lead is outside the caller's scope and the conversion must not silently half-complete. Fake
repositories, no database. Prior art is the existing `WidgetExampleService` test class and its
dictionary-backed fake repository — the same pattern, and no mocking library is needed or referenced
by any project.

**The HTTP API** *(existing).* Deliberately thin — one or two tests per guarantee, never one per
rule. Three guarantees live only here: that a controller genuinely calls the rules module and maps
its failures onto the correct validation-problem keys; that the access-control policies deny what
they should, for a caller inside and outside the target's subtree; and that a domain rejection
produces a validation response rather than a server error. Prior art is the existing widget API test
class and its web-application factory, moved onto real Postgres.

**Pure domain modules** *(new).* The `HierarchyPath` type and the org-tree lookup, tested directly.
The trailing-slash invariant and the sibling-prefix case are the reason the type exists; the existing
hierarchy query-filter tests already establish that this is the case worth pinning, and that
precedent carries over.

**Harness.** Integration tests move from the in-memory provider to real Postgres via containers.
The in-memory provider does not implement transactions, so atomicity is untestable under it, and it
does not reproduce the SQL string-matching semantics the hierarchy filter depends on. Container
tooling is already a hard prerequisite for local development, so the dependency is present on every
machine and in CI. Pure rule and service tests stay dependency-free and fast.

**Coverage bar for handover.** The three consultation services, the rules module, and the
access-control policies. Reporting query services are explicitly below the bar: a wrong dashboard
figure is visible and correctable, a scoping failure is neither.

## Out of Scope

- **A submission module for the Field App's offline outbox.** Considered and dropped: the sync
  service is already well-shaped, and three call sites make the seam hypothetical. Revisit only if a
  fourth producer appears.
- **A test project for the Field App.** No Blazor test project is created, so change 5's wiring — the
  form calling the shared rules — is covered by the rules tests and a manual pass in the running app,
  not by an automated test. This is an accepted gap, agreed explicitly.
- **Caching the reference-data snapshot** across requests, for the multi-replica reason above.
- **Tests for the reporting query services**, the outbox, and the offline cache.
- **Localisation.** Rejection messages stay as English copy; making them translatable is real work,
  not a rendering change.
- **Any correction path for a Test, Lead or Sale.** They remain create-once events by deliberate
  product decision.
- **Removing the frame-coverage column**, or reintroducing the question on either write path.
- **Extending the Coating set model to Test or Lead.** Settled the other way — see the ADR-0001
  scope correction.
- **Toolkit content and in-app document viewing** — the two remaining feedback tickets, closed as
  `wontfix` and out of handover scope.

## Further Notes

Read ADRs 0002, 0003 and 0004 and the ADR-0001 scope-correction note before starting any change;
they carry the rejected alternatives, which is the part most likely to be re-proposed. `CONTEXT.md`
now defines **Coating set**, **Coating preference** and **Retailer** — use those terms in code and in
commit messages rather than inventing synonyms.

Two claims in the originating review were corrected during grilling and the corrected versions are
what this spec reflects: the Event History interface collapses to four methods rather than one, and
the test work comes before the rules refactor rather than after it.

One unrelated documentation inaccuracy was noticed and deliberately left alone: the open-issues
document still states that no standalone export exists from any screen. Export shipped, and it does
carry the consent flag that bullet required, so the bullet is stale and should be deleted under that
file's own convention.
