using System.Linq.Expressions;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Reference-data labels come from IReferenceDataSnapshotProvider, which loads retired
/// items too — a historical Lead/referral pointing at a since-retired reason must still render
/// that reason rather than an em-dash, so the Field-App-facing active-only view is the wrong
/// source here.
///
/// Every tab is the same three steps — filter, order newest-first, map — with paging as the only
/// optional one. RunAsync holds those steps once, so the on-screen list and the CSV export of a
/// tab are the same query by construction rather than by two methods agreeing.</summary>
public class EventHistoryQueryService(DotGlassesDbContext dbContext, IReferenceDataSnapshotProvider referenceDataSnapshotProvider, IUnscopedReportQueryService unscopedReportQueryService) : IEventHistoryQueryService
{
    public Task<EventHistoryResult<SaleOrTestEventRow>> ListSalesAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default) =>
        RunAsync(FilterSales(fromUtc, toUtcExclusive), x => x.CreatedAtUtc, paging, MapSalesAsync, cancellationToken);

    public Task<EventHistoryResult<SaleOrTestEventRow>> ListTestsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default) =>
        RunAsync(FilterTests(fromUtc, toUtcExclusive), x => x.CreatedAtUtc, paging, MapTestsAsync, cancellationToken);

    public Task<EventHistoryResult<LeadEventRow>> ListLeadsAsync(string? searchByName, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default) =>
        RunAsync(FilterLeads(searchByName, fromUtc, toUtcExclusive), x => x.CreatedAtUtc, paging, MapLeadsAsync, cancellationToken);

    public Task<EventHistoryResult<ReferralEventRow>> ListReferralsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default)
    {
        // Merged across all three entities (2026-09-03 — "referred or treated" is no longer
        // Test-only). Each Select projects the same shape so Concat becomes one SQL UNION ALL, and
        // ordering/paging happens once after the union rather than per-entity.
        //
        // The projection is an ANONYMOUS type, not the named ReferralSourceRow it converts to
        // below, and that is load-bearing: EF Core treats a projection into a named type as a
        // client projection and then refuses the set operation outright — "Unable to translate set
        // operation after client projection has been applied". An anonymous type is recognised as
        // a server-side projection and translates. Both the Referrals tab and its export threw on
        // every call before this; the EF InMemory provider translated the named-type version
        // happily, so nothing caught it until the tests moved onto real Postgres (ticket 02).
        // The three anonymous projections must keep identical member names, order and types, or
        // they stop being the same type and Concat won't compile.
        var testRows = dbContext.Tests.Where(t => t.ReferredOrTreated)
            .Select(t => new { Source = "Test", t.HierarchyPath, t.ReferralReasonRefId, t.ReferralOtherText, t.TreatedInFacility, t.CreatedAtUtc });
        var leadRows = dbContext.Leads.Where(l => l.ReferredOrTreated)
            .Select(l => new { Source = "Lead", l.HierarchyPath, l.ReferralReasonRefId, l.ReferralOtherText, l.TreatedInFacility, l.CreatedAtUtc });
        var saleRows = dbContext.Sales.Where(s => s.ReferredOrTreated)
            .Select(s => new { Source = "Sale", s.HierarchyPath, s.ReferralReasonRefId, s.ReferralOtherText, s.TreatedInFacility, s.CreatedAtUtc });

        var query = testRows.Concat(leadRows).Concat(saleRows);
        if (fromUtc is { } from) query = query.Where(x => x.CreatedAtUtc >= from);
        if (toUtcExclusive is { } to) query = query.Where(x => x.CreatedAtUtc < to);

        return RunAsync(
            query,
            x => x.CreatedAtUtc,
            paging,
            (rows, ct) => MapReferralsAsync(
                rows.Select(r => new ReferralSourceRow(r.Source, r.HierarchyPath, r.ReferralReasonRefId, r.ReferralOtherText, r.TreatedInFacility, r.CreatedAtUtc)).ToList(),
                ct),
            cancellationToken);
    }

    /// <summary>The one shape every tab runs. Only the Skip/Take differs between a screen page and
    /// an export, which is what stops an export ever seeing rows the screen would not have — the
    /// filter (and with it the global hierarchy-scoping query filter) is the same IQueryable in
    /// both cases. Unpaged skips the COUNT: every matching row comes back, so the row count is the
    /// total, and a second round trip would only re-derive it.</summary>
    private static async Task<EventHistoryResult<TRow>> RunAsync<TSource, TRow>(
        IQueryable<TSource> filtered,
        Expression<Func<TSource, DateTimeOffset>> newestFirstBy,
        PageRequest? paging,
        Func<List<TSource>, CancellationToken, Task<List<TRow>>> mapAsync,
        CancellationToken cancellationToken)
    {
        var ordered = filtered.OrderByDescending(newestFirstBy);

        if (paging is null)
        {
            var all = await ordered.ToListAsync(cancellationToken);
            return new EventHistoryResult<TRow>(await mapAsync(all, cancellationToken), all.Count);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var page = await ordered.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new EventHistoryResult<TRow>(await mapAsync(page, cancellationToken), totalCount);
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
    /// <summary>The materialised shape of one unioned referral row. Deliberately NOT the type the
    /// union projects into — see ListReferralsAsync for why that has to be anonymous. This exists
    /// so MapReferralsAsync has a named parameter type to read against.</summary>
    private sealed record ReferralSourceRow(string Source, string HierarchyPath, Guid? ReferralReasonRefId, string? ReferralOtherText, bool TreatedInFacility, DateTimeOffset CreatedAtUtc);
}
