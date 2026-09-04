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

    public int? AgeYears { get; set; }

    public Gender Gender { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Occupation). Optional — marketing data only.</summary>
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }

    public TestOutcome Outcome { get; set; }

    /// <summary>"Referred or treated" — an orthogonal flag independent of Outcome (2026-09-03;
    /// previously implied by Outcome == the now-retired TestOutcome.Referred). Explicit rather
    /// than inferred from ReferralReasonRefId != null.</summary>
    public bool ReferredOrTreated { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = ReferralReason). Required whenever
    /// ReferredOrTreated is true, regardless of TreatedInFacility.</summary>
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }

    /// <summary>Required when ReferredOrTreated is true and TreatedInFacility is false; must be
    /// empty when TreatedInFacility is true (treated in-house has no external location) or when
    /// ReferredOrTreated is false.</summary>
    public string? ReferralLocationFreeText { get; set; }

    /// <summary>Some facilities have their own general doctors/eye professionals who can treat
    /// in-house rather than referring out — checking this hides ReferralLocationFreeText (the
    /// reason stays required either way).</summary>
    public bool TreatedInFacility { get; set; }

    /// <summary>Which lens(es) this person needs, recorded whenever Outcome == NeedsGlasses —
    /// regardless of whether contact details are also captured (see ticket "show lens-needed
    /// result whenever glasses are needed, not just on lead"). Nullable/optional throughout,
    /// same as Lead's equivalent block: a technician may record the outcome alone with no known
    /// product preference yet.</summary>
    public LensRangeType? LensRangeType { get; set; }

    public Guid? PresetCatalogueId { get; set; }
    public Guid? LensOptionLeftId { get; set; }
    public Guid? LensOptionRightId { get; set; }

    public decimal? CustomSphereLeft { get; set; }
    public decimal? CustomCylinderLeft { get; set; }
    public decimal? CustomAxisLeft { get; set; }
    public decimal? CustomAddPowerLeft { get; set; }
    public decimal? CustomSphereRight { get; set; }
    public decimal? CustomCylinderRight { get; set; }
    public decimal? CustomAxisRight { get; set; }
    public decimal? CustomAddPowerRight { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = LensType) — asked when a Custom lens carries
    /// two distinct powers (an add power alongside its base sphere) on either eye.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>The real inter-pupillary distance in mm — meaningful only for Custom range, and
    /// optional here the same as on a Lead (see Lead.PupilDistanceMm).</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Meaningful only for a preset range (SixLensSet/NineLensSet) — see
    /// Lead.PresetPupilDistanceBucket.</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Coating). Optional — same "no known
    /// preference yet" reasoning as Lead.CoatingPreferenceRefId.</summary>
    public Guid? CoatingPreferenceRefId { get; set; }

    public Guid? ConvertedToLeadId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
