namespace DotGlasses.Contracts.Common;

/// <summary>Mirrors DotGlasses.Domain.Enums.LensRangeType — shared between Lead and Sale, see
/// Contracts.Common.Gender for why Contracts keeps its own copy rather than referencing Domain.</summary>
public enum LensRangeType
{
    SixLensSet = 0,
    NineLensSet = 1,
    Custom = 2,
}
