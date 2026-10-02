using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public class CreateReferenceDataItemRequest
{
    public ReferenceDataCategory Category { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>An uploaded picture, for the frame colour lists only. There is no address to
    /// type: a picture is uploaded or absent.</summary>
    public IFormFile? Picture { get; set; }
    public bool IsOtherOption { get; set; }
}
