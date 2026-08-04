namespace DotGlasses.Contracts.Tests;

/// <summary>Mirrors DotGlasses.Domain.Enums.TestOutcome — see Contracts.Common.Gender for why
/// Contracts keeps its own copy rather than referencing Domain.</summary>
public enum TestOutcome
{
    NoGlassesNeeded = 0,
    NeedsGlasses = 1,
    Referred = 2,
}
