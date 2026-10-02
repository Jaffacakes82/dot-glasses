namespace DotGlasses.Web.Models;

public class UpdateReferenceDataItemRequest
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>A new picture to replace the current one. Absent leaves the picture as it is.</summary>
    public IFormFile? Picture { get; set; }

    /// <summary>Takes the current picture down without a replacement.</summary>
    public bool RemovePicture { get; set; }
}
