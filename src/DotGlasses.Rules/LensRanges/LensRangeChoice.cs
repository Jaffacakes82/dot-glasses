using DotGlasses.Contracts.Common;

namespace DotGlasses.Rules.LensRanges;

/// <summary>
/// How a lens range travels through a single dropdown (ADR-0005): each lens set's id, or
/// <see cref="Custom"/> for a Custom prescription. The Field App's lens range selector and the Admin
/// Portal's conversion screen both offer lens sets and Custom in one control, so both turn that one
/// value into the request's two fields — LensRangeType and PresetCatalogueId — here, not each their
/// own way.
/// </summary>
public static class LensRangeChoice
{
    public const string Custom = "custom";

    /// <summary>Anything that is neither <see cref="Custom"/> nor a lens set id — the empty
    /// placeholder, "No preference yet" — is no lens range at all.</summary>
    public static (LensRangeType? LensRangeType, Guid? PresetCatalogueId) Parse(string? value) => value switch
    {
        Custom => (LensRangeType.Custom, null),
        _ when Guid.TryParse(value, out var lensSetId) => (LensRangeType.LensSet, lensSetId),
        _ => (null, null),
    };

    /// <summary>The dropdown value for a lens range, or null when none is chosen.</summary>
    public static string? Format(LensRangeType? lensRangeType, Guid? presetCatalogueId) => lensRangeType switch
    {
        LensRangeType.LensSet when presetCatalogueId is { } lensSetId => lensSetId.ToString(),
        LensRangeType.Custom => Custom,
        _ => null,
    };
}
