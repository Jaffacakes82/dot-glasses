namespace DotGlasses.Domain.Entities;

/// <summary>
/// One lens in a lens set (a "lens set lens"; the type keeps its historical name). Per ADR-0007 a
/// lens is a <b>lens power</b> — one eye's sphere, cylinder, axis and add, from the allowed values
/// in DotGlasses.Rules.LensPowers — plus the typed label technicians choose it by, a lens type when
/// it has an add, the coatings it comes in (<see cref="LensOptionCoating"/>) and its own coating
/// pairings (<see cref="LensOptionCoatingPairing"/>).
///
/// The four power fields have the same types and meaning as a Test/Lead/Sale's per-eye fields, so a
/// lens in a set and a lens on a record compare by value: a blank cylinder means 0.00, an axis only
/// accompanies a cylinder, and a blank add or an add of 0.00 is no add (see LensPowerRules).
///
/// Replaced the 2026-08-05 shape, which pointed at a "Lens strength" reference item whose only
/// content was a label, and whose coatings came from a global grid keyed on that label.
/// </summary>
public class LensOption
{
    public Guid Id { get; set; }

    public Guid PresetCatalogueId { get; set; }

    /// <summary>What technicians see (e.g. "+2.50", "Bifocal +2.00") — typed by the admin, and
    /// unique within its set so no two choices look alike.</summary>
    public string Label { get; set; } = string.Empty;

    public decimal Sphere { get; set; }

    public decimal? Cylinder { get; set; }

    public decimal? Axis { get; set; }

    public decimal? Add { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = LensType). Set only when the lens has an add;
    /// null means single vision, which is inferred, never chosen.</summary>
    public Guid? LensTypeRefId { get; set; }

    /// <summary>The free text for the LensType category's "Other" item.</summary>
    public string? LensTypeOtherText { get; set; }
}
