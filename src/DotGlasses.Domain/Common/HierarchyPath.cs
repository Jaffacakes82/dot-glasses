using System.Diagnostics.CodeAnalysis;

namespace DotGlasses.Domain.Common;

/// <summary>
/// The materialized path behind every IHierarchyScoped entity's HierarchyPath column: a run of
/// ever-increasing integer segments bracketed by slashes, e.g. "/1/4/12/".
///
/// The trailing slash is load-bearing — it is the only thing stopping the prefix "/1/4/" from
/// matching the unrelated sibling "/1/40/" — so this type owns that invariant rather than leaving
/// it to whichever call site remembers it.
///
/// The two containment questions are deliberately separate, differently named operations:
/// IsSelfOrDescendantOf ("is this row inside that node's subtree?") and IsSelfOrAncestorOf ("is
/// this node at or above that row?"). In raw string form both are the same StartsWith call with
/// its operands swapped, and getting them the wrong way round is a bug class that has bitten this
/// codebase twice (see CLAUDE.md's ancestor-resolution pitfall) — naming them forces a caller to
/// say which question it is asking.
///
/// Persistence deliberately keeps the plain string column, and the global query filter deliberately
/// keeps operating on that raw string — see docs/adr/0004. This type wraps at the application
/// edges only, never at the database.
/// </summary>
public sealed record HierarchyPath
{
    private const char SegmentSeparator = '/';

    /// <summary>Shortest legal path: a separator, one digit, a separator.</summary>
    private const int MinimumLength = 3;

    private HierarchyPath(string value, int depth)
    {
        Value = value;
        Depth = depth;
    }

    /// <summary>The string the database stores — Parse(path.Value) round-trips back to path.</summary>
    public string Value { get; }

    /// <summary>Number of segments, so "/1/" is 1 and "/1/4/12/" is 3. The ordering key for
    /// "nearest ancestor" questions — segment count, not string length.</summary>
    public int Depth { get; }

    /// <summary>True when <paramref name="value"/> satisfies the invariant and so would parse.</summary>
    public static bool IsValid([NotNullWhen(true)] string? value) => value is not null && TryMeasure(value, out _);

    public static HierarchyPath Parse(string? value)
    {
        if (value is null || !TryMeasure(value, out var depth))
        {
            throw new ArgumentException(
                $"\"{value}\" is not a hierarchy path — expected slash-bracketed integer segments like \"/1/4/12/\", including the trailing slash.",
                nameof(value));
        }

        return new HierarchyPath(value, depth);
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out HierarchyPath? path)
    {
        if (value is not null && TryMeasure(value, out var depth))
        {
            path = new HierarchyPath(value, depth);
            return true;
        }

        path = null;
        return false;
    }

    /// <summary>"Is this path inside <paramref name="ancestor"/>'s subtree (or is it that node
    /// itself)?" — the data-scoping direction: which rows may a viewer at <paramref name="ancestor"/>
    /// see.</summary>
    public bool IsSelfOrDescendantOf(HierarchyPath ancestor) =>
        Value.StartsWith(ancestor.Value, StringComparison.Ordinal);

    /// <summary>"Is this path at or above <paramref name="descendant"/>?" — the ancestor-resolution
    /// direction: which country/retailer sits over a given row. The exact mirror of
    /// IsSelfOrDescendantOf, spelled out so the two can never be swapped by accident.</summary>
    public bool IsSelfOrAncestorOf(HierarchyPath descendant) => descendant.IsSelfOrDescendantOf(this);

    public override string ToString() => Value;

    /// <summary>Validates the invariant and counts the segments in one pass: a leading separator,
    /// one or more non-empty all-digit segments, each closed by a separator. Mirrors the wire-level
    /// regex on CreateWidgetExampleRequest ("^/(\d+/)+$"), which stays a string — Contracts may not
    /// reference Domain.</summary>
    private static bool TryMeasure(string value, out int depth)
    {
        depth = 0;

        if (value.Length < MinimumLength || value[0] != SegmentSeparator || value[^1] != SegmentSeparator)
        {
            return false;
        }

        var segmentLength = 0;

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];

            if (c == SegmentSeparator)
            {
                if (segmentLength == 0)
                {
                    depth = 0;
                    return false;
                }

                depth++;
                segmentLength = 0;
                continue;
            }

            if (!char.IsAsciiDigit(c))
            {
                depth = 0;
                return false;
            }

            segmentLength++;
        }

        return true;
    }
}
