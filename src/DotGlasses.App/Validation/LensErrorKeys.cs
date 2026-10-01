namespace DotGlasses.App.Validation;

/// <summary>
/// The request DTO property names the lens section renders messages against, grouped by what a
/// change to the lens section invalidates (see <see cref="FormErrors.ClearFields"/>). The one list
/// the form's known-field sets and the controls' clearing both read, so a new lens key can't be
/// shown by one and forgotten by the other.
/// </summary>
public static class LensErrorKeys
{
    /// <summary>Every lens-section key — changing the lens range invalidates all of them.</summary>
    public static readonly string[] All =
        ["LensRangeType", "PresetCatalogueId", "SphereLeft", "SphereRight",
         "CylinderLeft", "CylinderRight", "AxisLeft", "AxisRight", "AddLeft", "AddRight",
         "LensTypeRefId", "LensTypeOtherText", "PupilDistanceMm", "PresetPupilDistanceBucket"];

    /// <summary>What choosing a lens (or toggling "Same lens for both eyes") sets: each eye's
    /// power and the pair's lens type. Not the range, and not the pupil distance.</summary>
    public static readonly string[] LensChoice =
        ["SphereLeft", "SphereRight", "CylinderLeft", "CylinderRight", "AxisLeft", "AxisRight",
         "AddLeft", "AddRight", "LensTypeRefId", "LensTypeOtherText"];

    /// <summary>The lens type radios' keys, cleared together with the add that asks for them.</summary>
    public static readonly string[] LensType = ["LensTypeRefId", "LensTypeOtherText"];
}
