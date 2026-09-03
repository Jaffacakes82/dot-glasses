using Asp.Versioning;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.ReferenceData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

/// <summary>
/// Read-only — backs dropdowns in the Field App and (eventually) the Admin Portal's own Reference
/// Data screen. Any authenticated role can read (a technician needs this to fill a form); editing
/// reference data is a separate, not-yet-built, DGI-only surface (see
/// AuthorizationPolicies.ReferenceDataManage, currently only gating the Admin Portal's
/// placeholder screen).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reference-data")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ReferenceDataController(IReferenceDataQueryService referenceDataQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReferenceDataItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await referenceDataQueryService.ListActiveAsync(cancellationToken));

    /// <summary>Coating pairing/exclusion rules — see ADR-0001. Fetched/cached by the Field App
    /// alongside the reference-data list above so live enforcement works offline.</summary>
    [HttpGet("coating-rules")]
    public async Task<ActionResult<CoatingRulesDto>> CoatingRules(CancellationToken cancellationToken) =>
        Ok(await referenceDataQueryService.GetCoatingRulesAsync(cancellationToken));
}
