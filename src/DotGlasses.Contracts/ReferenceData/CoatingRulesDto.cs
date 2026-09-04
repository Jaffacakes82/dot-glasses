namespace DotGlasses.Contracts.ReferenceData;

/// <summary>Directional: selecting TriggerCoatingRefId auto-adds PairedCoatingRefId — see
/// ADR-0001. Fetched/cached by the Field App alongside reference data so the pairing/exclusion
/// UI works offline.</summary>
public class CoatingPairingDto
{
    public Guid Id { get; set; }
    public Guid TriggerCoatingRefId { get; set; }
    public Guid PairedCoatingRefId { get; set; }
}

/// <summary>Symmetric: CoatingRefIdA and CoatingRefIdB can never both be selected at once — see
/// ADR-0001.</summary>
public class CoatingExclusionDto
{
    public Guid Id { get; set; }
    public Guid CoatingRefIdA { get; set; }
    public Guid CoatingRefIdB { get; set; }
}

public class CoatingRulesDto
{
    public List<CoatingPairingDto> Pairings { get; set; } = [];
    public List<CoatingExclusionDto> Exclusions { get; set; } = [];
}
