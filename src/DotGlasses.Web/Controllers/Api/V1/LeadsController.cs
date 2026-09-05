using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Leads;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Leads;
using DotGlasses.Rules;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DotGlasses.Web.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/leads")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LeadsController(
    ILeadService leadService,
    IVisionTestService testService,
    ICurrentUserContext currentUser,
    IReferenceDataSnapshotProvider snapshots) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> List(CancellationToken cancellationToken) =>
        Ok(await leadService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeadDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await leadService.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>ConvertedFlag == false only — backs the Field App's leads worklist.</summary>
    [HttpGet("open")]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> ListOpen(CancellationToken cancellationToken) =>
        Ok(await leadService.ListOpenAsync(cancellationToken));

    /// <summary>The most recent open Lead for an exact name+phone match, or 204 if none — backs
    /// the Field App's "convert this instead?" prompt when recording a Sale.</summary>
    [HttpGet("match")]
    public async Task<ActionResult<LeadDto>> Match([FromQuery] string fullName, [FromQuery] string? phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.HierarchyPathPrefix))
        {
            return Problem("The authenticated user has no org assignment.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest();
        }

        var match = await leadService.FindOpenMatchAsync(currentUser.HierarchyPathPrefix, fullName, phoneNumber, cancellationToken);
        return match is null ? NoContent() : Ok(match);
    }

    /// <summary>
    /// Idempotent upsert keyed on <see cref="CreateLeadRequest.Id"/>. HierarchyPath/
    /// TechnicianUserId are stamped from the authenticated caller's claims, never from the
    /// request body — see TestsController.Create.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create(CreateLeadRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } technicianUserId || string.IsNullOrEmpty(currentUser.HierarchyPathPrefix))
        {
            return Problem("The authenticated user has no org assignment and cannot record a lead.", statusCode: StatusCodes.Status400BadRequest);
        }

        // One reference-data read for the whole request, then every rule answered in memory —
        // ADR-0002. The provider is scoped and memoized, so this is the request's only load.
        var snapshot = await snapshots.GetAsync(cancellationToken);
        var modelState = ConsultationRules.Check(request, snapshot).ToModelStateDictionary();
        await AddSourceTestFailureAsync(request, modelState, cancellationToken);

        if (!modelState.IsValid)
        {
            return ValidationProblem(modelState);
        }

        var dto = await leadService.CreateAsync(request, technicianUserId, currentUser.HierarchyPathPrefix, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }

    /// <summary>
    /// The one consultation rule that can never live in DotGlasses.Rules: it resolves a specific
    /// hierarchy-scoped row rather than answering from the reference-data snapshot, so the module's
    /// synchronous snapshot-only signature deliberately has no room for it (see ConsultationRules'
    /// doc comment). It sits here rather than being left to LeadService's own refusal because the
    /// two produce different response shapes: the service throws DomainRuleViolationException,
    /// which DomainRuleViolationFilter renders keyed on "", while the Field App needs the failure
    /// against the control that produced it. The service's guard stays as defence in depth — it is
    /// what makes a half-completed conversion impossible — and this is the net in front of it.
    /// ConversionSourceScopingApiTests pins that ordering.
    ///
    /// An out-of-scope Test is indistinguishable from one that never existed, deliberately: the
    /// lookup goes through the same hierarchy-scoped read the service uses, so a caller learns
    /// nothing about records outside their own subtree.
    /// </summary>
    private async Task AddSourceTestFailureAsync(
        CreateLeadRequest request, ModelStateDictionary modelState, CancellationToken cancellationToken)
    {
        if (request.SourceTestId is not { } sourceTestId)
        {
            return;
        }

        var test = await testService.GetByIdAsync(sourceTestId, cancellationToken);
        if (test is null)
        {
            modelState.AddModelError(nameof(request.SourceTestId), "SourceTestId must reference an existing Test.");
        }
        else if (test.ConvertedToLeadId is not null)
        {
            modelState.AddModelError(nameof(request.SourceTestId), "This Test has already been converted into a Lead.");
        }
    }
}
