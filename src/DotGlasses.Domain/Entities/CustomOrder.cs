using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// A custom lens ordered from DOT Glasses (ADR-0008, <c>CONTEXT.md</c> "Custom order"). A record
/// of its own: it holds the order — where it was placed, when, and how far through the lab it is
/// — and points at the Lead or Sale that placed it. The lens, coatings, pupil distance and
/// customer are read from that record; none is copied here, because a Lead or Sale can't be
/// edited and so can never drift from what was ordered.
///
/// An order is placed when a Lead or a Sale is recorded, never afterwards. One placed by a Lead
/// has <see cref="LeadId"/> and no <see cref="SaleId"/> until that Lead converts — "not yet
/// paid" — and the Sale it converts into is linked to this same order rather than placing a
/// second. There is no cancel.
/// </summary>
public class CustomOrder : IAuditable, ISoftDeletable, IHierarchyScoped
{
    public Guid Id { get; set; }

    /// <summary>The retail point the order was placed at — the placing record's own path.</summary>
    public string HierarchyPath { get; set; } = string.Empty;

    /// <summary>Submitted at placing, then advanced forward-only from the Custom Orders screen.</summary>
    public FulfilmentStatus Status { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    /// <summary>The Lead that placed the order, when a Lead did.</summary>
    public Guid? LeadId { get; set; }

    /// <summary>The Sale that placed the order, or the Sale an ordering Lead converted into.
    /// Null means nobody has paid for it yet.</summary>
    public Guid? SaleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>A new order, placed now at <paramref name="hierarchyPath"/> by exactly one of a
    /// Lead or a Sale. Every order starts Submitted.</summary>
    public static CustomOrder Place(string hierarchyPath, Guid? leadId = null, Guid? saleId = null) => new()
    {
        Id = Guid.NewGuid(),
        HierarchyPath = hierarchyPath,
        Status = FulfilmentStatus.Submitted,
        PlacedAtUtc = DateTimeOffset.UtcNow,
        LeadId = leadId,
        SaleId = saleId,
    };
}

/// <summary>
/// "This ordering Lead's lens includes this Coating" — the Lead's counterpart of
/// <see cref="SaleCoating"/>. Only a Lead that places a custom order has rows here: the lab makes
/// what was ordered, so it needs the full Coating set (ADR-0008). A Lead that doesn't order keeps
/// its single optional Coating preference on the Lead itself (ADR-0001).
/// </summary>
public class LeadCoating
{
    public Guid Id { get; set; }

    public Guid LeadId { get; set; }

    public Guid CoatingRefId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
