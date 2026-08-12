using DotGlasses.Application.CustomOrders;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DomainFulfilmentStatus = DotGlasses.Domain.Enums.FulfilmentStatus;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — matches
/// EventHistoryQueryService/PresetCatalogueAdminService (a bespoke read + one write action, no
/// repository interface needed for this shape).</summary>
public class CustomOrderService(DotGlassesDbContext dbContext) : ICustomOrderService
{
    public async Task<PagedResult<CustomOrderRow>> ListAsync(DomainFulfilmentStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Sales.Where(x => x.FulfilmentStatus != null);
        if (status is { } value)
        {
            query = query.Where(x => x.FulfilmentStatus == value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var sales = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var customerIds = sales.Select(s => s.CustomerId).Distinct().ToList();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var orgByPath = (await dbContext.OrganisationNodes.ToListAsync(cancellationToken))
            .ToDictionary(n => n.HierarchyPath);

        var items = sales.Select(s => new CustomOrderRow(
            s.Id,
            customers.TryGetValue(s.CustomerId, out var customer) ? customer.FullName : "—",
            orgByPath.TryGetValue(s.HierarchyPath, out var org) ? org.Name : "Unknown outlet",
            FormatPrescription(s),
            s.FulfilmentStatus!.Value,
            s.CreatedAtUtc))
            .ToList();

        return new PagedResult<CustomOrderRow>(items, totalCount, page, pageSize);
    }

    public async Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await dbContext.Sales.FirstAsync(x => x.Id == saleId, cancellationToken);
        if (sale.FulfilmentStatus is not { } current)
        {
            throw new InvalidOperationException("This Sale is not a custom order routed to fulfilment.");
        }

        sale.FulfilmentStatus = current switch
        {
            DomainFulfilmentStatus.Submitted => DomainFulfilmentStatus.InLab,
            DomainFulfilmentStatus.InLab => DomainFulfilmentStatus.ReadyForPickup,
            DomainFulfilmentStatus.ReadyForPickup => DomainFulfilmentStatus.Fulfilled,
            DomainFulfilmentStatus.Fulfilled => throw new InvalidOperationException("This custom order is already Fulfilled."),
            _ => throw new ArgumentOutOfRangeException(nameof(current), current, null),
        };

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string FormatPrescription(Sale s) =>
        $"OD {FormatEye(s.CustomSphereRight, s.CustomCylinderRight, s.CustomAddPowerRight)} / OS {FormatEye(s.CustomSphereLeft, s.CustomCylinderLeft, s.CustomAddPowerLeft)}";

    private static string FormatEye(decimal? sphere, decimal? cylinder, decimal? addPower)
    {
        var parts = new List<string> { FormatPower(sphere ?? 0m) };
        if (cylinder is { } cyl && cyl != 0m)
        {
            parts.Add($"cyl {FormatPower(cyl)}");
        }

        if (addPower is { } add && add != 0m)
        {
            parts.Add($"add {FormatPower(add)}");
        }

        return string.Join(" ", parts);
    }

    private static string FormatPower(decimal v) => v >= 0 ? $"+{v:0.00}" : v.ToString("0.00");
}
