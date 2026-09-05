using DotGlasses.Contracts.Leads;

namespace DotGlasses.Application.Leads;

public interface ILeadService
{
    Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>ConvertedFlag == false only — backs the Field App's leads worklist and the Admin
    /// Portal's Event History Leads tab conversion action. Hierarchy scoping is automatic (Lead
    /// implements IHierarchyScoped), same as every other list method here.</summary>
    Task<IReadOnlyList<LeadDto>> ListOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>The most recent open Lead for an exact name+phone match, or null. Backs the
    /// "convert this instead?" prompt when recording a Sale for a customer who already has an
    /// unconverted Lead.</summary>
    Task<LeadDto?> FindOpenMatchAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>Idempotent upsert keyed on <see cref="CreateLeadRequest.Id"/>. If
    /// <see cref="CreateLeadRequest.SourceTestId"/> is set, atomically sets that Test's
    /// ConvertedToLeadId in the same transaction; throws DomainRuleViolationException, having
    /// written nothing, when that Test can't be read under the caller's hierarchy scope.
    /// hierarchyPath/technicianUserId come from the authenticated caller, not the request
    /// body.</summary>
    Task<LeadDto> CreateAsync(CreateLeadRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default);
}
