using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Reference-data labels come from IReferenceDataSnapshotProvider, which loads retired
/// items too — a historical Lead/referral pointing at a since-retired reason must still render
/// that reason rather than an em-dash, so the Field-App-facing active-only view is the wrong
/// source here.</summary>
public class EventHistoryQueryService(DotGlassesDbContext dbContext, IReferenceDataSnapshotProvider referenceDataSnapshotProvider, IUnscopedReportQueryService unscopedReportQueryService) : IEventHistoryQueryService
{
    public async Task<PagedResult<SaleOrTestEventRow>> ListSalesAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = FilterSales(fromUtc, toUtcExclusive);
        var totalCount = await query.CountAsync(cancellationToken);
        var sales = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = await MapSalesAsync(sales, cancellationToken);
        return new PagedResult<SaleOrTestEventRow>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<SaleOrTestEventRow>> ExportSalesAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken = default)
    {
        var sales = await FilterSales(fromUtc, toUtcExclusive).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return await MapSalesAsync(sales, cancellationToken);
    }

    public async Task<PagedResult<SaleOrTestEventRow>> ListTestsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = FilterTests(fromUtc, toUtcExclusive);
        var totalCount = await query.CountAsync(cancellationToken);
        var tests = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = await MapTestsAsync(tests, cancellationToken);
        return new PagedResult<SaleOrTestEventRow>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<SaleOrTestEventRow>> ExportTestsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken = default)
    {
        var tests = await FilterTests(fromUtc, toUtcExclusive).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return await MapTestsAsync(tests, cancellationToken);
    }

    public async Task<PagedResult<LeadEventRow>> ListLeadsAsync(string? searchByName, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = FilterLeads(searchByName, fromUtc, toUtcExclusive);
        var totalCount = await query.CountAsync(cancellationToken);
        var leads = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = await MapLeadsAsync(leads, cancellationToken);
        return new PagedResult<LeadEventRow>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<LeadEventRow>> ExportLeadsAsync(string? searchByName, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken = default)
    {
        var leads = await FilterLeads(searchByName, fromUtc, toUtcExclusive).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return await MapLeadsAsync(leads, cancellationToken);
    }

    public async Task<PagedResult<ReferralEventRow>> ListReferralsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = FilterReferrals(fromUtc, toUtcExclusive);
        var totalCount = await query.CountAsync(cancellationToken);
        var referrals = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = await MapReferralsAsync(referrals, cancellationToken);
        return new PagedResult<ReferralEventRow>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<ReferralEventRow>> ExportReferralsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken = default)
    {
        var referrals = await FilterReferrals(fromUtc, toUtcExclusive).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return await MapReferralsAsync(referrals, cancellationToken);
    }

    private IQueryable<Sale> FilterSales(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive)
    {
        var query = dbContext.Sales.AsQueryable();
        if (fromUtc is { } from) query = query.Where(x => x.CreatedAtUtc >= from);
        if (toUtcExclusive is { } to) query = query.Where(x => x.CreatedAtUtc < to);
        return query;
    }

    private async Task<List<SaleOrTestEventRow>> MapSalesAsync(List<Sale> sales, CancellationToken cancellationToken)
    {
        var customers = await GetCustomersByIdAsync(sales.Select(s => (Guid?)s.CustomerId), cancellationToken);
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);

        return sales.Select(s =>
        {
            var (outlet, country) = Resolve(orgLookup, s.HierarchyPath);
            return new SaleOrTestEventRow("Sale", s.LensRangeType == LensRangeType.Custom, CustomerName(customers, s.CustomerId), outlet, country, s.CreatedAtUtc, s.ConsentGiven);
        }).ToList();
    }

    private IQueryable<Test> FilterTests(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive)
    {
        var query = dbContext.Tests.AsQueryable();
        if (fromUtc is { } from) query = query.Where(x => x.CreatedAtUtc >= from);
        if (toUtcExclusive is { } to) query = query.Where(x => x.CreatedAtUtc < to);
        return query;
    }

    /// <summary>Tests stay deliberately anonymous (no name/phone captured on the Test form at
    /// all — see CLAUDE.md's Phase 3 notes), so unlike MapSalesAsync there is no customer lookup
    /// here at all.</summary>
    private async Task<List<SaleOrTestEventRow>> MapTestsAsync(List<Test> tests, CancellationToken cancellationToken)
    {
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);
        return tests.Select(t =>
        {
            var (outlet, country) = Resolve(orgLookup, t.HierarchyPath);
            return new SaleOrTestEventRow("Test", false, Name: null, outlet, country, t.CreatedAtUtc, ConsentGiven: null);
        }).ToList();
    }

    private IQueryable<Lead> FilterLeads(string? searchByName, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive)
    {
        var query = dbContext.Leads.AsQueryable();
        if (fromUtc is { } from) query = query.Where(x => x.CreatedAtUtc >= from);
        if (toUtcExclusive is { } to) query = query.Where(x => x.CreatedAtUtc < to);

        if (!string.IsNullOrWhiteSpace(searchByName))
        {
            // A DB-level subquery on Customer rather than an in-memory filter after loading — the
            // filter must apply before paging, or "page 2" would silently mean something
            // different depending on how many rows on page 1 the search happened to exclude.
            // EF.Functions.ILike translates to Postgres ILIKE — plain .Contains() translates to
            // LIKE, which is case-sensitive absent a citext column/collation (neither exists in
            // this schema), so a search for "jane" would silently miss a stored "Jane Doe".
            var matchingCustomerIds = dbContext.Customers
                .Where(c => EF.Functions.ILike(c.FullName, $"%{searchByName}%"))
                .Select(c => c.Id);
            query = query.Where(l => matchingCustomerIds.Contains(l.CustomerId));
        }

        return query;
    }

    private async Task<List<LeadEventRow>> MapLeadsAsync(List<Lead> leads, CancellationToken cancellationToken)
    {
        var customers = await GetCustomersByIdAsync(leads.Select(l => (Guid?)l.CustomerId), cancellationToken);
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);
        var referenceData = await referenceDataSnapshotProvider.GetAsync(cancellationToken);

        return leads.Select(l =>
        {
            var customer = customers.GetValueOrDefault(l.CustomerId);
            var (outlet, _) = Resolve(orgLookup, l.HierarchyPath);
            var reason = referenceData.ResolveLabel(l.ReasonNotPurchasedRefId, l.ReasonNotPurchasedOtherText);
            return new LeadEventRow(l.Id, customer?.FullName ?? "—", MaskPhone(customer?.PhoneNumber), outlet, reason, l.CreatedAtUtc, l.ConsentGiven, l.ConvertedFlag);
        }).ToList();
    }

    private IQueryable<ReferralSourceRow> FilterReferrals(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive)
    {
        // Merged across all three entities (2026-09-03 — "referred or treated" is no longer
        // Test-only). Each Select projects to the same ReferralSourceRow shape so Concat
        // translates to a single SQL UNION ALL — ordering/paging happens once, after the union,
        // not per-entity. A named record (not an anonymous type) is required here so
        // FilterReferrals/MapReferralsAsync have a concrete type to share as their signature.
        var testRows = dbContext.Tests.Where(t => t.ReferredOrTreated)
            .Select(t => new ReferralSourceRow("Test", t.HierarchyPath, t.ReferralReasonRefId, t.ReferralOtherText, t.TreatedInFacility, t.CreatedAtUtc));
        var leadRows = dbContext.Leads.Where(l => l.ReferredOrTreated)
            .Select(l => new ReferralSourceRow("Lead", l.HierarchyPath, l.ReferralReasonRefId, l.ReferralOtherText, l.TreatedInFacility, l.CreatedAtUtc));
        var saleRows = dbContext.Sales.Where(s => s.ReferredOrTreated)
            .Select(s => new ReferralSourceRow("Sale", s.HierarchyPath, s.ReferralReasonRefId, s.ReferralOtherText, s.TreatedInFacility, s.CreatedAtUtc));

        var query = testRows.Concat(leadRows).Concat(saleRows);
        if (fromUtc is { } from) query = query.Where(x => x.CreatedAtUtc >= from);
        if (toUtcExclusive is { } to) query = query.Where(x => x.CreatedAtUtc < to);
        return query;
    }

    private async Task<List<ReferralEventRow>> MapReferralsAsync(List<ReferralSourceRow> referrals, CancellationToken cancellationToken)
    {
        var orgLookup = await BuildOrgLookupAsync(cancellationToken);
        var referenceData = await referenceDataSnapshotProvider.GetAsync(cancellationToken);

        return referrals.Select(t =>
        {
            var (outlet, country) = Resolve(orgLookup, t.HierarchyPath);
            var reason = referenceData.ResolveLabel(t.ReferralReasonRefId, t.ReferralOtherText);
            return new ReferralEventRow(t.Source, outlet, country, reason, t.TreatedInFacility, t.CreatedAtUtc);
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
    /// resolution — see CLAUDE.md). That "identical resolution" is now literally the same code:
    /// OrgTreeLookup, shared with the Dashboard and Custom Orders (docs/adr/0004).</summary>
    private async Task<OrgTreeLookup> BuildOrgLookupAsync(CancellationToken cancellationToken)
    {
        var nodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        return new OrgTreeLookup(nodes);
    }

    /// <summary>Both names every row on this screen needs, in the one call the mapping methods
    /// already made — Event History shows outlet and country, never a Retailer.</summary>
    private static (string Outlet, string Country) Resolve(OrgTreeLookup orgLookup, string hierarchyPath) =>
        (orgLookup.RowOutletName(hierarchyPath), orgLookup.RowCountryName(hierarchyPath));

    /// <summary>Common projected shape for the Test/Lead/Sale union behind FilterReferrals — see
    /// its doc comment for why this needs to be a named record rather than an anonymous type.</summary>
    private sealed record ReferralSourceRow(string Source, string HierarchyPath, Guid? ReferralReasonRefId, string? ReferralOtherText, bool TreatedInFacility, DateTimeOffset CreatedAtUtc);
}
