using System.Globalization;

namespace DotGlasses.Rules.LensPowers;

/// <summary>
/// The allowed <b>lens power</b> values — the single definition every validator and screen reads
/// (ADR-0007). They are copied from the DOT Glasses online shop's prescription configurator and
/// change only with a release: the device and the server must agree on them, which is why they
/// live here rather than in admin-managed reference data. Nothing else lists these values; a
/// dropdown, a validator or the Lens powers page that needs them reads them from this type.
///
/// Each list is in the shop's order, which is not always ascending: sphere and cylinder put 0.00
/// first because that is by far the most common answer. The matching <c>*Range</c> answers "is
/// this value allowed" arithmetically, and is the same set as the list (pinned by
/// LensPowerValuesTests).
/// </summary>
public static class LensPowerValues
{
    /// <summary>Sphere: -10.00 to +10.00 in 0.25 steps.</summary>
    public static AllowedRange SphereRange { get; } = new(-10.00m, 10.00m, 0.25m);

    /// <summary>Cylinder: 0.00 down to -6.00 in 0.25 steps. The shop sells no positive cylinder;
    /// a blank cylinder means 0.00 (see <see cref="LensPowerRules.HasCylinder"/>).</summary>
    public static AllowedRange CylinderRange { get; } = new(-6.00m, 0.00m, 0.25m);

    /// <summary>Axis: 0 to 180 whole degrees — asked only when the cylinder isn't 0.00.</summary>
    public static AllowedRange AxisRange { get; } = new(0m, 180m, 1m);

    /// <summary>Add: 0.00 to 3.00 in 0.25 steps. An add of 0.00 is no add (see
    /// <see cref="LensPowerRules.NormaliseAdd"/>).</summary>
    public static AllowedRange AddRange { get; } = new(0.00m, 3.00m, 0.25m);

    /// <summary>A Custom prescription's pupil distance: 54 to 74 whole millimetres. Not part of a
    /// lens power, but the same kind of release-controlled value, so it lives beside them.</summary>
    public static AllowedRange PupilDistanceMmRange { get; } = new(54m, 74m, 1m);

    /// <summary>0.00 first, then -10.00 ascending to +10.00 (0.00 not repeated) — the shop's
    /// order.</summary>
    public static IReadOnlyList<decimal> Sphere { get; } = ZeroFirst(SphereRange);

    /// <summary>0.00 first, then -6.00 ascending to -0.25 — the shop's order.</summary>
    public static IReadOnlyList<decimal> Cylinder { get; } = ZeroFirst(CylinderRange);

    public static IReadOnlyList<decimal> Axis { get; } = AxisRange.Values().ToList();

    public static IReadOnlyList<decimal> Add { get; } = AddRange.Values().ToList();

    public static IReadOnlyList<decimal> PupilDistanceMm { get; } = PupilDistanceMmRange.Values().ToList();

    /// <summary>The display format for a sphere, cylinder or add: <c>+</c> on a positive value,
    /// always two decimals (<c>+2.50</c>, <c>0.00</c>, <c>-1.25</c>). Culture-invariant, so a
    /// device set to a comma-decimal locale shows the same text as the shop.</summary>
    public static string FormatPower(decimal value) =>
        (value > 0 ? "+" : "") + value.ToString("0.00", CultureInfo.InvariantCulture);

    private static IReadOnlyList<decimal> ZeroFirst(AllowedRange range) =>
        [0.00m, .. range.Values().Where(v => v != 0)];
}

/// <summary>An inclusive range walked in fixed steps from <paramref name="Min"/>. A value is
/// allowed only when it is inside the range <em>and</em> on a step: a power between two quarter
/// dioptres is no more grindable than one outside the range.</summary>
public sealed record AllowedRange(decimal Min, decimal Max, decimal Step)
{
    public bool Allows(decimal value) => value >= Min && value <= Max && (value - Min) % Step == 0;

    /// <summary>Every allowed value, ascending.</summary>
    public IEnumerable<decimal> Values()
    {
        for (var value = Min; value <= Max; value += Step)
        {
            yield return value;
        }
    }

    /// <summary>A bound as the validation messages write it: <c>-10</c>, <c>0.25</c>, <c>3</c> —
    /// no trailing zeros, invariant culture. The messages predate this type and are
    /// client-visible, so they keep that shape.</summary>
    internal static string Describe(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
