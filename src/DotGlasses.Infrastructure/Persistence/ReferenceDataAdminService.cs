using System.Text;
using System.Text.RegularExpressions;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no
/// repository interface exists for ReferenceDataItem, and one isn't needed for four
/// straightforward operations (matches how PresetCatalogueQueryService queries DbContext
/// directly).</summary>
public partial class ReferenceDataAdminService(DotGlassesDbContext dbContext) : IReferenceDataAdminService
{
    public async Task<IReadOnlyList<ReferenceDataAdminItem>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ReferenceDataItems
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return items.Select(ToAdminItem).ToList();
    }

    public async Task<bool> HasActiveOtherOptionAsync(ReferenceDataCategory category, CancellationToken cancellationToken = default) =>
        await dbContext.ReferenceDataItems
            .AnyAsync(x => x.Category == category && x.IsActive && x.IsOtherOption, cancellationToken);

    public async Task<ReferenceDataAdminItem> CreateAsync(ReferenceDataCategory category, string label, string? imageUrl, bool isOtherOption, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await dbContext.ReferenceDataItems
            .Where(x => x.Category == category)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new ReferenceDataItem
        {
            Id = Guid.NewGuid(),
            Category = category,
            Code = Slugify(label),
            Label = label,
            SortOrder = maxSortOrder + 1,
            IsActive = true,
            IsOtherOption = isOtherOption,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
        };

        dbContext.ReferenceDataItems.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAdminItem(entity);
    }

    public async Task<ReferenceDataAdminItem> UpdateAsync(Guid id, string label, string? imageUrl, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Label = label;
        entity.ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAdminItem(entity);
    }

    public async Task MoveUpAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        var neighbor = await dbContext.ReferenceDataItems
            .Where(x => x.Category == entity.Category && x.IsActive && x.SortOrder < entity.SortOrder)
            .OrderByDescending(x => x.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (neighbor is null)
        {
            return;
        }

        (entity.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, entity.SortOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveDownAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        var neighbor = await dbContext.ReferenceDataItems
            .Where(x => x.Category == entity.Category && x.IsActive && x.SortOrder > entity.SortOrder)
            .OrderBy(x => x.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (neighbor is null)
        {
            return;
        }

        (entity.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, entity.SortOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CoatingPairingAdminItem>> ListCoatingPairingsAsync(CancellationToken cancellationToken = default)
    {
        var pairings = await dbContext.CoatingPairings.ToListAsync(cancellationToken);
        var labels = await GetCoatingLabelsAsync(cancellationToken);

        return pairings
            .Select(p => new CoatingPairingAdminItem(p.Id, p.TriggerCoatingRefId, Label(labels, p.TriggerCoatingRefId), p.PairedCoatingRefId, Label(labels, p.PairedCoatingRefId)))
            .OrderBy(p => p.TriggerCoatingLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.PairedCoatingLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<CoatingExclusionAdminItem>> ListCoatingExclusionsAsync(CancellationToken cancellationToken = default)
    {
        var exclusions = await dbContext.CoatingExclusions.ToListAsync(cancellationToken);
        var labels = await GetCoatingLabelsAsync(cancellationToken);

        return exclusions
            .Select(e => new CoatingExclusionAdminItem(e.Id, e.CoatingRefIdA, Label(labels, e.CoatingRefIdA), e.CoatingRefIdB, Label(labels, e.CoatingRefIdB)))
            .OrderBy(e => e.LabelA, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.LabelB, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task AddCoatingPairingAsync(Guid triggerCoatingRefId, Guid pairedCoatingRefId, CancellationToken cancellationToken = default)
    {
        if (triggerCoatingRefId == pairedCoatingRefId)
        {
            throw new InvalidOperationException("A coating can't pair with itself.");
        }

        await EnsureActiveCoatingAsync(triggerCoatingRefId, cancellationToken);
        await EnsureActiveCoatingAsync(pairedCoatingRefId, cancellationToken);

        if (await dbContext.CoatingPairings.AnyAsync(p => p.TriggerCoatingRefId == triggerCoatingRefId && p.PairedCoatingRefId == pairedCoatingRefId, cancellationToken))
        {
            throw new InvalidOperationException("This pairing already exists.");
        }

        if (await HasExclusionAsync(triggerCoatingRefId, pairedCoatingRefId, cancellationToken))
        {
            throw new InvalidOperationException("Can't add this pairing — an exclusion already exists between these two coatings.");
        }

        dbContext.CoatingPairings.Add(new CoatingPairing
        {
            Id = Guid.NewGuid(),
            TriggerCoatingRefId = triggerCoatingRefId,
            PairedCoatingRefId = pairedCoatingRefId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCoatingPairingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CoatingPairings.FirstAsync(x => x.Id == id, cancellationToken);
        dbContext.CoatingPairings.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCoatingExclusionAsync(Guid coatingRefIdA, Guid coatingRefIdB, CancellationToken cancellationToken = default)
    {
        if (coatingRefIdA == coatingRefIdB)
        {
            throw new InvalidOperationException("A coating can't exclude itself.");
        }

        await EnsureActiveCoatingAsync(coatingRefIdA, cancellationToken);
        await EnsureActiveCoatingAsync(coatingRefIdB, cancellationToken);

        var (lower, higher) = Canonicalize(coatingRefIdA, coatingRefIdB);
        if (await dbContext.CoatingExclusions.AnyAsync(e => e.CoatingRefIdA == lower && e.CoatingRefIdB == higher, cancellationToken))
        {
            throw new InvalidOperationException("This exclusion already exists.");
        }

        var hasPairing = await dbContext.CoatingPairings.AnyAsync(
            p => (p.TriggerCoatingRefId == coatingRefIdA && p.PairedCoatingRefId == coatingRefIdB)
                || (p.TriggerCoatingRefId == coatingRefIdB && p.PairedCoatingRefId == coatingRefIdA),
            cancellationToken);
        if (hasPairing)
        {
            throw new InvalidOperationException("Can't add this exclusion — a pairing already exists between these two coatings.");
        }

        dbContext.CoatingExclusions.Add(new CoatingExclusion
        {
            Id = Guid.NewGuid(),
            CoatingRefIdA = lower,
            CoatingRefIdB = higher,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCoatingExclusionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CoatingExclusions.FirstAsync(x => x.Id == id, cancellationToken);
        dbContext.CoatingExclusions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureActiveCoatingAsync(Guid coatingRefId, CancellationToken cancellationToken)
    {
        var isActive = await dbContext.ReferenceDataItems
            .AnyAsync(x => x.Id == coatingRefId && x.Category == ReferenceDataCategory.Coating && x.IsActive, cancellationToken);
        if (!isActive)
        {
            throw new InvalidOperationException("Both coatings must reference an existing, active Coating reference-data item.");
        }
    }

    private async Task<bool> HasExclusionAsync(Guid coatingRefIdA, Guid coatingRefIdB, CancellationToken cancellationToken)
    {
        var (lower, higher) = Canonicalize(coatingRefIdA, coatingRefIdB);
        return await dbContext.CoatingExclusions.AnyAsync(e => e.CoatingRefIdA == lower && e.CoatingRefIdB == higher, cancellationToken);
    }

    private static (Guid Lower, Guid Higher) Canonicalize(Guid a, Guid b) =>
        a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    private async Task<IReadOnlyDictionary<Guid, string>> GetCoatingLabelsAsync(CancellationToken cancellationToken) =>
        await dbContext.ReferenceDataItems
            .Where(x => x.Category == ReferenceDataCategory.Coating)
            .ToDictionaryAsync(x => x.Id, x => x.Label, cancellationToken);

    private static string Label(IReadOnlyDictionary<Guid, string> labels, Guid id) => labels.GetValueOrDefault(id, "(retired coating)");

    private static ReferenceDataAdminItem ToAdminItem(ReferenceDataItem entity) =>
        new(entity.Id, entity.Category, entity.Code, entity.Label, entity.SortOrder, entity.IsActive, entity.IsOtherOption, entity.ImageUrl);

    private static string Slugify(string label)
    {
        var lowered = label.Trim().ToLowerInvariant();
        var withHyphens = NonAlphanumericRun().Replace(lowered, "-");
        return withHyphens.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRun();
}
