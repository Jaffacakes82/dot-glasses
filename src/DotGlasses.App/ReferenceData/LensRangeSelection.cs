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

    /// <summary>"Same lens for both eyes" — ticked by default. UI state only, never sent: with it
    /// ticked the technician makes one "Lens" choice and both eyes record that lens's power;
    /// unticked, each eye is chosen separately, the right limited to the left's lens type.</summary>
    public bool SameLensForBothEyes { get; set; } = true;

    /// <summary>Set when a record's lens is no longer in its lens set (seeding from a converted
    /// Lead or a Failed record): the note shown under that eye's choice until a lens is chosen.
    /// UI state only.</summary>
    public string? LensNoteLeft { get; private set; }
    public string? LensNoteRight { get; private set; }

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
    /// An id not in the set chooses nothing for that eye. An eye that now has a lens loses its
    /// "no longer in the lens set" note.
    /// </summary>
    public void ChooseLenses(IReadOnlyList<LensOptionSnapshot> setLenses, Guid? leftId, Guid? rightId)
    {
        var left = setLenses.FirstOrDefault(lens => lens.Id == leftId);
        var right = setLenses.FirstOrDefault(lens => lens.Id == rightId);
        var recorded = LensSetLenses.RecordedAs(left, right);

        LensLeftId = left?.Id;
        LensRightId = right?.Id;
        if (left is not null)
        {
            LensNoteLeft = null;
        }

        if (right is not null)
        {
            LensNoteRight = null;
        }

        (SphereLeft, CylinderLeft, AxisLeft, AddLeft) = (recorded.SphereLeft, recorded.CylinderLeft, recorded.AxisLeft, recorded.AddLeft);
        (SphereRight, CylinderRight, AxisRight, AddRight) = (recorded.SphereRight, recorded.CylinderRight, recorded.AxisRight, recorded.AddRight);
        (LensTypeRefId, LensTypeOtherText) = (recorded.LensTypeRefId, recorded.LensTypeOtherText);
    }

    /// <summary>The one "Lens" choice while "Same lens for both eyes" is ticked: both eyes get it.</summary>
    public void ChooseSameLens(IReadOnlyList<LensOptionSnapshot> setLenses, Guid? lensId) =>
        ChooseLenses(setLenses, lensId, lensId);

    /// <summary>Ticks or unticks "Same lens for both eyes". Ticking gives both eyes the lens
    /// already chosen (the left's, else the right's); unticking leaves both as they are.</summary>
    public void SetSameLensForBothEyes(IReadOnlyList<LensOptionSnapshot> setLenses, bool same)
    {
        SameLensForBothEyes = same;
        if (same)
        {
            var lensId = LensLeftId ?? LensRightId;
            ChooseLenses(setLenses, lensId, lensId);
        }
    }

    /// <summary>The left eye's lens when the eyes are chosen separately. The right eye is limited
    /// to lenses of the left's lens type, so a right lens of another type is dropped rather than
    /// left as a mixed pair the rules would refuse.</summary>
    public void ChooseLeftLens(IReadOnlyList<LensOptionSnapshot> setLenses, Guid? lensId)
    {
        var left = setLenses.FirstOrDefault(lens => lens.Id == lensId);
        var right = setLenses.FirstOrDefault(lens => lens.Id == LensRightId);
        var keepRight = left is null || right is null || right.LensTypeRefId == left.LensTypeRefId;
        ChooseLenses(setLenses, lensId, keepRight ? right?.Id : null);
    }

    public void ChooseRightLens(IReadOnlyList<LensOptionSnapshot> setLenses, Guid? lensId) =>
        ChooseLenses(setLenses, LensLeftId, lensId);

    /// <summary>Records that a record's lens for an eye is no longer in its lens set (the note
    /// text, or null for an eye that has no such problem) — see the seeding in ConsultationForm.</summary>
    public void NoteMissingLenses(string? left, string? right) => (LensNoteLeft, LensNoteRight) = (left, right);

    /// <summary>
    /// Whether two eyes' lens powers are the same lens — the definition the rules and the Admin
    /// Portal use (<see cref="LensSetLenses.Match"/>: a blank cylinder is 0.00, an add of 0.00 is
    /// none, an axis only counts with a cylinder), so a record starts with "Same lens for both
    /// eyes" ticked exactly when its eyes match. Two eyes with nothing chosen count as matching.
    /// </summary>
    public static bool EyesMatch(
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId)
    {
        if (sphereLeft is not { } left)
        {
            return sphereRight is null;
        }

        var leftAsLens = new LensOptionSnapshot(Guid.Empty, string.Empty, left, [], cylinderLeft, axisLeft, addLeft, lensTypeRefId);
        return LensSetLenses.Match([leftAsLens], sphereRight, cylinderRight, axisRight, addRight, lensTypeRefId) is not null;
    }

    /// <summary>Forgets the chosen lenses (switching lens range, or loading a record whose lens
    /// set this device doesn't offer) without touching the powers, and goes back to the default of
    /// one lens for both eyes with no notes.</summary>
    public void ForgetChosenLenses()
    {
        (LensLeftId, LensRightId) = (null, null);
        (LensNoteLeft, LensNoteRight) = (null, null);
        SameLensForBothEyes = true;
    }
}
