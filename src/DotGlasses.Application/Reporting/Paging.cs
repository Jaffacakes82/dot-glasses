namespace DotGlasses.Application.Reporting;

/// <summary>Which page of a result set a caller wants — 1-based, matching the page numbers the
/// Admin Portal's pagers put in their URLs. Passing one is what makes a query paged; a query that
/// takes a <c>PageRequest?</c> and is given null returns every matching row instead (see
/// IEventHistoryQueryService).</summary>
public record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;

    /// <summary>0 when nothing matched — an empty tab shows no pager, not a single empty page.
    /// Lives here rather than on a result type because PageSize is what decides how many pages a
    /// count spans, and only a caller that asked for pages has one.</summary>
    public int TotalPages(int totalCount) => totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / PageSize);
}

/// <summary>Page is 1-based. TotalPages is 0 when TotalCount is 0 (an empty list shows no pager,
/// not a single empty page).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
