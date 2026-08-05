namespace DotGlasses.Domain.Entities;

/// <summary>
/// "This lens strength is available in this coating" — the many-to-many that replaced
/// LensOption's old single forced CoatingId (2026-08-05). Both FKs point at ReferenceDataItem,
/// in different categories (LensStrength and Coating respectively) — category correctness is
/// enforced in the Application layer, same trade-off ReferenceDataItem's own doc comment already
/// makes for every other FK into it. Editable from the Preset Catalogues admin screen, not
/// Reference Data — this relationship is about how catalogues work, not a property Reference
/// Data's generic per-category screen should special-case.
/// </summary>
public class LensStrengthCoatingOption
{
    public Guid Id { get; set; }

    public Guid LensStrengthRefId { get; set; }

    public Guid CoatingRefId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
