namespace DotGlasses.Application.Common;

/// <summary>Gender is reused identically across Test/Lead/Sale (unlike TestOutcome/LensRangeType/
/// FrameCoverage, which are entity-specific and mapped inline in their own service) — shared here
/// rather than duplicated three times.</summary>
public static class GenderMapping
{
    public static Domain.Enums.Gender ToDomain(this Contracts.Common.Gender gender) => gender switch
    {
        Contracts.Common.Gender.Female => Domain.Enums.Gender.Female,
        Contracts.Common.Gender.Male => Domain.Enums.Gender.Male,
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };

    public static Contracts.Common.Gender ToContract(this Domain.Enums.Gender gender) => gender switch
    {
        Domain.Enums.Gender.Female => Contracts.Common.Gender.Female,
        Domain.Enums.Gender.Male => Contracts.Common.Gender.Male,
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };
}
