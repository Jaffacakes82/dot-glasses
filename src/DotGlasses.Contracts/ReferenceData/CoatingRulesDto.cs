namespace DotGlasses.Contracts.ReferenceData;

/// <summary>Symmetric: CoatingRefIdA and CoatingRefIdB can never both be selected at once — see
/// ADR-0001.</summary>
public class CoatingExclusionDto
{
    public Guid Id { get; set; }
    public Guid CoatingRefIdA { get; set; }
    public Guid CoatingRefIdB { get; set; }
}

/// <summary>The global coating rules the Field App fetches and caches alongside reference data, so
/// the coating picker works offline. Exclusions only: pairings left this payload when they moved
/// onto each lens set lens (ADR-0007, "Coatings") — see LensOptionDto.Pairings.</summary>
public class CoatingRulesDto
{
    public List<CoatingExclusionDto> Exclusions { get; set; } = [];
}
