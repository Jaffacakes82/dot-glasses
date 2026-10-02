using DotGlasses.Contracts.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.CustomOrders;

/// <summary>What LeadService and SaleService need of the custom order record: placing one in the
/// same unit of work as the Lead or Sale, and finding the one a record already has. Add/Update
/// only track changes (no auto-save) — see IVisionTestRepository.</summary>
public interface ICustomOrderRepository
{
    /// <summary>The order each of these Leads placed, keyed by Lead id. A Lead with none is absent.</summary>
    Task<IReadOnlyDictionary<Guid, CustomOrder>> GetByLeadIdsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken = default);

    /// <summary>The order each of these Sales is linked to, keyed by Sale id.</summary>
    Task<IReadOnlyDictionary<Guid, CustomOrder>> GetBySaleIdsAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken cancellationToken = default);

    void Add(CustomOrder entity);

    void Update(CustomOrder entity);
}

public static class CustomOrderStatusMapping
{
    public static CustomOrderStatus ToContract(this FulfilmentStatus status) => status switch
    {
        FulfilmentStatus.Submitted => CustomOrderStatus.Submitted,
        FulfilmentStatus.InLab => CustomOrderStatus.InLab,
        FulfilmentStatus.ReadyForPickup => CustomOrderStatus.ReadyForPickup,
        FulfilmentStatus.Fulfilled => CustomOrderStatus.Fulfilled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
