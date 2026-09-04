namespace DotGlasses.Domain.Entities;

/// <summary>
/// "This Sale's lens includes this Coating" — replaces Sale's former single CoatingRefId
/// (2026-09-03, see ADR-0001): a lens can carry more than one Coating at once (e.g. Blue Block +
/// Photochromic together). No navigation property on Sale, matching the rest of this codebase's
/// Guid-FK-only convention (see CustomerId/OccupationRefId etc. on Sale itself) — SaleService
/// queries/writes this table directly alongside Sale in the same unit of work.
/// </summary>
public class SaleCoating
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Guid CoatingRefId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
