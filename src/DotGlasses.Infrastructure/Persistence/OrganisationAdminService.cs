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
        // The global filter hides soft-deleted rows, so it is ignored and the caller's scope
        // re-applied by hand — the same LIKE ANY shape as the filter (ADR-0004/0006). No scope
        // paths, no patterns, no rows.
        var patterns = currentUserContext.ScopePaths.Select(p => p.Value + "%").ToArray();
        var nodes = await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(x => x.IsDeleted && patterns.Any(p => EF.Functions.Like(x.HierarchyPath, p)))
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

    public async Task<OrganisationAdminNode> CreateChildAsync(Guid parentId, string name, OrganisationLevel level, CancellationToken cancellationToken = default)
    {
        var parent = await dbContext.OrganisationNodes.FirstAsync(x => x.Id == parentId, cancellationToken);

        if (!IsValidChildLevel(parent.Level, level))
        {
            throw new DomainRuleViolationException("That level can't sit directly under this organisation. Choose another level.");
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
            await ReactivateAsync(entity, cancellationToken);
        }
        else
        {
            await DeactivateAsync(entity, cancellationToken);
        }

        // One SaveChanges, so the whole group goes or comes back together.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The organisation and every active one beneath it, stamped with one group id.
    /// Read past the global filter so the group doesn't depend on which rows the caller's own
    /// scope happens to return — the controller has already checked the target is theirs to
    /// manage, and everything beneath it is then theirs too. Already-deactivated descendants are
    /// left out: they belong to their own, earlier group.</summary>
    private async Task DeactivateAsync(OrganisationNode entity, CancellationToken cancellationToken)
    {
        if (entity.IsDeleted)
        {
            return;
        }

        if (OwnAccess.ComesThrough(HierarchyPath.Parse(entity.HierarchyPath), currentUserContext.ScopePaths))
        {
            throw new DomainRuleViolationException(OwnAccess.DeactivationRefusal);
        }

        var pattern = entity.HierarchyPath + "%";
        var group = await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && EF.Functions.Like(x.HierarchyPath, pattern))
            .ToListAsync(cancellationToken);

        var groupId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        foreach (var node in group)
        {
            node.DeactivationGroupId = groupId;

            // Stamped by hand rather than through Remove() and AuditSaveChangesInterceptor's
            // soft-delete flip: marking a parent Deleted makes EF null the ParentId of every
            // tracked child (the FK is optional), and the flip back to Modified doesn't undo
            // that — the children would be saved detached from the tree.
            node.IsDeleted = true;
            node.DeletedAtUtc = now;
            node.DeletedBy = currentUserContext.UserName ?? currentUserContext.UserId?.ToString() ?? "system";
        }
    }

    private async Task ReactivateAsync(OrganisationNode entity, CancellationToken cancellationToken)
    {
        if (!entity.IsDeleted)
        {
            return;
        }

        // The parent lookup ignores the filter for the same reason the target's does: the row
        // that decides this is one the filter hides.
        if (entity.ParentId is { } parentId)
        {
            var parent = await dbContext.OrganisationNodes.IgnoreQueryFilters().FirstAsync(x => x.Id == parentId, cancellationToken);
            if (parent.IsDeleted)
            {
                throw new DomainRuleViolationException(
                    $"{entity.Name} sits under {parent.Name}, which is deactivated. Reactivate the organisation above it first.");
            }
        }

        var group = new List<OrganisationNode> { entity };
        if (entity.DeactivationGroupId is { } groupId)
        {
            var pattern = entity.HierarchyPath + "%";
            group.AddRange(await dbContext.OrganisationNodes
                .IgnoreQueryFilters()
                .Where(x => x.IsDeleted && x.Id != entity.Id && x.DeactivationGroupId == groupId && EF.Functions.Like(x.HierarchyPath, pattern))
                .ToListAsync(cancellationToken));
        }

        foreach (var node in group)
        {
            // AuditSaveChangesInterceptor has no "undelete" — it only turns a Remove() into a
            // soft-delete, one direction. Reactivating means clearing the soft-delete fields by
            // hand.
            node.IsDeleted = false;
            node.DeletedAtUtc = null;
            node.DeletedBy = null;
            node.DeactivationGroupId = null;
        }
    }

    private static OrganisationAdminNode ToAdminNode(OrganisationNode entity) =>
        new(entity.Id, entity.ParentId, entity.Name, entity.Level, entity.HierarchyPath, entity.IsTrainingOrg, !entity.IsDeleted, entity.DeactivationGroupId);
}
