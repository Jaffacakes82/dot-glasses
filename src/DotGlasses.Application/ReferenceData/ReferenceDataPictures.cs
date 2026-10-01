using System.Text.RegularExpressions;

namespace DotGlasses.Application.ReferenceData;

/// <summary>
/// Where an uploaded reference-data picture (a frame colour's) is kept. The storage is private:
/// nothing reads it but this, and the Admin Portal serves a picture back by its generated name.
/// </summary>
public interface IReferenceDataPictureStore
{
    /// <summary>Stores the picture under a new generated name and returns that name. Names never
    /// repeat, which is what lets the served copy be cached for good.</summary>
    Task<string> SaveAsync(Stream content, ReferenceDataPictureType type, CancellationToken cancellationToken = default);

    /// <summary>The stored picture, or null when there is none by that name.</summary>
    Task<Stream?> OpenAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Removes a stored picture. A name that isn't there is not an error.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}

public record ReferenceDataPictureType(string ContentType, string Extension);

/// <summary>
/// The rules for a reference-data picture, in one place: what may be uploaded, what a stored
/// picture is called, and the address it is served from. Nothing here does I/O.
/// </summary>
public static partial class ReferenceDataPictures
{
    public const int MaxBytes = 1024 * 1024;

    public const string TypeMessage = "Upload a PNG, JPEG or WebP picture.";
    public const string SizeMessage = "Upload a picture of 1 MB or smaller.";
    public const string EmptyMessage = "That file is empty. Choose a picture to upload.";

    /// <summary>The Admin Portal path a stored picture is served from. An item's ImageUrl holds
    /// this path, not a full address, so the same row works on every host; the Field App resolves
    /// it against its API address.</summary>
    public const string PathPrefix = "/reference-data/pictures/";

    private static readonly ReferenceDataPictureType Png = new("image/png", "png");
    private static readonly ReferenceDataPictureType Jpeg = new("image/jpeg", "jpg");
    private static readonly ReferenceDataPictureType WebP = new("image/webp", "webp");

    /// <summary>
    /// What kind of picture these bytes are, read from the file's own signature — never from its
    /// name or the content type the browser claimed, both of which the uploader controls. Null
    /// for anything that isn't a PNG, JPEG or WebP.
    /// </summary>
    public static ReferenceDataPictureType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (header.Length >= 3 && header[..3].SequenceEqual<byte>([0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        return null;
    }

    /// <summary>How many leading bytes <see cref="Detect"/> needs.</summary>
    public const int SignatureLength = 12;

    /// <summary>A new, unguessable name for a picture of this type.</summary>
    public static string NewName(ReferenceDataPictureType type) => $"{Guid.NewGuid():N}.{type.Extension}";

    /// <summary>Whether a name is one <see cref="NewName"/> could have produced. The serving
    /// endpoint accepts nothing else, so it can't be pointed at any other blob or path.</summary>
    public static bool IsStoredName(string? name) => name is not null && StoredName().IsMatch(name);

    public static string ContentTypeOf(string storedName) => Path.GetExtension(storedName) switch
    {
        ".png" => Png.ContentType,
        ".jpg" => Jpeg.ContentType,
        _ => WebP.ContentType,
    };

    public static string UrlFor(string storedName) => PathPrefix + storedName;

    /// <summary>The stored name behind an item's ImageUrl, or null when the address isn't one of
    /// ours — an old pasted web address, say, which has no blob to delete.</summary>
    public static string? StoredNameOf(string? imageUrl) =>
        imageUrl is not null && imageUrl.StartsWith(PathPrefix, StringComparison.Ordinal) && IsStoredName(imageUrl[PathPrefix.Length..])
            ? imageUrl[PathPrefix.Length..]
            : null;

    [GeneratedRegex("^[0-9a-f]{32}\\.(png|jpg|webp)$")]
    private static partial Regex StoredName();
}

/// <summary>What an edit does to an item's picture.</summary>
public abstract record ReferenceDataPictureChange
{
    private ReferenceDataPictureChange()
    {
    }

    /// <summary>Leave it as it is.</summary>
    public sealed record Keep : ReferenceDataPictureChange;

    /// <summary>Take it down; the item shows the placeholder.</summary>
    public sealed record Remove : ReferenceDataPictureChange;

    /// <summary>Point the item at a newly stored picture.</summary>
    public sealed record Replace(string ImageUrl) : ReferenceDataPictureChange;
}
