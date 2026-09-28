using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public record ReferenceDataOption(Guid Id, string Label, string? ImageUrl);

/// <summary>CoatingExclusions is only ever populated for the Coating category row — see ADR-0001.
/// Every other category leaves it empty. There is no pairings list: pairings belong to each lens
/// set lens since ADR-0007.</summary>
public record ReferenceDataList(
    ReferenceDataCategory Category,
    string Name,
    string ScopeNote,
    bool ShowImageField,
    bool HasActiveOtherOption,
    IReadOnlyList<ReferenceDataOption> ActiveOptions,
    IReadOnlyList<ReferenceDataOption> RetiredOptions,
    IReadOnlyList<CoatingExclusionAdminItem> CoatingExclusions);
