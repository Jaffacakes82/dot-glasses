namespace DotGlasses.Contracts.Sales;

/// <summary>Mirrors DotGlasses.Domain.Enums.FrameCoverage — see Contracts.Common.Gender for why
/// Contracts keeps its own copy rather than referencing Domain. Sale-only, not shared with Lead,
/// so it lives here rather than Contracts.Common.</summary>
public enum FrameCoverage
{
    FullFrame = 0,
    EyeFrameRimsOnly = 1,
}
