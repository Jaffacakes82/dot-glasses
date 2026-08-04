using DotGlasses.Domain.Common;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Lightweight identity used for fuzzy name+phone matching and to let a technician see that a
/// customer has history at this retail point without exposing who served them previously. Not a
/// full CRM record — Test/Lead/Sale carry the actual event data and link here optionally once
/// matched or created.
/// </summary>
public class Customer : IAuditable, ISoftDeletable, IHierarchyScoped
{
    public Guid Id { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
