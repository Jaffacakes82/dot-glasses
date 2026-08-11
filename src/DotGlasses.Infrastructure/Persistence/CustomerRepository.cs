using DotGlasses.Application.Customers;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class CustomerRepository(DotGlassesDbContext dbContext) : ICustomerRepository
{
    public async Task<Customer?> FindByNameAndPhoneAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken = default) =>
        await dbContext.Customers.FirstOrDefaultAsync(
            x => x.HierarchyPath == hierarchyPath && x.FullName == fullName && x.PhoneNumber == phoneNumber,
            cancellationToken);

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Customer>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, Customer>();
        }

        var customers = await dbContext.Customers.Where(c => distinctIds.Contains(c.Id)).ToListAsync(cancellationToken);
        return customers.ToDictionary(c => c.Id);
    }

    public void Add(Customer entity) => dbContext.Customers.Add(entity);
}
