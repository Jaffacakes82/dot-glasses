using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no
/// repository interface exists for OrganisationNode, matching PresetCatalogueQueryService.</summary>
public class OrganisationAdminService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService, ICurrentUserContext currentUserContext) : IOrganisationAdminService
{
    public async Task<IReadOnlyList<OrganisationAdminNode>> ListAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await dbContext.OrganisationNodes
            .OrderBy(x => x.HierarchyPath)
            .ToListAsync(cancellationToken);

        return nodes.Select(ToAdminNode).ToList();
    }

    public async Task<IReadOnlyList<OrganisationAdminNode>> ListDeactivatedAsync(CancellationToken cancellationToken = default)
    {
        var prefix = currentUserContext.HierarchyPathPrefix;
        var nodes = await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(x => x.IsDeleted && x.HierarchyPath.StartsWith(prefix))
            .OrderBy(x => x.HierarchyPath)
            .ToListAsync(cancellationToken);

        return nodes.Select(ToAdminNode).ToList();
    }

    public bool IsValidChildLevel(OrganisationLevel parentLevel, OrganisationLevel childLevel) => parentLevel switch
    {
        OrganisationLevel.Dgi => childLevel == OrganisationLevel.Country,
        OrganisationLevel.Country or OrganisationLevel.Intermediate => childLevel is OrganisationLevel.Intermediate or OrganisationLevel.RetailPoint,
        OrganisationLevel.RetailPoint => false,
        _ => false,
    };

    public async Task<OrganisationAdminNode> CreateChildAsync(Guid parentId, string name, OrganisationLevel level, string? kind, CancellationToken cancellationToken = default)
    {
        var parent = await dbContext.OrganisationNodes.FirstAsync(x => x.Id == parentId, cancellationToken);

        if (!IsValidChildLevel(parent.Level, level))
        {
            throw new DomainRuleViolationException($"{level} is not a valid child level under a {parent.Level} node.");
        }

        // New path segments are small ever-increasing integers assigned in creation order across
        // the *whole* tree (not per-parent) — see the seeded /1/, /1/2/, /1/2/3/ paths. Picking
        // the next one safely means seeing the current global max, which requires looking outside
        // the caller's own hierarchy scope (an Admin below DGI creating a node must not collide
        // with a segment an org they can't see already used) — hence IUnscopedReportQueryService, not a
        // scoped query here. Known simplification: read-max-then-increment has a small race
        // window under concurrent creates — acceptable for an infrequent, admin-only action.
        var allPaths = await unscopedReportQueryService.GetOrganisationNodePathsUnscopedAsync(cancellationToken);
        var maxSegment = allPaths
            .SelectMany(p => p.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Select(s => int.TryParse(s, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        var entity = new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = parent.Id,
            Name = name,
            Level = level,
            Kind = kind,
            HierarchyPath = $"{parent.HierarchyPath}{maxSegment + 1}/",
            IsTrainingOrg = false,
        };

        dbContext.OrganisationNodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAdminNode(entity);
    }

    public async Task SetTrainingOrgFlagAsync(Guid id, bool isTrainingOrg, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OrganisationNodes.FirstAsync(x => x.Id == id, cancellationToken);
        entity.IsTrainingOrg = isTrainingOrg;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OrganisationNodes.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Name = name;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters() — reactivating means finding a row the standard filter currently
        // hides (IsDeleted = true), same reason ListDeactivatedAsync needs it.
        var entity = await dbContext.OrganisationNodes.IgnoreQueryFilters().FirstAsync(x => x.Id == id, cancellationToken);

        if (isActive)
        {
            // AuditSaveChangesInterceptor has no "undelete" — it only turns a Remove() into a
            // soft-delete, one direction. Reactivating means clearing the soft-delete fields by
            // hand.
            entity.IsDeleted = false;
            entity.DeletedAtUtc = null;
            entity.DeletedBy = null;
        }
        else
        {
            var hasActiveChildren = await dbContext.OrganisationNodes.AnyAsync(x => x.ParentId == id, cancellationToken);
            if (hasActiveChildren)
            {
                throw new DomainRuleViolationException("Deactivate this node's child orgs first — an org with active children can't be deactivated.");
            }

            // Remove() on an ISoftDeletable entity is turned into a soft-delete by
            // AuditSaveChangesInterceptor (State flips Deleted -> Modified, IsDeleted/
            // DeletedAtUtc/DeletedBy get stamped) — same sanctioned pattern WidgetExampleRepository
            // already uses, not a hard delete.
            dbContext.OrganisationNodes.Remove(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OrganisationAdminNode ToAdminNode(OrganisationNode entity) =>
        new(entity.Id, entity.ParentId, entity.Name, entity.Level, entity.Kind, entity.HierarchyPath, entity.IsTrainingOrg, !entity.IsDeleted);
}
