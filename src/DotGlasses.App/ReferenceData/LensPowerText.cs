using System.Globalization;
using DotGlasses.Rules.LensPowers;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// One eye's lens power as a line of text, in the Rules display format
/// (<see cref="LensPowerValues.FormatPower"/>) — the same wording the Admin Portal's Lens Sets
/// table uses: "SPH +2.50", then "CYL -0.75 × 90" and "ADD +2.00" only when the lens has them (a
/// blank or 0.00 cylinder and a blank or 0.00 add are absent, per <see cref="LensPowerRules"/>).
/// It is shown under a chosen lens and in the "no longer in the lens set" note.
/// </summary>
public static class LensPowerText
{
    public static string Format(decimal sphere, decimal? cylinder, decimal? axis, decimal? add)
    {
        var parts = new List<string> { $"SPH {LensPowerValues.FormatPower(sphere)}" };
        if (LensPowerRules.HasCylinder(cylinder))
        {
            var cyl = LensPowerValues.FormatPower(cylinder!.Value);
            parts.Add(axis is { } a ? $"CYL {cyl} × {a.ToString("0", CultureInfo.InvariantCulture)}" : $"CYL {cyl}");
        }

        if (LensPowerRules.HasAdd(add))
        {
            parts.Add($"ADD {LensPowerValues.FormatPower(add!.Value)}");
        }

        return string.Join(" · ", parts);
    }
}
