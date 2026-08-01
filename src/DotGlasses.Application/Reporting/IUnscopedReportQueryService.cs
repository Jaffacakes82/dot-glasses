using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Reporting;

/// <summary>
/// The one sanctioned way to look outside the caller's hierarchy scope (e.g. an org-wide
/// report). Every other query in the codebase goes through the normal repositories and is
/// subject to the hierarchy-scoping global query filter — do not call .IgnoreQueryFilters()
/// ad hoc elsewhere. Callers must still be gated by an RBAC policy (e.g. "can view org-wide
/// reports") since this service intentionally bypasses data scoping, not authorization.
/// </summary>
public interface IUnscopedReportQueryService
{
    Task<IReadOnlyList<WidgetExample>> GetAllWidgetExamplesUnscopedAsync(CancellationToken cancellationToken = default);
}
