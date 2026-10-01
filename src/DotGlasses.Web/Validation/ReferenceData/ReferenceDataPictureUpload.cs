using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Validation.ReferenceData;

/// <summary>
/// The checks an uploaded reference-data picture has to pass, shared by the create and update
/// validators and by the controller that then stores it. The type is read from the file's own
/// signature (<see cref="ReferenceDataPictures.Detect"/>) — the file name and the content type the
/// browser sent are both the uploader's to choose, so neither is trusted.
/// </summary>
public static class ReferenceDataPictureUpload
{
    public const string WrongListMessage = "Only the frame colour lists have pictures.";

    /// <summary>The lists whose options carry a picture.</summary>
    public static bool TakesPictures(ReferenceDataCategory category) =>
        category is ReferenceDataCategory.FrameColour or ReferenceDataCategory.FrameColourChild;

    /// <summary>The picture's type when it may be stored, otherwise the message saying what to
    /// change.</summary>
    public static async Task<(ReferenceDataPictureType? Type, string? Error)> CheckAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return (null, ReferenceDataPictures.EmptyMessage);
        }

        if (file.Length > ReferenceDataPictures.MaxBytes)
        {
            return (null, ReferenceDataPictures.SizeMessage);
        }

        var header = new byte[ReferenceDataPictures.SignatureLength];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);

        return ReferenceDataPictures.Detect(header.AsSpan(0, read)) is { } type
            ? (type, null)
            : (null, ReferenceDataPictures.TypeMessage);
    }
}
