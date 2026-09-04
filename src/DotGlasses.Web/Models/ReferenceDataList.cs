using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public record ReferenceDataOption(Guid Id, string Label, string? ImageUrl);

/// <summary>CoatingPairings/CoatingExclusions are only ever populated for the Coating category row
/// — see ADR-0001. Every other category leaves them empty.</summary>
public record ReferenceDataList(
    ReferenceDataCategory Category,
    string Name,
    string ScopeNote,
    bool ShowImageField,
    bool HasActiveOtherOption,
    IReadOnlyList<ReferenceDataOption> ActiveOptions,
    IReadOnlyList<ReferenceDataOption> RetiredOptions,
    IReadOnlyList<CoatingPairingAdminItem> CoatingPairings,
    IReadOnlyList<CoatingExclusionAdminItem> CoatingExclusions);
