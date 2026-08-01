using DotGlasses.Domain.Common;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Deliberately generic placeholder entity used to prove the architectural skeleton
/// (audit/soft-delete, hierarchy scoping, RBAC, offline sync) end-to-end before real domain
/// entities are designed. Not a template to extend with real fields.
/// </summary>
public class WidgetExample : IAuditable, ISoftDeletable, IHierarchyScoped
{
    /// <summary>
    /// Client-generated for records created offline in the field app, so the id itself doubles
    /// as the outbox sync idempotency key — a repeated sync of the same record is a no-op upsert.
    /// </summary>
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
