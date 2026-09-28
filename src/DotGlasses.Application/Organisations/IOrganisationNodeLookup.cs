namespace DotGlasses.Application.Organisations;

/// <summary>
/// The org node standing at an exact HierarchyPath — deactivated nodes included. The one caller
/// (LeadConversionController, checking whether a Lead's retail point is still active before
/// creating the Sale — ticket 07) is asking specifically about deactivation, which the standard
/// hierarchy-scoping/soft-delete query filter — and IUnscopedReportQueryService, which
/// re-applies the soft-delete half of it by hand — would otherwise hide (CLAUDE.md's "un-hiding
/// a soft-deleted entity" pitfall: the row being asked about is exactly the row a plain scoped or
/// "unscoped" read leaves out).
/// </summary>
public interface IOrganisationNodeLookup
{
    Task<OrganisationNodeStatus?> FindByHierarchyPathAsync(string hierarchyPath, CancellationToken cancellationToken = default);
}

public sealed record OrganisationNodeStatus(Guid Id, string Name, bool IsActive);
