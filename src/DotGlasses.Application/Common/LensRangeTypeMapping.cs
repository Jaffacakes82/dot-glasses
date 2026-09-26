namespace DotGlasses.Application.Common;

/// <summary>Shared between Lead and Sale (both carry LensRangeType) — see GenderMapping.</summary>
public static class LensRangeTypeMapping
{
    public static Domain.Enums.LensRangeType ToDomain(this Contracts.Common.LensRangeType type) => type switch
    {
        Contracts.Common.LensRangeType.LensSet => Domain.Enums.LensRangeType.LensSet,
        Contracts.Common.LensRangeType.Custom => Domain.Enums.LensRangeType.Custom,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static Contracts.Common.LensRangeType ToContract(this Domain.Enums.LensRangeType type) => type switch
    {
        Domain.Enums.LensRangeType.LensSet => Contracts.Common.LensRangeType.LensSet,
        Domain.Enums.LensRangeType.Custom => Contracts.Common.LensRangeType.Custom,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
