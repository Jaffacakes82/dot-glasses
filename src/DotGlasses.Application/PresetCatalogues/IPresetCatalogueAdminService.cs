using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.PresetCatalogues;

/// <summary>Admin-only Preset Catalogue management — backs the Admin Portal's Preset Catalogues
/// screen. Deliberately separate from IPresetCatalogueQueryService (the Field-App-facing "which
/// catalogues can this caller use" read, active-assignment-scoped): this returns every catalogue
/// the caller is allowed to manage and can mutate. Reuses
/// AuthorizationPolicies.PresetCatalogueManage (Admin, Country level+) — no new RBAC
/// policy needed.</summary>
public interface IPresetCatalogueAdminService
{
    /// <summary>Every catalogue, with its lens roster (label-resolved) and assignment count.</summary>
    Task<IReadOnlyList<PresetCatalogueAdminDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>owningOrgNodeId is the caller's own org node (stamped by the Web controller from
    /// ICurrentUserContext, never client-submitted) — see PresetCatalogue's own doc comment for
    /// why it must be Dgi/Country. Enforced here, not in Domain.</summary>
    Task<PresetCatalogueAdminDto> CreateAsync(string name, string? description, string? rangeDescription, Guid owningOrgNodeId, PresetCatalogueKind kind, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, string name, string? description, string? rangeDescription, PresetCatalogueKind kind, CancellationToken cancellationToken = default);

    /// <summary>True if an existing catalogue (other than excludeId) already holds this Kind —
    /// backs the create/update validators' "at most one SixLensSet, at most one NineLensSet"
    /// guard. Always false for Kind.Other, which any number of catalogues may hold.</summary>
    Task<bool> HasCatalogueWithKindAsync(PresetCatalogueKind kind, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>SortOrder is max+1 within the catalogue — matches ReferenceDataAdminService's
    /// CreateAsync convention.</summary>
    Task<PresetCatalogueLensOptionAdminDto> AddLensOptionAsync(Guid catalogueId, Guid lensStrengthRefId, CancellationToken cancellationToken = default);

    /// <summary>True if this exact catalogue/lens-strength pairing already exists — backs
    /// AddLensOptionRequestValidator's duplicate guard (the Field App's lens-range picker would
    /// otherwise render the same strength twice).</summary>
    Task<bool> LensOptionExistsAsync(Guid catalogueId, Guid lensStrengthRefId, CancellationToken cancellationToken = default);

    /// <summary>Hard remove — no historical Test/Lead/Sale can reference a LensOption that was
    /// never actually chosen on one, so nothing needs preserving (see CLAUDE.md).</summary>
    Task RemoveLensOptionAsync(Guid lensOptionId, CancellationToken cancellationToken = default);

    /// <summary>No-op (not an error) if this exact catalogue/org pairing is already assigned —
    /// matches the "select all that apply" mockup UX, which re-submits the whole set on save.</summary>
    Task AssignCatalogueToOrgAsync(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken = default);

    /// <summary>Every org node this catalogue is currently assigned to, name-resolved. A plain
    /// scoped query against OrganisationNodes is correct here (not
    /// IUnscopedReportQueryService) — an assignment outside the caller's own hierarchy scope is
    /// genuinely not this caller's to manage, unlike resolving an ancestor's name.</summary>
    Task<IReadOnlyList<PresetCatalogueAssignmentAdminDto>> ListAssignedOrgsAsync(Guid catalogueId, CancellationToken cancellationToken = default);

    /// <summary>No-op (not an error) if the pairing doesn't exist.</summary>
    Task UnassignCatalogueFromOrgAsync(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken = default);

    /// <summary>The set of Coating reference-data Ids currently configured as available for a
    /// given LensStrength reference-data item — the many-to-many backing
    /// IsCoatingAvailableForLensOptionAsync's validator check.</summary>
    Task<IReadOnlyList<Guid>> ListAvailableCoatingsAsync(Guid lensStrengthRefId, CancellationToken cancellationToken = default);

    /// <summary>No-op if already available.</summary>
    Task AddAvailableCoatingAsync(Guid lensStrengthRefId, Guid coatingRefId, CancellationToken cancellationToken = default);

    Task RemoveAvailableCoatingAsync(Guid lensStrengthRefId, Guid coatingRefId, CancellationToken cancellationToken = default);
}

public record PresetCatalogueAdminDto(
    Guid Id,
    string Name,
    string? Description,
    string? RangeDescription,
    Guid OwningOrgNodeId,
    PresetCatalogueKind Kind,
    IReadOnlyList<PresetCatalogueLensOptionAdminDto> LensOptions);

public record PresetCatalogueLensOptionAdminDto(Guid Id, Guid LensStrengthRefId, string Label, int SortOrder);

public record PresetCatalogueAssignmentAdminDto(Guid OrgNodeId, string OrgName);
