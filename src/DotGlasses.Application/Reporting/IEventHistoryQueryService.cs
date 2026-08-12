namespace DotGlasses.Application.Reporting;

/// <summary>Read-only — backs the Admin Portal's Event History screen. Hierarchy scoping is
/// automatic (Test/Lead/Sale/Customer/OrganisationNode all implement IHierarchyScoped), so every
/// method here just needs to query normally, no unscoped lookups. Newest-first ordering
/// throughout. Paged (2026-08-05 — real Test/Lead/Sale volume genuinely grows in production,
/// unlike Reference Data/Organisations' naturally small, bounded lists) — page is 1-based.</summary>
public interface IEventHistoryQueryService
{
    /// <summary>fromUtc/toUtcExclusive filter on CreatedAtUtc; either or both may be null (no
    /// bound on that side). toUtcExclusive is exclusive — the Web layer is responsible for
    /// turning an inclusive "to" date into the next day's midnight before calling this.</summary>
    Task<PagedResult<SaleOrTestEventRow>> ListSalesAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<SaleOrTestEventRow>> ListTestsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>searchByName filters by the linked Customer's FullName (case-insensitive
    /// ILIKE); null/empty returns everything. Filtering happens before paging (a DB-level
    /// subquery on Customer, not an in-memory filter after the page is loaded).</summary>
    Task<PagedResult<LeadEventRow>> ListLeadsAsync(string? searchByName, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Test rows where Outcome == Referred — a filtered view of the same data
    /// ListTestsAsync shows unfiltered, not a separate entity.</summary>
    Task<PagedResult<ReferralEventRow>> ListReferralsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>Name/ConsentGiven are null for a Test row — Tests stay deliberately anonymous (no
/// name/phone captured at all) and carry no consent concept.</summary>
public record SaleOrTestEventRow(string Type, bool Custom, string? Name, string Outlet, string Country, DateTimeOffset CreatedAtUtc, bool? ConsentGiven);

/// <summary>Id/ConvertedFlag back the Admin Portal's Leads tab conversion action (Phase 4) — a
/// row needs its own Lead Id to link to the conversion form, and ConvertedFlag to know whether
/// to show "Convert to sale" or an already-converted state.</summary>
public record LeadEventRow(Guid Id, string Name, string PhoneMasked, string Outlet, string Reason, DateTimeOffset CreatedAtUtc, bool ConsentGiven, bool ConvertedFlag);
public record ReferralEventRow(string Outlet, string Country, string Reason, DateTimeOffset CreatedAtUtc);

/// <summary>Page is 1-based. TotalPages is 0 when TotalCount is 0 (an empty tab shows no pager,
/// not a single empty page).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
