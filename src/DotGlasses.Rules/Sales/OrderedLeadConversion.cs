using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules.LensPowers;

namespace DotGlasses.Rules.Sales;

/// <summary>
/// Converting a Lead whose lens is already ordered (ADR-0008): the lab is making what the Lead
/// ordered, so the Sale has to keep that lens and that Coating set, and it shares the Lead's order
/// rather than placing another. A changed prescription is a new Sale recorded from scratch.
///
/// Pure, over the two DTOs, so every caller asks the one rule: the Field App before it queues a
/// conversion, the Sale endpoint and the Admin Portal's conversion screen beside their
/// SourceLeadId check, and SaleService as defence in depth. Failures are keyed on the Sale
/// request's own property names like every other rule.
/// </summary>
public static class OrderedLeadConversion
{
    public const string LensLockedMessage =
        "This lens is already ordered, so it can't be changed here. To sell a different lens, record a new sale.";

    public const string CoatingsLockedMessage =
        "The coatings were ordered with this lens, so they can't be changed here. To sell different coatings, record a new sale.";

    public const string AlreadyOrderedMessage =
        "This lens is already ordered — untick \"Order this lens from Dot Glasses\".";

    /// <summary>Valid for a Lead that placed no order: nothing is locked.</summary>
    public static RuleResult Check(CreateSaleRequest sale, LeadDto lead) =>
        RuleResult.From(lead.OrderFromDotGlasses ? Failures(sale, lead) : []);

    private static IEnumerable<RuleFailure> Failures(CreateSaleRequest sale, LeadDto lead)
    {
        // Compared as a record stores them (LensPowerRules.Normalise), so a 0.00 cylinder against
        // no cylinder is the same lens, not a change.
        var saleLeft = LensPowerRules.Normalise(sale.CylinderLeft, sale.AxisLeft, sale.AddLeft);
        var saleRight = LensPowerRules.Normalise(sale.CylinderRight, sale.AxisRight, sale.AddRight);
        var leadLeft = LensPowerRules.Normalise(lead.CylinderLeft, lead.AxisLeft, lead.AddLeft);
        var leadRight = LensPowerRules.Normalise(lead.CylinderRight, lead.AxisRight, lead.AddRight);

        var lens = new (string Key, bool Differs)[]
        {
            (nameof(CreateSaleRequest.LensRangeType), sale.LensRangeType != lead.LensRangeType),
            (nameof(CreateSaleRequest.PresetCatalogueId), sale.PresetCatalogueId != lead.PresetCatalogueId),
            (nameof(CreateSaleRequest.SphereLeft), sale.SphereLeft != lead.SphereLeft),
            (nameof(CreateSaleRequest.CylinderLeft), saleLeft.Cylinder != leadLeft.Cylinder),
            (nameof(CreateSaleRequest.AxisLeft), saleLeft.Axis != leadLeft.Axis),
            (nameof(CreateSaleRequest.AddLeft), saleLeft.Add != leadLeft.Add),
            (nameof(CreateSaleRequest.SphereRight), sale.SphereRight != lead.SphereRight),
            (nameof(CreateSaleRequest.CylinderRight), saleRight.Cylinder != leadRight.Cylinder),
            (nameof(CreateSaleRequest.AxisRight), saleRight.Axis != leadRight.Axis),
            (nameof(CreateSaleRequest.AddRight), saleRight.Add != leadRight.Add),
            (nameof(CreateSaleRequest.LensTypeRefId), sale.LensTypeRefId != lead.LensTypeRefId),
            (nameof(CreateSaleRequest.LensTypeOtherText), !SameText(sale.LensTypeOtherText, lead.LensTypeOtherText)),
            (nameof(CreateSaleRequest.PupilDistanceMm), sale.PupilDistanceMm != lead.PupilDistanceMm),
        };

        foreach (var (key, differs) in lens)
        {
            if (differs)
            {
                yield return new RuleFailure(key, LensLockedMessage);
            }
        }

        if (!sale.CoatingRefIds.ToHashSet().SetEquals(lead.CoatingRefIds))
        {
            yield return new RuleFailure(nameof(CreateSaleRequest.CoatingRefIds), CoatingsLockedMessage);
        }

        if (sale.OrderFromDotGlasses)
        {
            yield return new RuleFailure(nameof(CreateSaleRequest.OrderFromDotGlasses), AlreadyOrderedMessage);
        }
    }

    private static bool SameText(string? a, string? b) =>
        string.Equals(string.IsNullOrWhiteSpace(a) ? null : a.Trim(), string.IsNullOrWhiteSpace(b) ? null : b.Trim(), StringComparison.Ordinal);
}
