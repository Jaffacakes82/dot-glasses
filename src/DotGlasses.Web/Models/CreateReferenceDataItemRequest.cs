using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public class CreateReferenceDataItemRequest
{
    public ReferenceDataCategory Category { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsOtherOption { get; set; }
}
