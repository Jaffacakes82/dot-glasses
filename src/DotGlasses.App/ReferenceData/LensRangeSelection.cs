using DotGlasses.Contracts.Common;
using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;

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

    /// <summary>Which lens set lens each dropdown shows — UI state only, never sent: a record
    /// holds each eye's lens power and the pair's lens type, not a lens id (ADR-0007). Set through
    /// <see cref="ChooseLenses"/>, which keeps the powers below in step with them.</summary>
    public Guid? LensLeftId { get; private set; }
    public Guid? LensRightId { get; private set; }

    public decimal? SphereLeft { get; set; }
    public decimal? CylinderLeft { get; set; }
    public decimal? AxisLeft { get; set; }
    public decimal? AddLeft { get; set; }
    public decimal? SphereRight { get; set; }
    public decimal? CylinderRight { get; set; }
    public decimal? AxisRight { get; set; }
    public decimal? AddRight { get; set; }

    /// <summary>Required once either add power is set (two distinct powers on that eye) — see
    /// LensRangeSelector. On a lens set, the chosen lenses' own lens type.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>The real inter-pupillary distance in mm — Custom range only.</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Coarse 0-4 PD shorthand for a lens set (0-2 when ChildrensFrame) — lens sets
    /// only, see Sale.PresetPupilDistanceBucket.</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }

    /// <summary>
    /// Chooses a lens set lens for each eye (null for none) from <paramref name="setLenses"/>, the
    /// chosen set's lenses, and records what a lens-set record carries: each eye's power and the
    /// pair's lens type, the same way the Admin Portal does (<see cref="LensSetLenses.RecordedAs"/>).
    /// An id not in the set chooses nothing for that eye.
    /// </summary>
    public void ChooseLenses(IReadOnlyList<LensOptionSnapshot> setLenses, Guid? leftId, Guid? rightId)
    {
        var left = setLenses.FirstOrDefault(lens => lens.Id == leftId);
        var right = setLenses.FirstOrDefault(lens => lens.Id == rightId);
        var recorded = LensSetLenses.RecordedAs(left, right);

        LensLeftId = left?.Id;
        LensRightId = right?.Id;
        (SphereLeft, CylinderLeft, AxisLeft, AddLeft) = (recorded.SphereLeft, recorded.CylinderLeft, recorded.AxisLeft, recorded.AddLeft);
        (SphereRight, CylinderRight, AxisRight, AddRight) = (recorded.SphereRight, recorded.CylinderRight, recorded.AxisRight, recorded.AddRight);
        (LensTypeRefId, LensTypeOtherText) = (recorded.LensTypeRefId, recorded.LensTypeOtherText);
    }

    /// <summary>Forgets the chosen lenses (switching lens range, or loading a record whose lens
    /// set this device doesn't offer) without touching the powers.</summary>
    public void ForgetChosenLenses() => (LensLeftId, LensRightId) = (null, null);
}
