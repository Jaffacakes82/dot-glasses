using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Rules.LensPowers;

namespace DotGlasses.Web.Models;

public record CataloguesIndexViewModel(
    IReadOnlyList<CatalogueCard> Catalogues,
    IReadOnlyList<RetiredCatalogueCard> RetiredCatalogues,
    IReadOnlyList<(Guid Id, string Name)> AssignableOrgs,
    IReadOnlyList<(Guid Id, string Name)> OwningOrgOptions,
    string? Search);

public record RetiredCatalogueCard(Guid Id, string Name, bool CanReactivate);

public record CatalogueCard(Guid Id, string Name, string? Description, IReadOnlyList<LensOptionCard> LensOptions, IReadOnlyList<AssignedOrgCard> AssignedOrgs, bool CanEdit);

public record AssignedOrgCard(Guid OrgNodeId, string OrgName, bool CanUnassign);

/// <summary>One row of a lens set's lens table (ADR-0007): label, lens power, lens type, coatings
/// and pairings, each already rendered as text. The power is formatted by the Rules definition
/// (<see cref="LensPowerValues.FormatPower"/>), the one place the display format lives.</summary>
public record LensOptionCard(Guid Id, string Label, string LensPower, string LensType, IReadOnlyList<string> Coatings, IReadOnlyList<string> Pairings)
{
    /// <summary>Single vision is inferred when there is no lens type, never chosen — see
    /// LensPowerRules.LensType.</summary>
    public const string SingleVision = "Single vision";

    public static LensOptionCard From(PresetCatalogueLensOptionAdminDto lens) => new(
        lens.Id,
        lens.Label,
        FormatLensPower(lens.Sphere, lens.Cylinder, lens.Axis, lens.Add),
        lens.LensTypeLabel ?? SingleVision,
        lens.Coatings.Select(c => c.Label).ToList(),
        lens.Pairings.Select(p => $"{p.TriggerLabel} → {p.PairedLabel}").ToList());

    /// <summary>"SPH +2.50", then "CYL -0.75 × 90" and "ADD +2.00" only when the lens has them —
    /// a blank or 0.00 cylinder and a blank or 0.00 add are absent, per LensPowerRules.</summary>
    public static string FormatLensPower(decimal sphere, decimal? cylinder, decimal? axis, decimal? add)
    {
        var parts = new List<string> { $"SPH {LensPowerValues.FormatPower(sphere)}" };
        if (LensPowerRules.HasCylinder(cylinder))
        {
            parts.Add(axis is { } a ? $"CYL {LensPowerValues.FormatPower(cylinder!.Value)} × {a:0}" : $"CYL {LensPowerValues.FormatPower(cylinder!.Value)}");
        }

        if (LensPowerRules.HasAdd(add))
        {
            parts.Add($"ADD {LensPowerValues.FormatPower(add!.Value)}");
        }

        return string.Join(" · ", parts);
    }
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

public class AssignCataloguesRequest
{
    public Guid OrgNodeId { get; set; }
    public List<Guid> CatalogueIds { get; set; } = [];
}
