using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Customers;

/// <summary>
/// No public Contracts/API surface in this pass — used internally by LeadService/SaleService for
/// exact-match find-or-create ("don't create a duplicate Customer row for a repeat name+phone").
/// Fuzzy/suggested-match UX is Field App UI work for later.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> FindByNameAndPhoneAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup for a list of Leads/Sales resolving customer name/phone in one
    /// query, same rationale as EventHistoryQueryService's own GetCustomersByIdAsync.</summary>
    Task<IReadOnlyDictionary<Guid, Customer>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    void Add(Customer entity);
}
