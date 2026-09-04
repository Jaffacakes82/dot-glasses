using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;

namespace DotGlasses.Rules.ReferenceData;

/// <summary>
/// Every reference-data fact the consultation rules and the display layer need, read once and
/// then answered in memory — see ADR-0002. Two adapters fill it: the server's
/// (ReferenceDataSnapshotProvider, in Infrastructure) loads every item from the database, retired
/// ones included; the Field App's (ReferenceDataSnapshotAdapter, in App) hands over the
/// reference-data response it already caches in IndexedDB, which returns active items only, via
/// <see cref="FromCachedReferenceData"/>. That one filling lives here rather than in App only so a
/// test can exercise it without a Blazor WebAssembly project reference — everything it does is
/// mechanical projection of Contracts DTOs this project already references.
///
/// The question the rules actually ask is "present <em>and</em> active", which is correct under
/// both fillings — a retired item is absent from the client's copy and present-but-inactive in the
/// server's, and both reject it.
///
/// Because the server's copy carries retired items it is also the single label resolver (see
/// <see cref="ResolveLabel"/>), replacing seven separate Guid→label implementations that had four
/// different fallback strings between them.
///
/// Deliberately constructible as a plain literal — no database, no HTTP, no interface to fake —
/// so a rules test can state the exact reference data a case needs and nothing else.
/// </summary>
public sealed class ReferenceDataSnapshot
{
    /// <summary>The one fallback for a reference-data id that resolves to nothing. An em-dash
    /// rather than "Unknown"/"(retired coating)": it is the only one of the four strings this
    /// replaced that is honest about an item that simply was not found, and it already matches
    /// how every other "nothing here" cell in the Admin Portal renders.</summary>
    public const string MissingLabel = "—";

    private readonly Dictionary<Guid, ReferenceItemSnapshot> _itemsById = [];
    private readonly Dictionary<Guid, PresetCatalogueSnapshot> _cataloguesById = [];
    private readonly Dictionary<Guid, LensOptionSnapshot> _lensOptionsById = [];
    private readonly Dictionary<Guid, Guid> _catalogueIdByLensOptionId = [];
    private readonly HashSet<(Guid Lower, Guid Higher)> _exclusions = [];

    public ReferenceDataSnapshot(
        IReadOnlyList<ReferenceItemSnapshot> items,
        IReadOnlyList<PresetCatalogueSnapshot> presetCatalogues,
        IReadOnlyList<CoatingPairingRule> coatingPairings,
        IReadOnlyList<CoatingExclusionRule> coatingExclusions)
    {
        Items = items;
        PresetCatalogues = presetCatalogues;
        CoatingPairings = coatingPairings;
        CoatingExclusions = coatingExclusions;

        // Indexer assignment rather than ToDictionary: a hand-written test literal that repeats an
        // id should not blow up the constructor with a duplicate-key exception before the rule
        // under test ever runs.
        foreach (var item in items)
        {
            _itemsById[item.Id] = item;
        }

        foreach (var catalogue in presetCatalogues)
        {
            _cataloguesById[catalogue.Id] = catalogue;
            foreach (var lensOption in catalogue.LensOptions)
            {
                _lensOptionsById[lensOption.Id] = lensOption;
                _catalogueIdByLensOptionId[lensOption.Id] = catalogue.Id;
            }
        }

        foreach (var exclusion in coatingExclusions)
        {
            _exclusions.Add(Canonicalize(exclusion.CoatingRefIdA, exclusion.CoatingRefIdB));
        }
    }

    /// <summary>No reference data at all — a caller that has never been online, and a convenient
    /// starting point for a test that only cares about one topic.</summary>
    public static ReferenceDataSnapshot Empty { get; } = new([], [], [], []);

    public IReadOnlyList<ReferenceItemSnapshot> Items { get; }

    public IReadOnlyList<PresetCatalogueSnapshot> PresetCatalogues { get; }

    public IReadOnlyList<CoatingPairingRule> CoatingPairings { get; }

    public IReadOnlyList<CoatingExclusionRule> CoatingExclusions { get; }

    /// <summary>The Field App adapter: the cached reference-data response, verbatim. Everything
    /// the API returns is active by definition, so <see cref="ReferenceItemSnapshot.IsActive"/> is
    /// true for every item — see this type's own doc comment for why that still makes "present and
    /// active" the right question to ask on both sides.</summary>
    public static ReferenceDataSnapshot FromCachedReferenceData(
        IReadOnlyList<ReferenceDataItemDto> activeItems,
        IReadOnlyList<PresetCatalogueDto> catalogues,
        IReadOnlyList<CoatingPairingDto> coatingPairings,
        IReadOnlyList<CoatingExclusionDto> coatingExclusions) =>
        new(
            activeItems.Select(x => new ReferenceItemSnapshot(x.Id, x.Category, x.Label, IsActive: true, x.IsOtherOption)).ToList(),
            catalogues.Select(c => new PresetCatalogueSnapshot(
                c.Id,
                c.Name,
                c.Kind,
                c.LensOptions.Select(l => new LensOptionSnapshot(l.Id, l.Label, l.SortOrder, l.AvailableCoatingIds)).ToList())).ToList(),
            coatingPairings.Select(p => new CoatingPairingRule(p.TriggerCoatingRefId, p.PairedCoatingRefId)).ToList(),
            coatingExclusions.Select(e => new CoatingExclusionRule(e.CoatingRefIdA, e.CoatingRefIdB)).ToList());

    /// <summary>Null if nothing carries this id — including a null id, so a caller holding an
    /// optional FK doesn't have to check first.</summary>
    public ReferenceItemSnapshot? FindItem(Guid? refId) =>
        refId is { } id ? _itemsById.GetValueOrDefault(id) : null;

    /// <summary>Null if nothing carries this id <em>in this category</em> — a Guid that resolves
    /// to a Frame colour is not an answer to "which Occupation is this".</summary>
    public ReferenceItemSnapshot? FindItem(Guid? refId, ReferenceDataCategory category) =>
        FindItem(refId) is { } item && item.Category == category ? item : null;

    /// <summary>"Present and active in the expected category" — the question every rule that
    /// accepts a reference-data id actually asks.</summary>
    public bool IsActiveItem(Guid? refId, ReferenceDataCategory category) =>
        FindItem(refId, category) is { IsActive: true };

    /// <summary>
    /// The single Guid→label resolution for the whole server. A missing (or null) id renders
    /// <see cref="MissingLabel"/>; a retired item still renders its own stored label, which is why
    /// the server's copy carries retired items at all; and an "Other" item with free text renders
    /// the free text, so the technician's own words win over the generic "Other" label exactly
    /// where they did before.
    /// </summary>
    public string ResolveLabel(Guid? refId, string? otherText = null)
    {
        if (FindItem(refId) is not { } item)
        {
            return MissingLabel;
        }

        return item.IsOtherOption && !string.IsNullOrWhiteSpace(otherText) ? otherText : item.Label;
    }

    public PresetCatalogueSnapshot? FindCatalogue(Guid? presetCatalogueId) =>
        presetCatalogueId is { } id ? _cataloguesById.GetValueOrDefault(id) : null;

    public LensOptionSnapshot? FindLensOption(Guid? lensOptionId) =>
        lensOptionId is { } id ? _lensOptionsById.GetValueOrDefault(id) : null;

    /// <summary>A lens option's label is the linked LensStrength item's label, already resolved
    /// when the snapshot was filled — same <see cref="MissingLabel"/> fallback as everything
    /// else.</summary>
    public string ResolveLensOptionLabel(Guid? lensOptionId) =>
        FindLensOption(lensOptionId)?.Label ?? MissingLabel;

    public bool LensOptionBelongsToCatalogue(Guid lensOptionId, Guid presetCatalogueId) =>
        _catalogueIdByLensOptionId.TryGetValue(lensOptionId, out var catalogueId) && catalogueId == presetCatalogueId;

    /// <summary>False (never an exception) if the lens option doesn't exist, or its lens strength
    /// has no coatings configured yet — a real interim state for most non-bifocal strengths, which
    /// tickets 10/11 report against the lens rather than the Coating set.</summary>
    public bool IsCoatingAvailableForLensOption(Guid lensOptionId, Guid coatingRefId) =>
        FindLensOption(lensOptionId) is { } lensOption && lensOption.AvailableCoatingIds.Contains(coatingRefId);

    /// <summary>Symmetric, per <c>CONTEXT.md</c>'s <b>Coating exclusion</b> — the pair is
    /// canonicalized on the way in and on the way out, so argument order never matters.</summary>
    public bool AreCoatingsExcluded(Guid coatingRefIdA, Guid coatingRefIdB) =>
        _exclusions.Contains(Canonicalize(coatingRefIdA, coatingRefIdB));

    /// <summary>The Coatings a <b>Coating pairing</b> auto-adds when this one is selected.
    /// Directional, per <c>CONTEXT.md</c>: the reverse selection does not pair back.</summary>
    public IReadOnlyList<Guid> PairedCoatingsFor(Guid triggerCoatingRefId) =>
        CoatingPairings.Where(p => p.TriggerCoatingRefId == triggerCoatingRefId).Select(p => p.PairedCoatingRefId).ToList();

    /// <summary>Mirrors Domain.Entities.CoatingExclusion.Canonicalize — restated rather than
    /// referenced because Rules must not reference Domain (see this project's csproj).</summary>
    private static (Guid Lower, Guid Higher) Canonicalize(Guid a, Guid b) =>
        a.CompareTo(b) <= 0 ? (a, b) : (b, a);
}

/// <summary>One admin-managed dropdown option. <paramref name="IsActive"/> and
/// <paramref name="IsOtherOption"/> are the two flags every rule keys off: a retired item may
/// still be rendered but never newly chosen, and an "Other" item is what makes a dropdown reveal
/// its free-text field.</summary>
public sealed record ReferenceItemSnapshot(Guid Id, ReferenceDataCategory Category, string Label, bool IsActive, bool IsOtherOption);

/// <summary>A catalogue's lens roster, in display order. Which catalogue a lens option belongs to
/// is the nesting, not a field — the snapshot indexes that on the way in.</summary>
public sealed record PresetCatalogueSnapshot(Guid Id, string Name, PresetCatalogueKind Kind, IReadOnlyList<LensOptionSnapshot> LensOptions);

/// <summary>Label is the linked LensStrength reference item's label (e.g. <c>+2.50</c>);
/// AvailableCoatingIds is which Coatings that strength is sellable in, empty meaning "not
/// configured yet".</summary>
public sealed record LensOptionSnapshot(Guid Id, string Label, int SortOrder, IReadOnlyList<Guid> AvailableCoatingIds);

/// <summary>Directional — see <c>CONTEXT.md</c>'s <b>Coating pairing</b>.</summary>
public sealed record CoatingPairingRule(Guid TriggerCoatingRefId, Guid PairedCoatingRefId);

/// <summary>Symmetric — see <c>CONTEXT.md</c>'s <b>Coating exclusion</b>.</summary>
public sealed record CoatingExclusionRule(Guid CoatingRefIdA, Guid CoatingRefIdB);
