using DotGlasses.Application.Dashboard;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — matches
/// EventHistoryQueryService/CustomOrderService (bespoke reporting reads, no repository interface
/// needed for this shape). Org name/level resolution goes through IUnscopedReportQueryService,
/// not a plain scoped OrganisationNodes query — a caller scoped at RetailPoint level can never
/// see their own Country/Intermediate ancestors via the standard hierarchy filter (it only ever
/// shows a caller their own subtree), so a plain query would silently resolve every
/// outlet/retailer/country name to "Unknown" for anyone below Country level. The resolution itself
/// is OrgTreeLookup's, shared with Event History and Custom Orders rather than reimplemented here,
/// so all three screens name the same Retailer for the same retail point (docs/adr/0004). The
/// figures are DashboardCalculator's.</summary>
public class DashboardQueryService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService) : IDashboardQueryService
{
    public async Task<DashboardSnapshot> GetAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orgLookup = new OrgTreeLookup(await unscopedReportQueryService.GetOrganisationNodesForReportsAsync(cancellationToken));

        var tests = await dbContext.Tests.ToListAsync(cancellationToken);
        var leads = await dbContext.Leads.ToListAsync(cancellationToken);
        var sales = await dbContext.Sales.ToListAsync(cancellationToken);
        var orders = await dbContext.CustomOrders.ToListAsync(cancellationToken);

        var technicianNames = await dbContext.Users
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? "—" : u.FullName, cancellationToken);

        var figures = DashboardCalculator.Calculate(tests, leads, sales, orders, orgLookup, technicianNames, filter, DateTimeOffset.UtcNow);

        // What the filters can offer: the scoped (and so active) organisations the caller can
        // see. A caller below Country level sees no Country node of their own, so the Country of
        // each organisation they do see is resolved upward through the lookup.
        var scopedNodes = await dbContext.OrganisationNodes.ToListAsync(cancellationToken);
        var scopedPaths = scopedNodes.Select(n => (Node: n, Path: HierarchyPath.Parse(n.HierarchyPath))).ToList();

        var countries = scopedPaths
            .Select(n => orgLookup.FindCountry(n.Path))
            .Where(c => c is not null)
            .DistinctBy(c => c!.Id)
            .Select(c => new DashboardOrgOption(c!.Id, c.Name))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Narrowed to the chosen Country, so the two dropdowns can't be set to contradict each other.
        var inChosenCountry = scopedPaths
            .Where(n => filter.CountryId is not { } countryId || orgLookup.FindCountry(n.Path)?.Id == countryId)
            .ToList();

        // Every retailer/distributor tier the caller can see, plus — resolved upward, as the
        // Country is — the Retailer over anything they can see, so a retail-point caller is
        // offered their own.
        var retailers = inChosenCountry
            .Where(n => n.Node.Level == OrganisationLevel.Intermediate)
            .Select(n => new DashboardOrgOption(n.Node.Id, n.Node.Name))
            .Concat(inChosenCountry
                .Select(n => orgLookup.ResolveRetailer(n.Path).Node)
                .Where(r => r is not null)
                .Select(r => new DashboardOrgOption(r!.Id, r.Name)))
            .DistinctBy(r => r.Id)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasNoRetailerOption = inChosenCountry.Any(n =>
            n.Node.Level == OrganisationLevel.RetailPoint && orgLookup.ResolveRetailer(n.Path).Kind == RetailerResolutionKind.NoRetailer);

        return new DashboardSnapshot(figures, countries, retailers, hasNoRetailerOption);
    }
}
