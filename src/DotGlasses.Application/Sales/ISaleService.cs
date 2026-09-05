using DotGlasses.Contracts.Sales;

namespace DotGlasses.Application.Sales;

public interface ISaleService
{
    Task<SaleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Idempotent upsert keyed on <see cref="CreateSaleRequest.Id"/>. If
    /// <see cref="CreateSaleRequest.SourceLeadId"/> is set, atomically sets that Lead's
    /// ConvertedFlag/SaleId in the same transaction; throws DomainRuleViolationException, having
    /// written nothing, when that Lead can't be read under the caller's hierarchy scope.
    /// CoatingRefIds is persisted as-is (validated
    /// by ConsultationRules, not re-derived here) — see ADR-0001. hierarchyPath/
    /// technicianUserId come from the authenticated caller, not the request body.</summary>
    Task<SaleDto> CreateAsync(CreateSaleRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default);
}
