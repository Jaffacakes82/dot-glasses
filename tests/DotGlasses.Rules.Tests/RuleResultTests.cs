namespace DotGlasses.Rules.Tests;

/// <summary>
/// The shape tickets 09–12 build on. A failure's Key is a request-DTO property name and three
/// mechanisms key off it (the Field App's FormErrors bag, the server's ValidationProblemDetails,
/// and LeadConversionController's Form.{PropertyName} remap), so the carrier must keep it verbatim
/// and must not collapse two failures that happen to share one.
/// </summary>
public class RuleResultTests
{
    [Fact]
    public void Valid_HasNoFailures()
    {
        Assert.True(RuleResult.Valid.IsValid);
        Assert.Empty(RuleResult.Valid.Failures);
    }

    [Fact]
    public void From_NoFailures_IsValid()
    {
        Assert.True(RuleResult.From([]).IsValid);
    }

    [Fact]
    public void From_Failures_KeepsKeysAndMessagesInOrder()
    {
        var result = RuleResult.From(
        [
            new RuleFailure("ReferralReasonRefId", "ReferralReasonRefId is required when ReferredOrTreated is true."),
            new RuleFailure("ReferralLocationFreeText", "ReferralLocationFreeText is required unless TreatedInFacility is true."),
        ]);

        Assert.False(result.IsValid);
        Assert.Equal(["ReferralReasonRefId", "ReferralLocationFreeText"], result.Failures.Select(f => f.Key));
        Assert.Equal("ReferralReasonRefId is required when ReferredOrTreated is true.", result.Failures[0].Message);
    }

    [Fact]
    public void From_TwoFailuresOnOneKey_KeepsBoth()
    {
        // A field can fail more than one rule at once, and ValidationProblemDetails renders a
        // list per key — deduplicating here would silently drop the second message.
        var result = RuleResult.From(
        [
            new RuleFailure("CustomAxisRight", "CustomAxisRight must be a whole number."),
            new RuleFailure("CustomAxisRight", "CustomAxisRight must be between 0 and 180."),
        ]);

        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public void From_EnumeratesItsSourceOnlyOnce()
    {
        // The per-topic rule functions tickets 09–11 add will be lazy LINQ chains over the
        // snapshot; From must materialise them rather than re-run them per read.
        var enumerations = 0;
        var result = RuleResult.From(Failures());

        Assert.False(result.IsValid);
        _ = result.Failures.Count;
        _ = result.Failures.Count;
        Assert.Equal(1, enumerations);

        IEnumerable<RuleFailure> Failures()
        {
            enumerations++;
            yield return new RuleFailure("OccupationRefId", "OccupationRefId must reference an existing, active Occupation reference-data item.");
        }
    }
}
