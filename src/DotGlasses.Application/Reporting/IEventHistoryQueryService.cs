namespace DotGlasses.Application.Reporting;

/// <summary>Read-only — backs the Admin Portal's Event History screen and its CSV export.
/// Hierarchy scoping is automatic (Test/Lead/Sale/Customer/OrganisationNode all implement
/// IHierarchyScoped), so every method here just needs to query normally, no unscoped lookups.
/// Newest-first ordering throughout.
///
/// One method per screen tab, paging optional (2026-09-05). The screen supplies a
/// <see cref="PageRequest"/>; the CSV export supplies null and gets every matching row. There is
/// deliberately no Export* counterpart any more: this used to be eight methods — a List* and an
/// Export* per tab, documented as "the same query, just unpaged" and held to that only by whoever
/// edited them next. An export that is literally the same call with paging omitted cannot drift
/// from the list, and cannot return a row the screen would have withheld, because the filtering
/// and the hierarchy scoping are one query rather than two that agree.
///
/// Paging stayed optional rather than collapsing further to a single method: the four tabs return
/// genuinely different row shapes and the language offers no union type to return them from one
/// method without a cast at every call site.</summary>
public interface IEventHistoryQueryService
{
    /// <summary>fromUtc/toUtcExclusive filter on CreatedAtUtc; either or both may be null (no
    /// bound on that side). toUtcExclusive is exclusive — the Web layer is responsible for
    /// turning an inclusive "to" date into the next day's midnight before calling this.</summary>
    Task<EventHistoryResult<SaleOrTestEventRow>> ListSalesAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default);

    Task<EventHistoryResult<SaleOrTestEventRow>> ListTestsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default);

    /// <summary>searchByName filters by the linked Customer's FullName (case-insensitive
    /// ILIKE); null/empty returns everything. Filtering happens before paging (a DB-level
    /// subquery on Customer, not an in-memory filter after the page is loaded).</summary>
    Task<EventHistoryResult<LeadEventRow>> ListLeadsAsync(string? searchByName, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default);

    /// <summary>Test/Lead/Sale rows where ReferredOrTreated is true (2026-09-03 — "referred or
    /// treated" is an orthogonal flag on all three, no longer tied to Test.Outcome) — a filtered,
    /// merged view of the same underlying data ListTestsAsync/ListLeadsAsync/ListSalesAsync show
    /// unfiltered, not a separate entity. The same real-world referral may legitimately appear
    /// more than once if it was (re)recorded at more than one stage of a converting journey.</summary>
    Task<EventHistoryResult<ReferralEventRow>> ListReferralsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, PageRequest? paging, CancellationToken cancellationToken = default);
}

/// <summary>What a tab's query returns: the rows asked for, and how many matched the filter
/// altogether. Deliberately not <see cref="PagedResult{T}"/> — that record also carries Page and
/// PageSize, which an unpaged call has no honest value for (nor would echoing back what the
/// caller passed tell it anything). A paged caller already holds its own <see cref="PageRequest"/>
/// and turns TotalCount into a page count through it; an unpaged caller gets every matching row,
/// so TotalCount is simply Rows.Count.</summary>
public record EventHistoryResult<T>(IReadOnlyList<T> Rows, int TotalCount);

/// <summary>Name/ConsentGiven are null for a Test row — Tests stay deliberately anonymous (no
/// name/phone captured at all) and carry no consent concept.</summary>
public record SaleOrTestEventRow(string Type, bool Custom, string? Name, string Outlet, string Country, DateTimeOffset CreatedAtUtc, bool? ConsentGiven);

/// <summary>Id/ConvertedFlag back the Admin Portal's Leads tab conversion action (Phase 4) — a
/// row needs its own Lead Id to link to the conversion form, and ConvertedFlag to know whether
/// to show "Convert to sale" or an already-converted state.</summary>
public record LeadEventRow(Guid Id, string Name, string PhoneMasked, string Outlet, string Reason, DateTimeOffset CreatedAtUtc, bool ConsentGiven, bool ConvertedFlag);
/// <summary>Source is "Test"/"Lead"/"Sale" — which entity this referral/treatment was recorded
/// against, since the same real-world event may be logged at more than one stage.</summary>
public record ReferralEventRow(string Source, string Outlet, string Country, string Reason, bool TreatedInFacility, DateTimeOffset CreatedAtUtc);
