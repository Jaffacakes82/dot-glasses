using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public record CataloguesIndexViewModel(
    IReadOnlyList<CatalogueCard> Catalogues,
    IReadOnlyList<RetiredCatalogueCard> RetiredCatalogues,
    IReadOnlyList<(Guid Id, string Label)> AllLensStrengths,
    IReadOnlyList<(Guid Id, string Label)> ActiveCoatings,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> AvailableCoatingsByLensStrength,
    IReadOnlyList<(Guid Id, string Name)> AssignableOrgs,
    IReadOnlyList<(Guid Id, string Name)> OwningOrgOptions,
    string? Search);

public record RetiredCatalogueCard(Guid Id, string Name, bool CanReactivate);

public record CatalogueCard(Guid Id, string Name, string? Description, IReadOnlyList<LensOptionCard> LensOptions, IReadOnlyList<AssignedOrgCard> AssignedOrgs, bool CanEdit);

public record AssignedOrgCard(Guid OrgNodeId, string OrgName, bool CanUnassign);

public record LensOptionCard(Guid Id, Guid LensStrengthRefId, string Label, int SortOrder);

public class CreateCatalogueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The Dgi/Country-level assignment chosen to own the new lens set. Only posted when
    /// the form showed the field, which happens only when more than one of the caller's own
    /// assignments qualifies — with a single qualifying assignment it is left null and resolved to
    /// that one automatically (CataloguesController.ResolveOwningOrgNodeIdAsync).</summary>
    public Guid? OwningOrgNodeId { get; set; }
}

public class UpdateCatalogueRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddLensOptionRequest
{
    public Guid CatalogueId { get; set; }
    public Guid LensStrengthRefId { get; set; }
}

public class AssignCataloguesRequest
{
    public Guid OrgNodeId { get; set; }
    public List<Guid> CatalogueIds { get; set; } = [];
}

/// <summary>The coating-availability grid posts once, carrying every checked cell — "checked"
/// checkboxes are the only ones the browser submits, so `Selected` is the full desired-available
/// set, not a list of changes. Each entry is "{LensStrengthRefId}:{CoatingRefId}"; see
/// `TryParsePair`.</summary>
public class SetCoatingAvailabilityBatchRequest
{
    public List<string> Selected { get; set; } = [];

    public static bool TryParsePair(string raw, out Guid lensStrengthRefId, out Guid coatingRefId)
    {
        var parts = raw.Split(':');
        if (parts.Length == 2 && Guid.TryParse(parts[0], out lensStrengthRefId) && Guid.TryParse(parts[1], out coatingRefId))
        {
            return true;
        }

        lensStrengthRefId = Guid.Empty;
        coatingRefId = Guid.Empty;
        return false;
    }
}
