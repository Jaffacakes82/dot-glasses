namespace DotGlasses.Domain.Common;

/// <summary>
/// A business rule refused the operation. The Message is user-facing copy, shown verbatim —
/// DotGlasses.Web's DomainRuleViolationFilter turns one of these into a validation response for
/// the JSON API and an inline banner on the Admin Portal screens, so no controller needs to
/// catch it (ADR-0003).
///
/// Deliberately not InvalidOperationException: EF Core throws that for a missing row (FirstAsync
/// on an empty sequence), which would make a rejection and a scoping miss indistinguishable at
/// the catch site. Anything left as InvalidOperationException is a missing/out-of-scope row or a
/// genuine bug — it has no copy worth showing a user, and still surfaces as a 500.
/// </summary>
public class DomainRuleViolationException(string message) : Exception(message);
