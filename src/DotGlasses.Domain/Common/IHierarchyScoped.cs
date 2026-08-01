namespace DotGlasses.Domain.Common;

/// <summary>
/// Entities scoped to a node in the org hierarchy via a materialized path, e.g. "/1/4/12/".
/// Drives the data-scoping global query filter — see DotGlasses.Infrastructure's DbContext.
/// </summary>
public interface IHierarchyScoped
{
    string HierarchyPath { get; set; }
}
