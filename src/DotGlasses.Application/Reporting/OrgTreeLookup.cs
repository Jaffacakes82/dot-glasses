using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Reporting;

/// <summary>
/// The single answer to "which outlet, which Retailer, which country is this row under?" — fed a
/// flat set of org nodes and asked about a HierarchyPath, with no database of its own.
///
/// Every question it answers is an *ancestor* one, so what it is fed matters: hand it
/// IUnscopedReportQueryService.GetOrganisationNodesUnscopedAsync's nodes, never a plain scoped
/// OrganisationNodes query — the hierarchy filter only ever shows a caller their own subtree, so a
/// scoped feed silently cannot see the caller's own country and every row resolves to
/// UnknownCountry (CLAUDE.md's standing gotcha, caught twice independently). The module reports the
/// gap honestly rather than guessing; it cannot detect that it was fed the wrong set.
///
/// Retailer is CONTEXT.md's definition and the only one: the nearest Intermediate-level ancestor.
/// A retail point whose nearest node above it is a Country has no Retailer, and ResolveRetailer
/// says so — it never substitutes the country.
/// </summary>
public sealed class OrgTreeLookup
{
    /// <summary>The missing-name fallbacks, defined here once for every reporting screen rather
    /// than copied per call site.</summary>
    public const string UnknownOutlet = "Unknown outlet";

    public const string UnknownCountry = "Unknown country";

    /// <summary>The path is not a node in the tree at all — a data problem, distinct from
    /// NoRetailer.</summary>
    public const string UnknownRetailer = "Unknown retailer";

    /// <summary>The retail point is known and genuinely has no Retailer above it.</summary>
    public const string NoRetailer = "No retailer";

    private readonly Dictionary<HierarchyPath, Node> _byPath;
    private readonly IReadOnlyList<Node> _countries;
    private readonly IReadOnlyList<Node> _intermediates;
    private readonly IReadOnlyList<HierarchyPath> _trainingOrgPaths;

    /// <summary>Throws if any node's stored path does not satisfy the HierarchyPath invariant, or
    /// if two nodes share one — both are data corruption worth surfacing, not working around.</summary>
    public OrgTreeLookup(IReadOnlyList<OrganisationNodeSummary> nodes)
    {
        var parsed = nodes.Select(n => new Node(n, HierarchyPath.Parse(n.HierarchyPath))).ToList();

        _byPath = parsed.ToDictionary(n => n.Path);
        _countries = parsed.Where(n => n.Summary.Level == OrganisationLevel.Country).ToList();
        _intermediates = parsed.Where(n => n.Summary.Level == OrganisationLevel.Intermediate).ToList();
        _trainingOrgPaths = parsed.Where(n => n.Summary.IsTrainingOrg).Select(n => n.Path).ToList();
    }

    /// <summary>The node the row actually sits on — an exact path match, not an ancestor search.</summary>
    public OrganisationNodeSummary? FindOutlet(HierarchyPath path) =>
        _byPath.TryGetValue(path, out var node) ? node.Summary : null;

    public string OutletName(HierarchyPath path) => FindOutlet(path)?.Name ?? UnknownOutlet;

    /// <summary>The Country-level node at or above the path — there is at most one on any path.</summary>
    public OrganisationNodeSummary? FindCountry(HierarchyPath path) =>
        _countries.FirstOrDefault(c => c.Path.IsSelfOrAncestorOf(path))?.Summary;

    public string CountryName(HierarchyPath path) => FindCountry(path)?.Name ?? UnknownCountry;

    /// <summary>Nearest (deepest) Intermediate-level node at or above the path — an Intermediate
    /// can itself sit under another Intermediate, so depth decides which one is the Retailer.</summary>
    public RetailerResolution ResolveRetailer(HierarchyPath path)
    {
        var retailer = _intermediates
            .Where(n => n.Path.IsSelfOrAncestorOf(path))
            .OrderByDescending(n => n.Path.Depth)
            .FirstOrDefault();

        if (retailer is not null)
        {
            return RetailerResolution.Of(retailer.Summary);
        }

        return _byPath.ContainsKey(path) ? RetailerResolution.None : RetailerResolution.Unknown;
    }

    public string RetailerName(HierarchyPath path) => ResolveRetailer(path).Name;

    /// <summary>True when the path is a training org or sits beneath one — the descendant
    /// direction, and the one exclusion Dashboard aggregates apply.</summary>
    public bool IsUnderTrainingOrg(HierarchyPath path) =>
        _trainingOrgPaths.Any(path.IsSelfOrDescendantOf);

    private sealed record Node(OrganisationNodeSummary Summary, HierarchyPath Path);
}

/// <summary>
/// Whether a row has a Retailer, and if not, why not — "the retail point hangs directly off a
/// Country" and "this path is not in the tree" are different facts and must not collapse into one
/// fallback string (CONTEXT.md's Retailer entry).
/// </summary>
public enum RetailerResolutionKind
{
    /// <summary>An Intermediate-level node was found at or above the path.</summary>
    Resolved,

    /// <summary>The node is known, and the nearest node above it is a Country — it genuinely has
    /// no Retailer.</summary>
    NoRetailer,

    /// <summary>The path is not a node in the tree at all.</summary>
    UnknownOrganisation,
}

/// <summary>The answer to "which Retailer?", carrying its own display name so no caller has to
/// re-derive a fallback.</summary>
public sealed record RetailerResolution
{
    private RetailerResolution(RetailerResolutionKind kind, OrganisationNodeSummary? node, string name)
    {
        Kind = kind;
        Node = node;
        Name = name;
    }

    public static RetailerResolution None { get; } =
        new(RetailerResolutionKind.NoRetailer, null, OrgTreeLookup.NoRetailer);

    public static RetailerResolution Unknown { get; } =
        new(RetailerResolutionKind.UnknownOrganisation, null, OrgTreeLookup.UnknownRetailer);

    public static RetailerResolution Of(OrganisationNodeSummary retailer) =>
        new(RetailerResolutionKind.Resolved, retailer, retailer.Name);

    public RetailerResolutionKind Kind { get; }

    /// <summary>The Retailer node, or null when there is none to name.</summary>
    public OrganisationNodeSummary? Node { get; }

    public string Name { get; }

    public bool HasRetailer => Kind == RetailerResolutionKind.Resolved;
}
