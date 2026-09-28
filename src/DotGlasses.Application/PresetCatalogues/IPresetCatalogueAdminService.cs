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

    /// <summary>Retired lens sets only — listed apart so they can be found and reactivated.</summary>
    Task<IReadOnlyList<PresetCatalogueAdminDto>> ListRetiredAsync(CancellationToken cancellationToken = default);

    /// <summary>A soft delete: the lens set stops being offered anywhere (Field App, assign form)
    /// but stays resolvable on the historical records that name it, and keeps its assignments so
    /// reactivating restores it as it was.</summary>
    Task RetireAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The org that owns a lens set, retired ones included (so Reactivate can be
    /// authorized too), or null if there is no such lens set — the resource the edit permission
    /// is checked against.</summary>
    Task<Guid?> FindOwningOrgNodeIdAsync(Guid catalogueId, CancellationToken cancellationToken = default);

    /// <summary>The lens set a lens option belongs to, or null if there is no such option.</summary>
    Task<Guid?> FindCatalogueIdForLensOptionAsync(Guid lensOptionId, CancellationToken cancellationToken = default);

    /// <summary>True if an active lens set other than <paramref name="excludeId"/> already has this
    /// name, ignoring case and surrounding whitespace — the name is the only thing a technician
    /// sees to tell lens sets apart (ADR-0005). A retired lens set's name doesn't count.</summary>
    Task<bool> IsNameTakenAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>owningOrgNodeId is the org chosen to own the new lens set — the Web controller
    /// resolves it from the caller's own Dgi/Country assignments before calling this (spec user
    /// stories 20-21; CreateCatalogueRequestValidator is what actually refuses a choice outside
    /// that set, since checking it needs IUserOrgAssignmentService, not just this service). The
    /// Dgi/Country level rule itself is enforced here regardless, not in Domain, as a second line
    /// of defence — see PresetCatalogue's own doc comment for why it must be Dgi/Country.</summary>
    Task<PresetCatalogueAdminDto> CreateAsync(string name, string? description, Guid owningOrgNodeId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, string name, string? description, CancellationToken cancellationToken = default);

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
    /// given LensStrength reference-data item — the many-to-many that reaches the consultation
    /// rules as LensOptionSnapshot.AvailableCoatingIds, behind
    /// ReferenceDataSnapshot.IsCoatingAvailableForLensOption.</summary>
    Task<IReadOnlyList<Guid>> ListAvailableCoatingsAsync(Guid lensStrengthRefId, CancellationToken cancellationToken = default);

    /// <summary>No-op if already available.</summary>
    Task AddAvailableCoatingAsync(Guid lensStrengthRefId, Guid coatingRefId, CancellationToken cancellationToken = default);

    Task RemoveAvailableCoatingAsync(Guid lensStrengthRefId, Guid coatingRefId, CancellationToken cancellationToken = default);
}

public record PresetCatalogueAdminDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwningOrgNodeId,
    IReadOnlyList<PresetCatalogueLensOptionAdminDto> LensOptions);

public record PresetCatalogueLensOptionAdminDto(Guid Id, Guid LensStrengthRefId, string Label, int SortOrder);

public record PresetCatalogueAssignmentAdminDto(Guid OrgNodeId, string OrgName);
