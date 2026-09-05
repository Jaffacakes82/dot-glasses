using DotGlasses.Application.Customers;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Tests.Fakes;

/// <summary>
/// Dictionary-backed stand-in for the EF repository. The match is exact on all three of
/// hierarchy path, full name and phone number — no fuzzy matching, by design — and C# equality
/// treats two nulls as equal, which is what EF Core's null semantics produce for the real query.
/// </summary>
public class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _store = [];

    public void Seed(Customer entity) => _store[entity.Id] = entity;

    public int Count => _store.Count;

    public IReadOnlyList<Customer> All => _store.Values.ToList();

    public Task<Customer?> FindByNameAndPhoneAsync(
        string hierarchyPath,
        string fullName,
        string? phoneNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(x =>
            x.HierarchyPath == hierarchyPath && x.FullName == fullName && x.PhoneNumber == phoneNumber));

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyDictionary<Guid, Customer>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToList();
        return Task.FromResult<IReadOnlyDictionary<Guid, Customer>>(
            _store.Values.Where(x => distinctIds.Contains(x.Id)).ToDictionary(x => x.Id));
    }

    public void Add(Customer entity) => _store[entity.Id] = entity;
}
