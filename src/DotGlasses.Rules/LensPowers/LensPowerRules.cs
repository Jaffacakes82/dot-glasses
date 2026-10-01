using DotGlasses.Contracts.Common;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.LensPowers;

/// <summary>
/// The names one eye's four lens power fields go by in whatever is being checked — a consultation
/// request's <c>SphereLeft</c>…<c>AddLeft</c>, or a lens-set lens's own fields. Each is the
/// <see cref="RuleFailure.Key"/> (so it must be the checked object's own property name — see
/// RuleFailure); the matching label is what the message calls the field.
/// </summary>
public sealed record LensPowerNames(string Sphere, string Cylinder, string Axis, string Add)
{
    /// <summary>What each field is called inside a message. Defaults to the key, which is right
    /// for a caller whose keys already are the names on screen (the Add lens dialog); the
    /// consultation rules set these, because their keys are request property names and no
    /// technician should have to read one.</summary>
    public string SphereLabel { get; init; } = Sphere;
    public string CylinderLabel { get; init; } = Cylinder;
    public string AxisLabel { get; init; } = Axis;
    public string AddLabel { get; init; } = Add;
}

/// <summary>
/// Whether one eye's <b>lens power</b> is valid, and whether the pair's <b>lens type</b> fits it
/// (ADR-0007). Pure functions over the four numbers, shared by the consultation rules and — from
/// the lens-set tickets on — the Admin Portal's add-lens dialog, so a lens power means the same
/// thing wherever it is entered.
///
/// <list type="bullet">
/// <item>The sphere is required.</item>
/// <item>A blank cylinder means 0.00.</item>
/// <item>The axis is required when the cylinder isn't 0.00, and must be empty when it is.</item>
/// <item>A blank add, or an add of 0.00, is no add.</item>
/// <item>Every value given must be one of <see cref="LensPowerValues"/>'s.</item>
/// </list>
/// </summary>
public static class LensPowerRules
{
    /// <summary>A blank add and an add of 0.00 are the same thing: no add. The shop treats 0.00
    /// that way and so does every rule here — in particular it never makes a lens type
    /// required.</summary>
    public static decimal? NormaliseAdd(decimal? add) => add is 0m ? null : add;

    /// <summary>
    /// One eye's cylinder, axis and add as a record stores them: a 0.00 (or blank) cylinder is no
    /// cylinder, an axis only goes with a cylinder, and a 0.00 add is no add. A lens set's lenses
    /// are stored this way, so a record normalised through here stores a Custom +3.00 exactly as
    /// it stores a lens set's +3.00. Applied on write, after the rules have passed: it never turns
    /// a refused power into an accepted one, only picks one spelling of an accepted power.
    /// </summary>
    public static (decimal? Cylinder, decimal? Axis, decimal? Add) Normalise(decimal? cylinder, decimal? axis, decimal? add) =>
        HasCylinder(cylinder) ? (cylinder, axis, NormaliseAdd(add)) : (null, null, NormaliseAdd(add));

    /// <summary>Whether this eye has an add above 0 — the thing that makes a lens type
    /// required.</summary>
    public static bool HasAdd(decimal? add) => add > 0m;

    /// <summary>Whether this eye has a cylinder other than 0.00 — blank means 0.00 — which is the
    /// thing that makes an axis required.</summary>
    public static bool HasCylinder(decimal? cylinder) => cylinder is { } value && value != 0m;

    /// <summary>One eye's lens power, in full: the sphere is required, then
    /// <see cref="CheckValues"/>.</summary>
    public static IEnumerable<RuleFailure> Check(
        decimal? sphere, decimal? cylinder, decimal? axis, decimal? add, LensPowerNames names) =>
        sphere is null
            ? [new RuleFailure(names.Sphere, $"{names.SphereLabel} is required."), .. CheckValues(sphere, cylinder, axis, add, names)]
            : CheckValues(sphere, cylinder, axis, add, names);

    /// <summary>
    /// Everything except the sphere requirement: each value given is an allowed one, and the axis
    /// agrees with the cylinder. For a caller that reports a missing sphere its own way — the
    /// consultation requests have always reported both eyes' missing spheres as one failure
    /// against LensRangeType, and that client-visible message stays.
    ///
    /// The axis/cylinder agreement is only asked of a cylinder that is itself allowed: while the
    /// cylinder is wrong, "axis required" or "axis must be empty" would be a second message about a
    /// value the technician is about to change. An axis that is given is still range-checked.
    /// </summary>
    public static IEnumerable<RuleFailure> CheckValues(
        decimal? sphere, decimal? cylinder, decimal? axis, decimal? add, LensPowerNames names)
    {
        foreach (var failure in Power(sphere, names.Sphere, names.SphereLabel, LensPowerValues.SphereRange))
        {
            yield return failure;
        }

        foreach (var failure in Power(cylinder, names.Cylinder, names.CylinderLabel, LensPowerValues.CylinderRange))
        {
            yield return failure;
        }

        var cylinderIsAllowed = cylinder is not { } c || LensPowerValues.CylinderRange.Allows(c);
        if (axis is { } a)
        {
            if (cylinderIsAllowed && !HasCylinder(cylinder))
            {
                yield return new RuleFailure(names.Axis, $"{names.AxisLabel} must be empty when {names.CylinderLabel} is 0.00 — an axis only applies to a cylinder.");
            }
            else if (!LensPowerValues.AxisRange.Allows(a))
            {
                yield return new RuleFailure(
                    names.Axis,
                    $"{names.AxisLabel} must be a whole number of degrees between {AllowedRange.Describe(LensPowerValues.AxisRange.Min)} and {AllowedRange.Describe(LensPowerValues.AxisRange.Max)}.");
            }
        }
        else if (cylinderIsAllowed && HasCylinder(cylinder))
        {
            yield return new RuleFailure(
                names.Axis,
                $"{names.AxisLabel} is required when {names.CylinderLabel} isn't 0.00 — choose an axis from {AllowedRange.Describe(LensPowerValues.AxisRange.Min)} to {AllowedRange.Describe(LensPowerValues.AxisRange.Max)}.");
        }

        foreach (var failure in Power(add, names.Add, names.AddLabel, LensPowerValues.AddRange))
        {
            yield return failure;
        }
    }

    /// <summary>
    /// The pair's <b>lens type</b> — one per pair, never per eye. Required when either eye has an
    /// add above 0 (<paramref name="hasAdd"/>; see <see cref="HasAdd"/>), and must be empty
    /// otherwise, which is what single vision means: it is inferred, never chosen, and is not
    /// reference data. The chosen item must be an active LensType reference-data item, and the
    /// category's "Other" item needs its free text.
    ///
    /// The snapshot is only asked about LensType items, so a caller that must not use the
    /// memoised per-request snapshot (a write to the reference-data library) can hand over a
    /// literal one holding just those.
    /// </summary>
    public static IEnumerable<RuleFailure> LensType(
        bool hasAdd, Guid? lensTypeRefId, string? lensTypeOtherText, ReferenceDataSnapshot snapshot,
        string refIdKey, string otherTextKey)
    {
        if (!hasAdd)
        {
            return lensTypeRefId is not null || lensTypeOtherText is not null
                ? [new RuleFailure(refIdKey, "A lens type only applies to a lens with an add power — remove the lens type.")]
                : [];
        }

        if (lensTypeRefId is null)
        {
            return [new RuleFailure(refIdKey, "Choose a lens type — a lens with an add power needs one.")];
        }

        if (snapshot.FindItem(lensTypeRefId, ReferenceDataCategory.LensType) is not { IsActive: true } item)
        {
            return [new RuleFailure(refIdKey, "Choose a lens type from the list.")];
        }

        return item.IsOtherOption && string.IsNullOrWhiteSpace(lensTypeOtherText)
            ? [new RuleFailure(otherTextKey, "Say what the other lens type is.")]
            : [];
    }

    /// <summary>Range and step are one question with one message — see
    /// <see cref="AllowedRange"/>.</summary>
    private static IEnumerable<RuleFailure> Power(decimal? value, string name, string label, AllowedRange range) =>
        value is { } v && !range.Allows(v)
            ? [new RuleFailure(name, $"{label} must be between {AllowedRange.Describe(range.Min)} and {AllowedRange.Describe(range.Max)} in {AllowedRange.Describe(range.Step)} increments.")]
            : [];
}
