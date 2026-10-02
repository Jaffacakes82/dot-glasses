using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;
using DotGlasses.Rules;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;
using DotGlasses.Web.Models;
using FluentValidation;
using ContractCategory = DotGlasses.Contracts.Common.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.PresetCatalogues;

/// <summary>
/// Everything the Add lens dialog checks when a lens is saved (ADR-0007; spec "Admin Portal"),
/// reported together so the admin can fix it all in one go. It is the only validator of a lens
/// set's lenses; a lens is a lens power with its own coatings, not a pick from a reference list.
///
/// <list type="bullet">
/// <item>The label is required, and unique within the set ignoring case.</item>
/// <item>The lens power is valid — <see cref="LensPowerRules.Check"/>, the same rule a custom
/// prescription is held to.</item>
/// <item>A lens type is required with an add, and only then — <see cref="LensPowerRules.LensType"/>.</item>
/// <item>At least one coating is ticked, and every ticked one is an active Coating.</item>
/// <item>No other lens in the set has the same power and lens type —
/// <see cref="LensSetLenses.Match"/>, the helper the device and the server use to find a lens.</item>
/// <item>Each pairing uses two ticked coatings, isn't self-paired or listed twice, and isn't
/// forbidden by a global exclusion.</item>
/// </list>
///
/// <para>
/// This runs inside a write to the lens-set library, so it must not read the memoised
/// per-request <see cref="ReferenceDataSnapshot"/> (CLAUDE.md, ADR-0002). It reads rows through
/// <see cref="IReferenceDataLookupService"/> and <see cref="IPresetCatalogueAdminService"/> instead,
/// and hands the Rules helpers a small literal snapshot built from them — just the items this lens
/// names, and the exclusions — which is what those helpers were written to accept.
/// </para>
///
/// <para>
/// The Rules helpers put a field's name in their message and use it as the failure's key. They
/// are given the names the dialog shows ("Spherical power", "Lens type"…) so the copy reads the
/// way the dialog does, and each failure is then re-keyed onto the request's own property name,
/// which is the field it is rendered against.
/// </para>
/// </summary>
public class SaveLensRequestValidator : AbstractValidator<SaveLensRequest>
{
    /// <summary>LensOptionConfiguration's column lengths.</summary>
    private const int LabelMaxLength = 100;
    private const int OtherTextMaxLength = 200;

    private static readonly LensPowerNames Shown = new("Spherical power", "Cylindrical power", "Axis", "Add near vision power");
    private const string LensTypeShown = "Lens type";
    private const string OtherTextShown = "Other lens type";

    private static readonly Dictionary<string, string> PropertyFor = new()
    {
        [Shown.Sphere] = nameof(SaveLensRequest.Sphere),
        [Shown.Cylinder] = nameof(SaveLensRequest.Cylinder),
        [Shown.Axis] = nameof(SaveLensRequest.Axis),
        [Shown.Add] = nameof(SaveLensRequest.Add),
        [LensTypeShown] = nameof(SaveLensRequest.LensTypeRefId),
        [OtherTextShown] = nameof(SaveLensRequest.LensTypeOtherText),
    };

    public SaveLensRequestValidator(IReferenceDataLookupService lookup, IPresetCatalogueAdminService catalogues)
    {
        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            void Fail(string key, string message) => context.AddFailure(key, message);
            void FailAll(IEnumerable<RuleFailure> failures)
            {
                foreach (var failure in failures)
                {
                    Fail(PropertyFor[failure.Key], failure.Message);
                }
            }

            // The set's other lenses — the one being edited is compared against everything but itself.
            var others = (await catalogues.ListLensesForCheckAsync(request.CatalogueId, cancellationToken))
                .Where(l => l.Id != request.LensOptionId)
                .ToList();

            // --- The literal snapshot: this lens's lens type and coatings, and the exclusions.
            var items = new List<ReferenceItemSnapshot>();
            if (request.LensTypeRefId is { } lensTypeId
                && await lookup.LookupAsync(lensTypeId, ReferenceDataCategory.LensType, cancellationToken) is { } lensType)
            {
                items.Add(new ReferenceItemSnapshot(lensTypeId, ContractCategory.LensType, lensType.Label, lensType.IsActive, lensType.IsOtherOption));
            }

            var coatingIds = request.CoatingIds.Distinct().ToList();
            foreach (var coatingId in coatingIds.Concat(request.Pairings.SelectMany(p => new[] { p.TriggerCoatingRefId, p.PairedCoatingRefId }).OfType<Guid>()).Distinct())
            {
                if (await lookup.LookupAsync(coatingId, ReferenceDataCategory.Coating, cancellationToken) is { } coating)
                {
                    items.Add(new ReferenceItemSnapshot(coatingId, ContractCategory.Coating, coating.Label, coating.IsActive, coating.IsOtherOption));
                }
            }

            var referenceData = new ReferenceDataSnapshot(items, [], await lookup.ListCoatingExclusionsAsync(cancellationToken));

            // --- Label
            var label = request.Label?.Trim();
            if (string.IsNullOrEmpty(label))
            {
                Fail(nameof(request.Label), "Enter the label technicians will see, e.g. +2.50.");
            }
            else if (label.Length > LabelMaxLength)
            {
                Fail(nameof(request.Label), $"Keep the label to {LabelMaxLength} characters or fewer.");
            }
            else if (others.Any(o => string.Equals(o.Label.Trim(), label, StringComparison.OrdinalIgnoreCase)))
            {
                Fail(nameof(request.Label), $"This lens set already has a lens labelled \"{label}\". Choose a label technicians can tell apart.");
            }

            // --- Lens power and lens type
            var powerFailures = LensPowerRules.Check(request.Sphere, request.Cylinder, request.Axis, request.Add, Shown).ToList();
            FailAll(powerFailures);

            var hasAdd = LensPowerRules.HasAdd(request.Add);
            var otherText = string.IsNullOrWhiteSpace(request.LensTypeOtherText) ? null : request.LensTypeOtherText.Trim();
            var lensTypeFailures = LensPowerRules.LensType(hasAdd, request.LensTypeRefId, otherText, referenceData, LensTypeShown, OtherTextShown).ToList();
            FailAll(lensTypeFailures);
            if (otherText is { Length: > OtherTextMaxLength })
            {
                Fail(nameof(request.LensTypeOtherText), $"Keep the other lens type to {OtherTextMaxLength} characters or fewer.");
            }

            // Only asked of a valid power and lens type: a duplicate of something that is itself
            // about to change would be a second message about the same fix.
            if (powerFailures.Count == 0 && lensTypeFailures.Count == 0
                && LensSetLenses.Match(others, request.Sphere, request.Cylinder, request.Axis, request.Add, hasAdd ? request.LensTypeRefId : null) is { } twin)
            {
                Fail(nameof(request.Sphere), $"This lens set already has a lens with this power and lens type (\"{twin.Label}\"). The same power can only appear again with a different lens type.");
            }

            // --- Coatings
            if (coatingIds.Count == 0)
            {
                Fail(nameof(request.CoatingIds), "Tick at least one coating this lens comes in.");
            }

            foreach (var coatingId in coatingIds.Where(id => !referenceData.IsActiveItem(id, ContractCategory.Coating)))
            {
                Fail(nameof(request.CoatingIds), referenceData.FindItem(coatingId, ContractCategory.Coating) is { } retired
                    ? $"{retired.Label} is no longer offered — untick it."
                    : "One of the ticked coatings doesn't exist — reload the page and try again.");
            }

            // --- Pairings for this lens. A row left blank on both sides is the dialog's empty row.
            var seen = new HashSet<CoatingPairingRule>();
            for (var i = 0; i < request.Pairings.Count; i++)
            {
                var key = $"{nameof(request.Pairings)}[{i}]";
                if (request.Pairings[i] is not { TriggerCoatingRefId: var trigger, PairedCoatingRefId: var paired } || (trigger is null && paired is null))
                {
                    continue;
                }

                if (trigger is not { } t || paired is not { } p)
                {
                    Fail(key, "Choose both coatings for this pairing, or remove it.");
                }
                else if (t == p)
                {
                    Fail(key, "A coating can't pair with itself.");
                }
                else if (!coatingIds.Contains(t) || !coatingIds.Contains(p))
                {
                    Fail(key, "Tick both coatings for this lens, or remove the pairing.");
                }
                else if (referenceData.AreCoatingsExcluded(t, p))
                {
                    Fail(key, $"{referenceData.ResolveLabel(t)} and {referenceData.ResolveLabel(p)} exclude each other, so they can't be paired.");
                }
                else if (!seen.Add(new CoatingPairingRule(t, p)))
                {
                    Fail(key, $"{referenceData.ResolveLabel(t)} → {referenceData.ResolveLabel(p)} is listed twice.");
                }
            }
        });
    }
}
