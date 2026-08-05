using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.ReferenceData;

public class ReferenceDataItemDto
{
    public Guid Id { get; set; }
    public ReferenceDataCategory Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsOtherOption { get; set; }
    public string? ImageUrl { get; set; }
}
