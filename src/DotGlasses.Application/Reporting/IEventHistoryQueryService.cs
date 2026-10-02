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
/// name/phone captured at all) and carry no consent concept.
///
/// IsTraining marks a row at a training organisation or beneath one — the rows the Dashboard
/// leaves out, so a reviewer can see why the two screens disagree. Lens is the record's lens
/// (Sales only on this shape; a Test row has none).</summary>
public record SaleOrTestEventRow(string Type, bool Custom, string? Name, string Outlet, string Country, DateTimeOffset CreatedAtUtc, bool? ConsentGiven, bool IsTraining = false, EventLens? Lens = null);

/// <summary>What a Sale or Lead recorded about its lens, as Event History shows and exports it.
/// Range is the lens set's name or "Custom"; null when the record chose no lens range. Each eye
/// is null when it has no power. Coatings is the coatings' names in one string ("A; B"), empty
/// when there are none — a string rather than a list so two rows holding the same lens compare
/// equal, which the list and the export sharing one row shape relies on.</summary>
public record EventLens(string? Range, EventEyePower? Left, EventEyePower? Right, string? LensType, string Coatings)
{
    public static readonly EventLens None = new(null, null, null, null, string.Empty);

    public static string JoinCoatings(IEnumerable<string> names) => string.Join("; ", names);
}

/// <summary>One eye's lens power. <see cref="Formatted"/> is the one display format a lens power
/// has (LensPowerValues.FormatLensPower, ADR-0007) — nothing restates it.</summary>
public record EventEyePower(decimal Sphere, decimal? Cylinder, decimal? Axis, decimal? Add)
{
    public string Formatted => DotGlasses.Rules.LensPowers.LensPowerValues.FormatLensPower(Sphere, Cylinder, Axis, Add);

    public static EventEyePower? From(decimal? sphere, decimal? cylinder, decimal? axis, decimal? add) =>
        sphere is { } value ? new EventEyePower(value, cylinder, axis, add) : null;
}

/// <summary>Id/ConvertedFlag back the Admin Portal's Leads tab conversion action (Phase 4) — a
/// row needs its own Lead Id to link to the conversion form, and ConvertedFlag to know whether
/// to show "Convert to sale" or an already-converted state.
///
/// CustomerToldPrice is the Lead's answer to "Has the customer been told the price?" — null for a
/// Lead recorded before the question was asked.</summary>
public record LeadEventRow(Guid Id, string Name, string PhoneMasked, string Outlet, string Reason, DateTimeOffset CreatedAtUtc, bool ConsentGiven, bool ConvertedFlag, bool IsTraining = false, EventLens? Lens = null, bool? CustomerToldPrice = null);
/// <summary>Source is "Test"/"Lead"/"Sale" — which entity this referral/treatment was recorded
/// against, since the same real-world event may be logged at more than one stage.</summary>
public record ReferralEventRow(string Source, string Outlet, string Country, string Reason, bool TreatedInFacility, DateTimeOffset CreatedAtUtc, bool IsTraining = false);
