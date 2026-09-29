using System.Globalization;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Web.Models;

public record CataloguesIndexViewModel(
    IReadOnlyList<CatalogueCard> Catalogues,
    IReadOnlyList<RetiredCatalogueCard> RetiredCatalogues,
    IReadOnlyList<(Guid Id, string Name)> AssignableOrgs,
    IReadOnlyList<(Guid Id, string Name)> OwningOrgOptions,
    string? Search,
    LensDialogViewModel LensDialog);

/// <summary>
/// The one Add lens dialog the screen renders (prototype variant A), shared by every lens set's
/// Add lens and Edit buttons, which fill it from their own data before showing it. The choices
/// are the active Coating and LensType items in their list order, and the global exclusions as
/// "A and B" for the note. <see cref="Reopen"/> is the admin's own posted form when a save was
/// refused: the screen is rendered straight back (not redirected) with the dialog open on it and
/// each problem in ModelState under its field's key — see CataloguesController.SaveLens.
/// </summary>
public record LensDialogViewModel(
    IReadOnlyList<LensDialogChoice> Coatings,
    IReadOnlyList<LensDialogChoice> LensTypes,
    IReadOnlyList<string> Exclusions,
    SaveLensRequest? Reopen,
    string? ReopenTitle);

public record LensDialogChoice(Guid Id, string Label, bool IsOtherOption = false);

/// <summary>
/// The dropdowns' option <em>values</em>, as opposed to their text (which is
/// <see cref="LensPowerValues.FormatPower"/>'s). A value is written in the request's culture
/// because that is the culture the model binder parses a posted form value with, so it
/// round-trips whatever the server's culture is; an Edit button's data uses the same strings so
/// the dialog can select the lens's own values by plain string equality.
/// </summary>
public static class LensDialogValues
{
    public static string Power(decimal value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    public static string Axis(decimal value) => value.ToString("0", CultureInfo.CurrentCulture);
}

/// <summary>What an Edit button opens the dialog with: the lens as stored, as option values.</summary>
public record LensEditValues(
    Guid Id,
    string Label,
    string Sphere,
    string Cylinder,
    string Axis,
    string Add,
    Guid? LensTypeRefId,
    string? LensTypeOtherText,
    IReadOnlyList<Guid> CoatingIds,
    IReadOnlyList<LensPairingField> Pairings);

public record RetiredCatalogueCard(Guid Id, string Name, bool CanReactivate);

public record CatalogueCard(Guid Id, string Name, string? Description, IReadOnlyList<LensOptionCard> LensOptions, IReadOnlyList<AssignedOrgCard> AssignedOrgs, bool CanEdit);

public record AssignedOrgCard(Guid OrgNodeId, string OrgName, bool CanUnassign);

/// <summary>One row of a lens set's lens table (ADR-0007): label, lens power, lens type, coatings
/// and pairings, each already rendered as text. The power is formatted by the Rules definition
/// (<see cref="LensPowerValues.FormatLensPower"/>), the one place the display format lives.</summary>
public record LensOptionCard(Guid Id, string Label, string LensPower, string LensType, IReadOnlyList<string> Coatings, IReadOnlyList<string> Pairings, LensEditValues Edit)
{
    /// <summary>Single vision is inferred when there is no lens type, never chosen — see
    /// LensPowerRules.LensType.</summary>
    public const string SingleVision = "Single vision";

    public static LensOptionCard From(PresetCatalogueLensOptionAdminDto lens) => new(
        lens.Id,
        lens.Label,
        LensPowerValues.FormatLensPower(lens.Sphere, lens.Cylinder, lens.Axis, lens.Add),
        lens.LensTypeLabel ?? SingleVision,
        lens.Coatings.Select(c => c.Label).ToList(),
        lens.Pairings.Select(p => $"{p.TriggerLabel} → {p.PairedLabel}").ToList(),
        new LensEditValues(
            lens.Id,
            lens.Label,
            LensDialogValues.Power(lens.Sphere),
            LensDialogValues.Power(lens.Cylinder ?? 0m),
            lens.Axis is { } axis ? LensDialogValues.Axis(axis) : "",
            LensDialogValues.Power(lens.Add ?? 0m),
            lens.LensTypeRefId,
            lens.LensTypeOtherText,
            lens.Coatings.Select(c => c.CoatingRefId).ToList(),
            lens.Pairings.Select(p => new LensPairingField { TriggerCoatingRefId = p.TriggerCoatingRefId, PairedCoatingRefId = p.PairedCoatingRefId }).ToList()));
}

public class CreateCatalogueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The Dgi/Country-level assignment chosen to own the new lens set. Only posted when
    /// the form showed the field, which happens only when more than one of the caller's own
    /// assignments qualifies — with a single qualifying assignment it is left null and resolved to
    /// that one automatically (CataloguesController.ResolveOwningOrgNodeIdAsync).</summary>
    public Guid? OwningOrgNodeId { get; set; }
}

public class UpdateCatalogueRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// The Add lens dialog's form (ADR-0007; prototype variant A) — adds a lens when
/// <see cref="LensOptionId"/> is null, and edits that lens otherwise. Checked by
/// SaveLensRequestValidator, whose failures are keyed on these property names, which are also the
/// dialog's field names — so each problem lands next to its own control. A pairing's failure is
/// keyed <c>Pairings[i]</c>, its row in the dialog.
/// </summary>
public class SaveLensRequest
{
    public Guid CatalogueId { get; set; }
    public Guid? LensOptionId { get; set; }
    public string? Label { get; set; }
    public decimal? Sphere { get; set; }
    public decimal? Cylinder { get; set; }
    public decimal? Axis { get; set; }
    public decimal? Add { get; set; }
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }
    public List<Guid> CoatingIds { get; set; } = [];
    public List<LensPairingField> Pairings { get; set; } = [];

    /// <summary>Only called once the validator has passed, so the label and sphere are present, and
    /// a pairing row left blank on both sides (the dialog's empty row) is simply not a pairing.</summary>
    public LensSetLensInput ToInput() => new(
        Label!,
        Sphere!.Value,
        Cylinder,
        Axis,
        Add,
        LensTypeRefId,
        LensTypeOtherText,
        CoatingIds,
        Pairings
            .Where(p => p.TriggerCoatingRefId is not null && p.PairedCoatingRefId is not null)
            .Select(p => new CoatingPairingRule(p.TriggerCoatingRefId!.Value, p.PairedCoatingRefId!.Value))
            .ToList());
}

/// <summary>One row of "Pairings for this lens": ticking the trigger brings the paired coating.</summary>
public class LensPairingField
{
    public Guid? TriggerCoatingRefId { get; set; }
    public Guid? PairedCoatingRefId { get; set; }
}

public class AssignCataloguesRequest
{
    public Guid OrgNodeId { get; set; }
    public List<Guid> CatalogueIds { get; set; } = [];
}

/// <summary>The Lens powers page (ticket 08): each value already formatted by
/// <see cref="LensPowerValues.FormatPower"/> (sphere, cylinder, add) or as a plain whole-number
/// string (axis in degrees, a Custom prescription's pupil distance in millimetres), in the shop's
/// order — the view renders these lists as given and states no bound of its own.</summary>
public record LensPowersViewModel(
    IReadOnlyList<string> Sphere,
    IReadOnlyList<string> Cylinder,
    IReadOnlyList<string> Axis,
    IReadOnlyList<string> Add,
    IReadOnlyList<string> PupilDistanceMm);
