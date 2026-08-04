using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Contracts.PresetCatalogues;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

/// <summary>
/// Read-only — backs the Field App's lens-range picker. Distinct from the MVC
/// CataloguesController (Admin Portal's placeholder Preset Catalogues screen, different
/// namespace/route) — that one is for DGI/Country admins to create/assign catalogues, this one is
/// for any technician to see which catalogues their own retail point can use.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/preset-catalogues")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PresetCataloguesController(
    IPresetCatalogueQueryService presetCatalogueQueryService,
    ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PresetCatalogueDto>>> List(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.HierarchyPathPrefix))
        {
            return Problem("The authenticated user has no org assignment and cannot list preset catalogues.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await presetCatalogueQueryService.ListAvailableForCallerAsync(currentUser.HierarchyPathPrefix, cancellationToken));
    }
}
