# Domain rule rejections travel as a typed exception handled by one filter, not a `Result` type

Application and Infrastructure services signal a business-rule rejection by throwing
`InvalidOperationException` carrying user-facing copy — 23 throw sites across the reference-data,
organisation, preset-catalogue, custom-order and identity services. Only 5 controller actions
catch it. The uncaught ones surface as a generic 500 error page: advancing an already-fulfilled
custom order, or assigning a preset catalogue below Country level, both produce a stack trace
rather than the message the service went to the trouble of writing. Worse, `InvalidOperationException`
is also what EF throws for a missing row (`FirstAsync` on an empty sequence), so a domain rejection
and a scoping miss are indistinguishable at the catch site.

**Decision.** Rejections throw a `DomainRuleViolationException`, mapped by a single exception
filter to a validation response. Messages stay as user-facing copy rather than becoming codes the
Web layer renders.

**Considered and rejected:** returning a `Result` from every service interface that can reject.
It's the purer shape — no control flow through exceptions, and the compiler forces callers to
handle the rejection. It was rejected on the shape of the actual failure: the bug here is a
*missed* catch, and a filter makes that structurally impossible, whereas `Result` only relocates
the obligation to a `switch` a caller can still get wrong. `Result` also means changing every
interface and every caller for 23 sites, against a filter and one new exception type.

**Consequences.** Because messages are copy rather than codes, localisation would be real work
rather than a rendering change — worth knowing before anyone promises a second language. EF's own
`InvalidOperationException` stays distinguishable from a domain rejection, which is the point:
"sequence contains no elements" should never reach a user, and after this it can't be mistaken for
something that should.
