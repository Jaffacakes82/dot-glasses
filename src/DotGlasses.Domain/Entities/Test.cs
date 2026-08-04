using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Atomic vision-test event, always logged regardless of whether it becomes a Lead. Id is
/// client-generated so it doubles as the offline-sync outbox idempotency key, same as
/// WidgetExample.
/// </summary>
public class Test : IAuditable, ISoftDeletable, IHierarchyScoped
{
    public Guid Id { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    public Guid TechnicianUserId { get; set; }

    /// <summary>Set only if the customer shared contact details and this test became a Lead.</summary>
    public Guid? CustomerId { get; set; }

    public int? AgeYears { get; set; }

    public Gender Gender { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Occupation). Optional — marketing data only.</summary>
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }

    public TestOutcome Outcome { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = ReferralReason). Required, alongside
    /// ReferralOtherText/ReferralLocationFreeText, iff Outcome == Referred.</summary>
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }
    public string? ReferralLocationFreeText { get; set; }

    public Guid? ConvertedToLeadId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
