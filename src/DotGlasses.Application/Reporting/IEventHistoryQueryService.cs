namespace DotGlasses.Application.Reporting;

/// <summary>Read-only — backs the Admin Portal's Event History screen. Hierarchy scoping is
/// automatic (Test/Lead/Sale/Customer/OrganisationNode all implement IHierarchyScoped), so every
/// method here just needs to query normally, no unscoped lookups. Newest-first ordering
/// throughout. Paged (2026-08-05 — real Test/Lead/Sale volume genuinely grows in production,
/// unlike Reference Data/Organisations' naturally small, bounded lists) — page is 1-based.</summary>
public interface IEventHistoryQueryService
{
    Task<PagedResult<SaleOrTestEventRow>> ListSalesAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<SaleOrTestEventRow>> ListTestsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>searchByName filters by the linked Customer's FullName (case-insensitive
    /// contains); null/empty returns everything. Filtering happens before paging (a DB-level
    /// subquery on Customer, not an in-memory filter after the page is loaded).</summary>
    Task<PagedResult<LeadEventRow>> ListLeadsAsync(string? searchByName, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Test rows where Outcome == Referred — a filtered view of the same data
    /// ListTestsAsync shows unfiltered, not a separate entity.</summary>
    Task<PagedResult<ReferralEventRow>> ListReferralsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public record SaleOrTestEventRow(string Type, bool Custom, string Name, string Outlet, string Country, DateTimeOffset CreatedAtUtc);
public record LeadEventRow(string Name, string PhoneMasked, string Outlet, string Reason, DateTimeOffset CreatedAtUtc);
public record ReferralEventRow(string Outlet, string Country, string Reason, DateTimeOffset CreatedAtUtc);

/// <summary>Page is 1-based. TotalPages is 0 when TotalCount is 0 (an empty tab shows no pager,
/// not a single empty page).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
