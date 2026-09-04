namespace DotGlasses.Rules;

/// <summary>
/// One rejected field. <paramref name="Key"/> is the <em>request DTO's own property name</em>
/// (e.g. <c>ReasonNotPurchasedRefId</c>) and that is load-bearing, not a convention anyone is free
/// to improve: three separate mechanisms already key off it — the Field App's FormErrors bag, the
/// server's ValidationProblemDetails response, and LeadConversionController's
/// <c>Form.{PropertyName}</c> model-state remap. Changing the shape of a key silently detaches a
/// message from the control that produced it.
/// </summary>
public sealed record RuleFailure(string Key, string Message);

/// <summary>The outcome of checking one consultation request against the reference-data
/// snapshot. Messages are user-facing English copy, not codes — see ADR-0003.</summary>
public sealed class RuleResult
{
    private RuleResult(IReadOnlyList<RuleFailure> failures) => Failures = failures;

    public static RuleResult Valid { get; } = new([]);

    public IReadOnlyList<RuleFailure> Failures { get; }

    public bool IsValid => Failures.Count == 0;

    public static RuleResult From(IEnumerable<RuleFailure> failures)
    {
        var materialised = failures.ToList();
        return materialised.Count == 0 ? Valid : new RuleResult(materialised);
    }
}
