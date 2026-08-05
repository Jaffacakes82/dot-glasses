namespace DotGlasses.Application.Reporting;

/// <summary>Read-only — backs the Admin Portal's Event History screen. Hierarchy scoping is
/// automatic (Test/Lead/Sale/Customer/OrganisationNode all implement IHierarchyScoped), so every
/// method here just needs to query normally, no unscoped lookups. Newest-first ordering
/// throughout.</summary>
public interface IEventHistoryQueryService
{
    Task<IReadOnlyList<SaleOrTestEventRow>> ListSalesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleOrTestEventRow>> ListTestsAsync(CancellationToken cancellationToken = default);

    /// <summary>searchByName filters by the linked Customer's FullName (case-insensitive
    /// contains); null/empty returns everything.</summary>
    Task<IReadOnlyList<LeadEventRow>> ListLeadsAsync(string? searchByName, CancellationToken cancellationToken = default);

    /// <summary>Test rows where Outcome == Referred — a filtered view of the same data
    /// ListTestsAsync shows unfiltered, not a separate entity.</summary>
    Task<IReadOnlyList<ReferralEventRow>> ListReferralsAsync(CancellationToken cancellationToken = default);
}

public record SaleOrTestEventRow(string Type, bool Custom, string Name, string Outlet, string Country, DateTimeOffset CreatedAtUtc);
public record LeadEventRow(string Name, string PhoneMasked, string Outlet, string Reason, DateTimeOffset CreatedAtUtc);
public record ReferralEventRow(string Outlet, string Country, string Reason, DateTimeOffset CreatedAtUtc);
