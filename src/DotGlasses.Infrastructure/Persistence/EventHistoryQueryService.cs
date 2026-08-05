using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class EventHistoryQueryService(DotGlassesDbContext dbContext, IReferenceDataAdminService referenceDataAdminService, IUnscopedReportQueryService unscopedReportQueryService) : IEventHistoryQueryService
{
    public async Task<IReadOnlyList<SaleOrTestEventRow>> ListSalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await dbContext.Sales.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var customers = await GetCustomersByIdAsync(sales.Select(s => (Guid?)s.CustomerId), cancellationToken);
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);

        return sales.Select(s =>
        {
            var (outlet, country) = orgLookup.Resolve(s.HierarchyPath);
            return new SaleOrTestEventRow("Sale", s.LensRangeType == LensRangeType.Custom, CustomerName(customers, s.CustomerId), outlet, country, s.CreatedAtUtc);
        }).ToList();
    }

    public async Task<IReadOnlyList<SaleOrTestEventRow>> ListTestsAsync(CancellationToken cancellationToken = default)
    {
        var tests = await dbContext.Tests.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var customers = await GetCustomersByIdAsync(tests.Select(t => t.CustomerId), cancellationToken);
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);

        return tests.Select(t =>
        {
            var (outlet, country) = orgLookup.Resolve(t.HierarchyPath);
            return new SaleOrTestEventRow("Test", false, CustomerName(customers, t.CustomerId), outlet, country, t.CreatedAtUtc);
        }).ToList();
    }

    public async Task<IReadOnlyList<LeadEventRow>> ListLeadsAsync(string? searchByName, CancellationToken cancellationToken = default)
    {
        var leads = await dbContext.Leads.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var customers = await GetCustomersByIdAsync(leads.Select(l => (Guid?)l.CustomerId), cancellationToken);
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);
        var refData = await BuildReferenceDataLookupAsync(cancellationToken);

        var rows = leads.Select(l =>
        {
            var customer = customers.GetValueOrDefault(l.CustomerId);
            var (outlet, _) = orgLookup.Resolve(l.HierarchyPath);
            var reason = refData.Resolve(l.ReasonNotPurchasedRefId, l.ReasonNotPurchasedOtherText);
            return new LeadEventRow(customer?.FullName ?? "—", MaskPhone(customer?.PhoneNumber), outlet, reason, l.CreatedAtUtc);
        });

        if (!string.IsNullOrWhiteSpace(searchByName))
        {
            rows = rows.Where(r => r.Name.Contains(searchByName, StringComparison.OrdinalIgnoreCase));
        }

        return rows.ToList();
    }

    public async Task<IReadOnlyList<ReferralEventRow>> ListReferralsAsync(CancellationToken cancellationToken = default)
    {
        var referrals = await dbContext.Tests
            .Where(t => t.Outcome == TestOutcome.Referred)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);
        var refData = await BuildReferenceDataLookupAsync(cancellationToken);

        return referrals.Select(t =>
        {
            var (outlet, country) = orgLookup.Resolve(t.HierarchyPath);
            var reason = refData.Resolve(t.ReferralReasonRefId, t.ReferralOtherText);
            return new ReferralEventRow(outlet, country, reason, t.CreatedAtUtc);
        }).ToList();
    }

    private async Task<Dictionary<Guid, Customer>> GetCustomersByIdAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var distinctIds = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var customers = await dbContext.Customers.Where(c => distinctIds.Contains(c.Id)).ToListAsync(cancellationToken);
        return customers.ToDictionary(c => c.Id);
    }

    private static string CustomerName(IReadOnlyDictionary<Guid, Customer> customers, Guid? customerId) =>
        customerId.HasValue && customers.TryGetValue(customerId.Value, out var customer) ? customer.FullName : "—";

    /// <summary>Keeps the first 4 / last 3 characters, redacts the rest with a fixed run of "•"
    /// — privacy-positive default matching the design mockup's clear intent, without over-fitting
    /// to one country's exact phone-number shape (the mockup's sample data hardcodes an
    /// already-masked string per row rather than a real masking function to copy).</summary>
    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "—";
        }

        if (phone.Length <= 7)
        {
            return phone;
        }

        return $"{phone[..4]}••••{phone[^3..]}";
    }

    /// <summary>Goes through IUnscopedReportQueryService, not a plain scoped OrganisationNodes
    /// query — a caller scoped below Country level can never see their own Country ancestor via
    /// the standard hierarchy filter (it only ever shows a caller their own subtree), so a plain
    /// query silently resolved every outlet's country to "Unknown country" for anyone below
    /// Country level (2026-08-05 fix, caught while building the Dashboard's identical org
    /// resolution — see CLAUDE.md).</summary>
    private async Task<OrgLookup> BuildOrgLookupAsync(CancellationToken cancellationToken)
    {
        var nodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        return new OrgLookup(nodes);
    }

    private async Task<ReferenceDataLookup> BuildReferenceDataLookupAsync(CancellationToken cancellationToken)
    {
        // ListAllAsync (not the Field-App-facing ListActiveAsync) so a historical event
        // referencing a since-retired reference-data item still resolves a label instead of
        // silently failing.
        var items = await referenceDataAdminService.ListAllAsync(cancellationToken);
        return new ReferenceDataLookup(items);
    }

    private sealed class OrgLookup(IReadOnlyList<OrganisationNodeSummary> nodes)
    {
        private readonly Dictionary<string, OrganisationNodeSummary> _byPath = nodes.ToDictionary(n => n.HierarchyPath);
        private readonly IReadOnlyList<OrganisationNodeSummary> _countries = nodes.Where(n => n.Level == OrganisationLevel.Country).ToList();

        public (string Outlet, string Country) Resolve(string hierarchyPath)
        {
            var outlet = _byPath.TryGetValue(hierarchyPath, out var node) ? node.Name : "Unknown outlet";
            var country = _countries.FirstOrDefault(c => hierarchyPath.StartsWith(c.HierarchyPath, StringComparison.Ordinal))?.Name ?? "Unknown country";
            return (outlet, country);
        }
    }

    private sealed class ReferenceDataLookup(IReadOnlyList<ReferenceDataAdminItem> items)
    {
        private readonly Dictionary<Guid, ReferenceDataAdminItem> _byId = items.ToDictionary(i => i.Id);

        public string Resolve(Guid? refId, string? otherText)
        {
            if (refId is null || !_byId.TryGetValue(refId.Value, out var item))
            {
                return "—";
            }

            return item.IsOtherOption && !string.IsNullOrWhiteSpace(otherText) ? otherText : item.Label;
        }
    }
}
