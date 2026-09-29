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
///  - Coating uses the client's own 5-item list, NOT Kobo's 7-item coating_types list
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
    // Exposed for ExampleLensSets, which gives each example lens its coatings and lens type from
    // these.
    public static readonly Guid CoatingPhotochromicId = new("b0000000-0000-0000-0000-000000000023");
    public static readonly Guid CoatingClearId = new("b0000000-0000-0000-0000-000000000024");
    public static readonly Guid CoatingBlueBlockId = new("b0000000-0000-0000-0000-000000000025");
    public static readonly Guid LensTypeBifocalId = new("b0000000-0000-0000-0000-000000000059");

    // b0000000-…-000000000043 to …-000000000058 were the sixteen "Lens strength" items, deleted
    // with that category (ADR-0007, migration ResetLensSetsToLensPowers). Don't reuse them.

    public void Configure(EntityTypeBuilder<ReferenceDataItem> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var items = new List<ReferenceDataItem>();
        var sort = 0;

        void Add(Guid id, ReferenceDataCategory category, string code, string label, bool isOther = false, string? imageUrl = null)
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
                ImageUrl = imageUrl,
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

        // Coating ← the client's own 5-item list (not Kobo's 7-item coating_types), plus
        // Anti-glare (2026-09-03, ticket 05/ADR-0001) — seeded with no pairing/exclusion rules
        // of its own.
        sort = 0;
        Add(CoatingPhotochromicId, ReferenceDataCategory.Coating, "photochromic", "Photochromic");
        Add(CoatingClearId, ReferenceDataCategory.Coating, "clear", "Clear");
        Add(CoatingBlueBlockId, ReferenceDataCategory.Coating, "blue_block", "Blue block");
        Add(new("b0000000-0000-0000-0000-000000000026"), ReferenceDataCategory.Coating, "polarized", "Polarized");
        Add(new("b0000000-0000-0000-0000-000000000027"), ReferenceDataCategory.Coating, "sunglasses", "Sunglasses");
        Add(new("b0000000-0000-0000-0000-000000000062"), ReferenceDataCategory.Coating, "anti_glare", "Anti-glare");

        // FrameColour ← the 6 colours named on the call, matching the e-commerce site, + Other.
        // 2026-09-03: image icons added (ticket 11) — matched 1:1 onto these six rows by the
        // reporter. "Purple"/"Purple-Black" renamed to "Pink"/"Pink Black" in the same pass — the
        // reporter's own supplied image filenames and labelling call these "Pink"/"Pink Black";
        // nothing was actually misrendering, the internal label just didn't match the e-commerce
        // site's name for the same colour.
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000028"), ReferenceDataCategory.FrameColour, "black", "Black",
            imageUrl: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e780b5efa4a_Black_white.png");
        Add(new("b0000000-0000-0000-0000-000000000029"), ReferenceDataCategory.FrameColour, "blue", "Blue",
            imageUrl: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e780b5efd97_Blue_white_1.png");
        Add(new("b0000000-0000-0000-0000-000000000035"), ReferenceDataCategory.FrameColour, "blue_black", "Blue-Black",
            imageUrl: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e7927c5ea8c_Blue_white.png");
        Add(new("b0000000-0000-0000-0000-000000000036"), ReferenceDataCategory.FrameColour, "brown_black", "Brown-Black",
            imageUrl: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e7927c5ee4e_Brown_white.png");
        Add(new("b0000000-0000-0000-0000-000000000037"), ReferenceDataCategory.FrameColour, "pink", "Pink",
            imageUrl: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e780b5ef6b9_Purple_white.png");
        Add(new("b0000000-0000-0000-0000-000000000038"), ReferenceDataCategory.FrameColour, "pink_black", "Pink Black",
            imageUrl: "https://dotglasses.org/dot-glasses-ecommerce/assets/images/products/68e7914719e10_Purple_white_1.png");
        Add(new("b0000000-0000-0000-0000-000000000039"), ReferenceDataCategory.FrameColour, "other", "Other", isOther: true);

        // HardCaseColour ← Orange/Green/Other (decision 5.1) — NOT Kobo's stale Blue/Pink/Purple/Black.
        sort = 0;
        Add(new("b0000000-0000-0000-0000-000000000040"), ReferenceDataCategory.HardCaseColour, "orange", "Orange");
        Add(new("b0000000-0000-0000-0000-000000000041"), ReferenceDataCategory.HardCaseColour, "green", "Green");
        Add(new("b0000000-0000-0000-0000-000000000042"), ReferenceDataCategory.HardCaseColour, "other", "Other", isOther: true);

        // No "Lens strength" list any more: a lens set lens is a lens power with its own typed
        // label (ADR-0007), and the category was retired with its sixteen items.

        // LensType ← Bifocal/Progressive/Other, asked when a lens has an add (2026-09-03 triage;
        // ADR-0007).
        sort = 0;
        Add(LensTypeBifocalId, ReferenceDataCategory.LensType, "bifocal", "Bifocal");
        Add(new("b0000000-0000-0000-0000-000000000060"), ReferenceDataCategory.LensType, "progressive", "Progressive");
        Add(new("b0000000-0000-0000-0000-000000000061"), ReferenceDataCategory.LensType, "other", "Other", isOther: true);

        builder.HasData(items);
    }
}
