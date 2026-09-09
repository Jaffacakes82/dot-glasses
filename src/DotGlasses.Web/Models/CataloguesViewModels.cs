using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public record CataloguesIndexViewModel(
    IReadOnlyList<CatalogueCard> Catalogues,
    IReadOnlyList<(Guid Id, string Label)> AllLensStrengths,
    IReadOnlyList<(Guid Id, string Label)> ActiveCoatings,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> AvailableCoatingsByLensStrength,
    IReadOnlyList<(Guid Id, string Name)> AssignableOrgs,
    string? Search);

public record CatalogueCard(Guid Id, string Name, string? Description, string? RangeDescription, PresetCatalogueKind Kind, IReadOnlyList<LensOptionCard> LensOptions, IReadOnlyList<AssignedOrgCard> AssignedOrgs);

public record AssignedOrgCard(Guid OrgNodeId, string OrgName);

public record LensOptionCard(Guid Id, Guid LensStrengthRefId, string Label, int SortOrder);

public class CreateCatalogueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RangeDescription { get; set; }
    public PresetCatalogueKind Kind { get; set; } = PresetCatalogueKind.Other;
}

public class UpdateCatalogueRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RangeDescription { get; set; }
    public PresetCatalogueKind Kind { get; set; } = PresetCatalogueKind.Other;
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
