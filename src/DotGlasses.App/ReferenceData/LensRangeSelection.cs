using DotGlasses.Contracts.Common;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// Plain mutable UI-state model shared between LensRangeSelector.razor and whichever
/// ConsultationForm.razor section hosts it — the child mutates it in place, the parent reads it
/// at submit time and maps it onto CreateLeadRequest/CreateSaleRequest's matching fields.
/// </summary>
public class LensRangeSelection
{
    public LensRangeType? LensRangeType { get; set; }

    public Guid? PresetCatalogueId { get; set; }
    public Guid? LensOptionLeftId { get; set; }
    public Guid? LensOptionRightId { get; set; }

    public decimal? CustomSphereLeft { get; set; }
    public decimal? CustomCylinderLeft { get; set; }
    public decimal? CustomAxisLeft { get; set; }
    public decimal? CustomAddPowerLeft { get; set; }
    public decimal? CustomSphereRight { get; set; }
    public decimal? CustomCylinderRight { get; set; }
    public decimal? CustomAxisRight { get; set; }
    public decimal? CustomAddPowerRight { get; set; }

    /// <summary>Required once either add power is set (two distinct powers on that eye) — see
    /// LensRangeSelector.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>The real inter-pupillary distance in mm — Custom range only.</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Coarse 0-4 PD shorthand for a preset range (0-2 when ChildrensFrame) — preset
    /// ranges only, see Sale.PresetPupilDistanceBucket.</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }
}
