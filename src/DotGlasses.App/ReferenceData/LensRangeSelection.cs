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

    public decimal? SphereLeft { get; set; }
    public decimal? CylinderLeft { get; set; }
    public decimal? AxisLeft { get; set; }
    public decimal? AddLeft { get; set; }
    public decimal? SphereRight { get; set; }
    public decimal? CylinderRight { get; set; }
    public decimal? AxisRight { get; set; }
    public decimal? AddRight { get; set; }

    /// <summary>Required once either add power is set (two distinct powers on that eye) — see
    /// LensRangeSelector.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>The real inter-pupillary distance in mm — Custom range only.</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Coarse 0-4 PD shorthand for a lens set (0-2 when ChildrensFrame) — lens sets
    /// only, see Sale.PresetPupilDistanceBucket.</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }
}
