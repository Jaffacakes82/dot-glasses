using DotGlasses.Application.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

/// <summary>
/// Serves an uploaded reference-data picture (a frame colour's) out of the private storage
/// container. Anonymous on purpose: the Field App shows these on its Sale form and keeps copies
/// for offline use, and a frame colour swatch is not confidential. What keeps it narrow is the
/// name — only one <see cref="ReferenceDataPictures.NewName"/> could have generated is looked up,
/// so the endpoint can't be pointed at anything else in storage.
/// </summary>
[AllowAnonymous]
public class ReferenceDataPicturesController(IReferenceDataPictureStore pictureStore) : Controller
{
    [HttpGet(ReferenceDataPictures.PathPrefix + "{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        if (!ReferenceDataPictures.IsStoredName(name)
            || await pictureStore.OpenAsync(name, cancellationToken) is not { } picture)
        {
            return NotFound();
        }

        // A name is never reused, so what it names never changes.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        // Always, not only when the request carried an Origin (which is when the CORS middleware
        // adds it): the Field App loads a picture in an <img> first, with no Origin, and then
        // fetches the same address to keep a copy for offline use. Without this the browser
        // answers that fetch from its cache with a response that has no CORS header, and the copy
        // is never made.
        Response.Headers.Append("Vary", "Origin");
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(picture, ReferenceDataPictures.ContentTypeOf(name));
    }
}
