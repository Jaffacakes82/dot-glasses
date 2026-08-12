namespace DotGlasses.Web.Models;

public class UpdateReferenceDataItemRequest
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
