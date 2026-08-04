using DotGlasses.Contracts.Leads;

namespace DotGlasses.Application.Leads;

public interface ILeadService
{
    Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Idempotent upsert keyed on <see cref="CreateLeadRequest.Id"/>. If
    /// <see cref="CreateLeadRequest.SourceTestId"/> is set, atomically sets that Test's
    /// ConvertedToLeadId in the same transaction. hierarchyPath/technicianUserId come from the
    /// authenticated caller, not the request body.</summary>
    Task<LeadDto> CreateAsync(CreateLeadRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default);
}
