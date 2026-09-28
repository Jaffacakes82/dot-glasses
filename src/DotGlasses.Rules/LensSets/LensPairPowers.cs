namespace DotGlasses.Rules.LensSets;

/// <summary>
/// What <see cref="LensSetLenses.RecordedAs"/> works out for a chosen pair of lens set lenses: the
/// per-eye lens power fields and the one lens type a Test, Lead or Sale records — named exactly as
/// the create requests name them, so a caller copies them across one for one.
/// </summary>
public sealed record LensPairPowers(
    decimal? SphereLeft, decimal? CylinderLeft, decimal? AxisLeft, decimal? AddLeft,
    decimal? SphereRight, decimal? CylinderRight, decimal? AxisRight, decimal? AddRight,
    Guid? LensTypeRefId, string? LensTypeOtherText);
