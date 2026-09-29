namespace DotGlasses.Domain.Entities;

/// <summary>
/// "This lens set lens comes in this coating" (ADR-0007, "Coatings"). Replaced the global
/// LensStrengthCoatingOption grid, which keyed availability on a label shared across every set and
/// so offered coatings a lens isn't made in. CoatingRefId points at ReferenceDataItem
/// (Category = Coating); category correctness is enforced in the Application layer, the same
/// trade-off ReferenceDataItem's other FKs make.
/// </summary>
public class LensOptionCoating
{
    public Guid Id { get; set; }

    public Guid LensOptionId { get; set; }

    public Guid CoatingRefId { get; set; }
}
