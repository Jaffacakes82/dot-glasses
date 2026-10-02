using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Leads;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules;
using DotGlasses.Rules.Sales;
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
    /// TechnicianUserId are stamped from the authenticated caller and their validated current
    /// location, never from the request body — see TestsController.Create.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } technicianUserId)
        {
            return Problem("The authenticated request has no user id.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Gates on the current location before anything else: a technician with no valid
        // location to record at gets the reason why, not a validation report against a body
        // that was never going anywhere (ticket 07, spec.md "Recording").
        if (currentUser.CurrentLocation.RefusalMessage is { } refusal)
        {
            return ValidationProblem(refusal.ToModelStateDictionary());
        }

        // One reference-data read for the whole request, then every rule answered in memory —
        // ADR-0002. A preset-range Sale used to cost 7 + 3n + n(n-1)/2 sequential lookups; the
        // provider is scoped and memoized, so this is the request's only load.
        // Placed where the record will be stamped, so a lens set is checked against the caller's
        // current location (ADR-0005).
        var snapshot = (await snapshots.GetAsync(cancellationToken)).AtLocation(RecordingPath);
        var modelState = ConsultationRules.Check(request, snapshot).ToModelStateDictionary();
        await AddSourceLeadFailureAsync(request, modelState, cancellationToken);

        if (!modelState.IsValid)
        {
            return ValidationProblem(modelState);
        }

        var dto = await saleService.CreateAsync(request, technicianUserId, RecordingPath, cancellationToken);
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
            modelState.AddModelError(nameof(request.SourceLeadId), "The lead this sale converts can't be found at this location. Discard this record and record the sale again.");
        }
        else if (lead.SaleId == request.Id)
        {
            // This Sale already converted this Lead: the outbox is retrying a record whose first
            // answer never arrived. Nothing to refuse — SaleService answers with the Sale that
            // exists and writes nothing.
        }
        else if (lead.SaleId is not null)
        {
            modelState.AddModelError(nameof(request.SourceLeadId), "This lead has already been converted into a sale. Discard this record.");
        }
        else
        {
            // A Lead whose lens is already ordered locks the Sale's lens and coatings to what was
            // ordered (ADR-0008). Reported here, field by field, for the same reason the source
            // check is: SaleService guards it too, but as one unkeyed sentence the Field App
            // can't put against a control.
            // On the locked fields the lock replaces the ordinary rules (OrderedLeadConversion.Over):
            // an ordered lens must stay sellable after one of its coatings is retired.
            if (lead.OrderFromDotGlasses)
            {
                foreach (var key in OrderedLeadConversion.LockedKeys)
                {
                    modelState.Remove(key);
                }
            }

            foreach (var failure in OrderedLeadConversion.Check(request, lead).Failures)
            {
                modelState.AddModelError(failure.Key, failure.Message);
            }
        }
    }

    /// <summary>Where a record is stamped: the validated current location, or "" when there is
    /// none — which the service refuses (ticket 07 gives each reason its own message).</summary>
    private string RecordingPath => currentUser.CurrentLocation.ValidLocation?.Path.Value ?? string.Empty;
}
