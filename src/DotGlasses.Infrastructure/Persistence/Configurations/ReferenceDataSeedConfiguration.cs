using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds every admin-managed dropdown list (HasData, fixed GUIDs) from the Kobo "General Retail
/// Point Data Collection" choices export, per the 2026-08-04 decisions:
///  - Occupation / ReasonNotPurchased / ReferralReason are taken verbatim from the Kobo
///    income_source / why_no_purchase / referral lists (each already includes an "Other" row).
///  - Coating uses Bradley's 5-item list from the CEO call, NOT Kobo's 7-item coating_types list
///    (no Antiglare, no separate "None").
///  - HardCaseColour is Orange/Green/Other — the actual manufactured colours — NOT Kobo's
///    Blue/Pink/Purple/Black (stale legacy data).
///  - FrameColour is the 6 colours named on the call (matches the e-commerce site) + an "Other"
///    fallback for consistency with every other list — not explicitly discussed on the call, but
///    low-risk/reversible via the future admin UI; flagging the assumption here.
///  - Kobo's lens_set "Classical (full refraction)" option, and the photophobias/vision/
///    multifocal_types lists, are deliberately not seeded anywhere (dropped as legacy-only /
///    superseded by the per-transaction lens-range model).
/// </summary>
public class ReferenceDataSeedConfiguration : IEntityTypeConfiguration<ReferenceDataItem>
{
    // Exposed for PresetCatalogueSeedConfiguration, which needs to pin each seeded LensOption to
    // one of these.
    public static readonly Guid CoatingPhotochromicId = new("b0000000-0000-0000-0000-000000000023");
    public static readonly Guid CoatingClearId = new("b0000000-0000-0000-0000-000000000024");

    // Exposed for PresetCatalogueSeedConfiguration/LensOptionSeedConfiguration, which build each
    // catalogue's LensOption roster from these.
    public static readonly Guid LensStrength300Id = new("b0000000-0000-0000-0000-000000000043");
    public static readonly Guid LensStrength250Id = new("b0000000-0000-0000-0000-000000000044");
    public static readonly Guid LensStrength200Id = new("b0000000-0000-0000-0000-000000000045");
    public static readonly Guid LensStrength125Id = new("b0000000-0000-0000-0000-000000000046");
    public static readonly Guid LensStrength000Id = new("b0000000-0000-0000-0000-000000000047");
    public static readonly Guid LensStrengthMinus100Id = new("b0000000-0000-0000-0000-000000000048");
    public static readonly Guid LensStrengthMinus150Id = new("b0000000-0000-0000-0000-000000000049");
    public static readonly Guid LensStrengthMinus200Id = new("b0000000-0000-0000-0000-000000000050");
    public static readonly Guid LensStrengthMinus250Id = new("b0000000-0000-0000-0000-000000000051");
    public static readonly Guid LensStrengthMinus300Id = new("b0000000-0000-0000-0000-000000000052");
    public static readonly Guid LensStrengthMinus400Id = new("b0000000-0000-0000-0000-000000000053");
    public static readonly Guid LensStrengthMinus450Id = new("b0000000-0000-0000-0000-000000000054");
    public static readonly Guid LensStrengthBifocal300Id = new("b0000000-0000-0000-0000-000000000055");
    public static readonly Guid LensStrengthBifocal250Id = new("b0000000-0000-0000-0000-000000000056");
    public static readonly Guid LensStrengthBifocal200Id = new("b0000000-0000-0000-0000-000000000057");
    public static readonly Guid LensStrengthBifocal125Id = new("b0000000-0000-0000-0000-000000000058");

    public void Configure(EntityTypeBuilder<ReferenceDataItem> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var items = new List<ReferenceDataItem>();
        var sort = 0;

        void Add(Guid id, ReferenceDataCategory category, string code, string label, bool isOther = false)
        {
            items.Add(new ReferenceDataItem
            {
                Id = id,
                Category = category,
                Code = code,
                Label = label,
                SortOrder = sort++,
                IsActive = true,
                IsOtherOption = isOther,
                CreatedAtUtc = now,
            });
        }

        // Occupation ← Kobo income_source (already includes "other").
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000001"), ReferenceDataCategory.Occupation, "farmer", "Farmer");
        Add(new("b0000000-0000-0000-0000-000000000002"), ReferenceDataCategory.Occupation, "factory_worker", "Factory worker");
        Add(new("b0000000-0000-0000-0000-000000000003"), ReferenceDataCategory.Occupation, "teacher", "Teacher");
        Add(new("b0000000-0000-0000-0000-000000000004"), ReferenceDataCategory.Occupation, "health", "Health worker");
        Add(new("b0000000-0000-0000-0000-000000000005"), ReferenceDataCategory.Occupation, "salaried", "Other salaried employee");
        Add(new("b0000000-0000-0000-0000-000000000006"), ReferenceDataCategory.Occupation, "business_owner", "Business owner");
        Add(new("b0000000-0000-0000-0000-000000000007"), ReferenceDataCategory.Occupation, "labourer", "Casual labour");
        Add(new("b0000000-0000-0000-0000-000000000008"), ReferenceDataCategory.Occupation, "fundi", "Fundi");
        Add(new("b0000000-0000-0000-0000-000000000009"), ReferenceDataCategory.Occupation, "retired", "Retired");
        Add(new("b0000000-0000-0000-0000-000000000010"), ReferenceDataCategory.Occupation, "student", "Student");
        Add(new("b0000000-0000-0000-0000-000000000011"), ReferenceDataCategory.Occupation, "none", "No economic activity");
        Add(new("b0000000-0000-0000-0000-000000000012"), ReferenceDataCategory.Occupation, "other", "Other", isOther: true);

        // ReasonNotPurchased ← Kobo why_no_purchase (already includes "other").
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000013"), ReferenceDataCategory.ReasonNotPurchased, "glasses_didnt_help", "These glasses couldn't help me");
        Add(new("b0000000-0000-0000-0000-000000000014"), ReferenceDataCategory.ReasonNotPurchased, "dont_need_glasses", "Don't need glasses");
        Add(new("b0000000-0000-0000-0000-000000000015"), ReferenceDataCategory.ReasonNotPurchased, "price", "Price");
        Add(new("b0000000-0000-0000-0000-000000000016"), ReferenceDataCategory.ReasonNotPurchased, "no_money", "Don't have the money now");
        Add(new("b0000000-0000-0000-0000-000000000017"), ReferenceDataCategory.ReasonNotPurchased, "consulting_family", "Needed to consult family");
        Add(new("b0000000-0000-0000-0000-000000000018"), ReferenceDataCategory.ReasonNotPurchased, "returning_later", "Wanted to return later");
        Add(new("b0000000-0000-0000-0000-000000000019"), ReferenceDataCategory.ReasonNotPurchased, "want_other_provider", "Preferred another provider");
        Add(new("b0000000-0000-0000-0000-000000000020"), ReferenceDataCategory.ReasonNotPurchased, "not_convinced_of_benefit", "Not convinced of benefit");
        Add(new("b0000000-0000-0000-0000-000000000021"), ReferenceDataCategory.ReasonNotPurchased, "other", "Other", isOther: true);

        // ReferralReason ← Kobo referral (already includes "other").
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000022"), ReferenceDataCategory.ReferralReason, "inconclusive", "Inconclusive test result");
        Add(new("b0000000-0000-0000-0000-000000000030"), ReferenceDataCategory.ReferralReason, "suspected_eye_disease", "Suspected eye disease");
        Add(new("b0000000-0000-0000-0000-000000000031"), ReferenceDataCategory.ReferralReason, "high_prescription", "High power requirement or outside Dot Glasses range");
        Add(new("b0000000-0000-0000-0000-000000000032"), ReferenceDataCategory.ReferralReason, "astigmatism", "Astigmatism");
        Add(new("b0000000-0000-0000-0000-000000000033"), ReferenceDataCategory.ReferralReason, "young_child", "Child under eligible age without approval from a specialist");
        Add(new("b0000000-0000-0000-0000-000000000034"), ReferenceDataCategory.ReferralReason, "other", "Other", isOther: true);

        // Coating ← Bradley's 5-item list from the call (not Kobo's 7-item coating_types).
        sort = 0;
        Add(CoatingPhotochromicId, ReferenceDataCategory.Coating, "photochromic", "Photochromic");
        Add(CoatingClearId, ReferenceDataCategory.Coating, "clear", "Clear");
        Add(new("b0000000-0000-0000-0000-000000000025"), ReferenceDataCategory.Coating, "blue_block", "Blue block");
        Add(new("b0000000-0000-0000-0000-000000000026"), ReferenceDataCategory.Coating, "polarized", "Polarized");
        Add(new("b0000000-0000-0000-0000-000000000027"), ReferenceDataCategory.Coating, "sunglasses", "Sunglasses");

        // FrameColour ← the 6 colours named on the call, matching the e-commerce site, + Other.
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000028"), ReferenceDataCategory.FrameColour, "black", "Black");
        Add(new("b0000000-0000-0000-0000-000000000029"), ReferenceDataCategory.FrameColour, "blue", "Blue");
        Add(new("b0000000-0000-0000-0000-000000000035"), ReferenceDataCategory.FrameColour, "blue_black", "Blue-Black");
        Add(new("b0000000-0000-0000-0000-000000000036"), ReferenceDataCategory.FrameColour, "brown_black", "Brown-Black");
        Add(new("b0000000-0000-0000-0000-000000000037"), ReferenceDataCategory.FrameColour, "purple", "Purple");
        Add(new("b0000000-0000-0000-0000-000000000038"), ReferenceDataCategory.FrameColour, "purple_black", "Purple-Black");
        Add(new("b0000000-0000-0000-0000-000000000039"), ReferenceDataCategory.FrameColour, "other", "Other", isOther: true);

        // HardCaseColour ← Orange/Green/Other (decision 5.1) — NOT Kobo's stale Blue/Pink/Purple/Black.
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000040"), ReferenceDataCategory.HardCaseColour, "orange", "Orange");
        Add(new("b0000000-0000-0000-0000-000000000041"), ReferenceDataCategory.HardCaseColour, "green", "Green");
        Add(new("b0000000-0000-0000-0000-000000000042"), ReferenceDataCategory.HardCaseColour, "other", "Other", isOther: true);

        // LensStrength ← the 16 distinct values across the two seeded preset catalogues (2026-08-05
        // rework — see CLAUDE.md's Admin Portal wiring (Preset Catalogues screen) section), taken
        // verbatim from the user-supplied screenshot of the real 6-Lens/9-Lens rosters. No coating
        // is baked into the label — which coatings each strength is available in is a separate
        // LensStrengthCoatingOption relationship, seeded (or left unconfigured) in
        // PresetCatalogueSeedConfiguration.
        sort = 0;
        Add(LensStrength300Id, ReferenceDataCategory.LensStrength, "plus_3_00", "+3.00");
        Add(LensStrength250Id, ReferenceDataCategory.LensStrength, "plus_2_50", "+2.50");
        Add(LensStrength200Id, ReferenceDataCategory.LensStrength, "plus_2_00", "+2.00");
        Add(LensStrength125Id, ReferenceDataCategory.LensStrength, "plus_1_25", "+1.25");
        Add(LensStrength000Id, ReferenceDataCategory.LensStrength, "plus_0_00", "+0.00");
        Add(LensStrengthMinus100Id, ReferenceDataCategory.LensStrength, "minus_1_00", "-1.00");
        Add(LensStrengthMinus150Id, ReferenceDataCategory.LensStrength, "minus_1_50", "-1.50");
        Add(LensStrengthMinus200Id, ReferenceDataCategory.LensStrength, "minus_2_00", "-2.00");
        Add(LensStrengthMinus250Id, ReferenceDataCategory.LensStrength, "minus_2_50", "-2.50");
        Add(LensStrengthMinus300Id, ReferenceDataCategory.LensStrength, "minus_3_00", "-3.00");
        Add(LensStrengthMinus400Id, ReferenceDataCategory.LensStrength, "minus_4_00", "-4.00");
        Add(LensStrengthMinus450Id, ReferenceDataCategory.LensStrength, "minus_4_50", "-4.50");
        Add(LensStrengthBifocal300Id, ReferenceDataCategory.LensStrength, "bifocal_0_00_3_00", "+0.00 / +3.00 (Bifocal)");
        Add(LensStrengthBifocal250Id, ReferenceDataCategory.LensStrength, "bifocal_0_00_2_50", "+0.00 / +2.50 (Bifocal)");
        Add(LensStrengthBifocal200Id, ReferenceDataCategory.LensStrength, "bifocal_0_00_2_00", "+0.00 / +2.00 (Bifocal)");
        Add(LensStrengthBifocal125Id, ReferenceDataCategory.LensStrength, "bifocal_0_00_1_25", "+0.00 / +1.25 (Bifocal)");

        builder.HasData(items);
    }
}
