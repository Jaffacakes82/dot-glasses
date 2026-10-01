using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Common;

namespace DotGlasses.Infrastructure.Storage;

/// <summary>
/// Reference-data pictures in the private <c>reference-data-images</c> blob container that AppHost
/// provisions (Azurite locally). The container has no public access: a picture leaves it only
/// through the Admin Portal's serving endpoint.
/// </summary>
public class BlobReferenceDataPictureStore(BlobContainerClient container) : IReferenceDataPictureStore
{
    public async Task<string> SaveAsync(Stream content, ReferenceDataPictureType type, CancellationToken cancellationToken = default)
    {
        // AppHost declares the container, so this is a no-op everywhere it has run; it covers a
        // fresh emulator. Private access is the default and is never widened here.
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var name = ReferenceDataPictures.NewName(type);
        await container.GetBlobClient(name).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = type.ContentType } },
            cancellationToken);
        return name;
    }

    public async Task<Stream?> OpenAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await container.GetBlobClient(name).OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        await container.GetBlobClient(name).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}

/// <summary>Stands in where no storage is connected — a plain <c>dotnet run</c> of the Web project
/// outside AppHost, or design-time tooling. Existing pictures simply aren't there; an upload is
/// refused with a message rather than failing on a missing connection.</summary>
public class UnavailableReferenceDataPictureStore : IReferenceDataPictureStore
{
    public Task<string> SaveAsync(Stream content, ReferenceDataPictureType type, CancellationToken cancellationToken = default) =>
        throw new DomainRuleViolationException("Pictures can't be uploaded here: picture storage isn't connected in this environment.");

    public Task<Stream?> OpenAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
