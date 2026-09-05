using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Leads;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DotGlasses.Web.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sales")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SalesController(
    ISaleService saleService,
    ILeadService leadService,
    ICurrentUserContext currentUser,
    IReferenceDataSnapshotProvider snapshots) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> List(CancellationToken cancellationToken) =>
        Ok(await saleService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await saleService.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Idempotent upsert keyed on <see cref="CreateSaleRequest.Id"/>. HierarchyPath/
    /// TechnicianUserId are stamped from the authenticated caller's claims, never from the
    /// request body — see TestsController.Create.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } technicianUserId || string.IsNullOrEmpty(currentUser.HierarchyPathPrefix))
        {
            return Problem("The authenticated user has no org assignment and cannot record a sale.", statusCode: StatusCodes.Status400BadRequest);
        }

        // One reference-data read for the whole request, then every rule answered in memory —
        // ADR-0002. A preset-range Sale used to cost 7 + 3n + n(n-1)/2 sequential lookups; the
        // provider is scoped and memoized, so this is the request's only load.
        var snapshot = await snapshots.GetAsync(cancellationToken);
        var modelState = ConsultationRules.Check(request, snapshot).ToModelStateDictionary();
        await AddSourceLeadFailureAsync(request, modelState, cancellationToken);

        if (!modelState.IsValid)
        {
            return ValidationProblem(modelState);
        }

        var dto = await saleService.CreateAsync(request, technicianUserId, currentUser.HierarchyPathPrefix, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }

    /// <summary>The Sale's half of the source check — see LeadsController.AddSourceTestFailureAsync
    /// for why it lives on the controller rather than in DotGlasses.Rules or SaleService.</summary>
    private async Task AddSourceLeadFailureAsync(
        CreateSaleRequest request, ModelStateDictionary modelState, CancellationToken cancellationToken)
    {
        if (request.SourceLeadId is not { } sourceLeadId)
        {
            return;
        }

        var lead = await leadService.GetByIdAsync(sourceLeadId, cancellationToken);
        if (lead is null)
        {
            modelState.AddModelError(nameof(request.SourceLeadId), "SourceLeadId must reference an existing Lead.");
        }
        else if (lead.SaleId is not null)
        {
            modelState.AddModelError(nameof(request.SourceLeadId), "This Lead has already been converted into a Sale.");
        }
    }
}
