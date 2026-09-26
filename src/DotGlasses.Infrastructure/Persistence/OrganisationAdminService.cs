using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no
/// repository interface exists for OrganisationNode, matching PresetCatalogueQueryService.</summary>
public class OrganisationAdminService(DotGlassesDbContext dbContext, ICurrentUserContext currentUserContext) : IOrganisationAdminService
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

        // New path segments are globally unique integers across the *whole* tree (not
        // per-parent), drawn from a sequence — see OrganisationNodeConfiguration.PathSegmentSequence
        // for why "current max + 1" was not safe.
        var segment = await dbContext.Database
            .SqlQueryRaw<long>($"""SELECT nextval('"{OrganisationNodeConfiguration.PathSegmentSequence}"') AS "Value" """)
            .SingleAsync(cancellationToken);

        var entity = new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = parent.Id,
            Name = name,
            Level = level,
            Kind = kind,
            HierarchyPath = $"{parent.HierarchyPath}{segment}/",
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
            // DeletedAtUtc/DeletedBy get stamped), not a hard delete.
            dbContext.OrganisationNodes.Remove(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OrganisationAdminNode ToAdminNode(OrganisationNode entity) =>
        new(entity.Id, entity.ParentId, entity.Name, entity.Level, entity.Kind, entity.HierarchyPath, entity.IsTrainingOrg, !entity.IsDeleted);
}
