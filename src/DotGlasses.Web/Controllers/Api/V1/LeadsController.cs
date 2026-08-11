using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Leads;
using DotGlasses.Contracts.Leads;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/leads")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LeadsController(
    ILeadService leadService,
    ICurrentUserContext currentUser,
    IValidator<CreateLeadRequest> createValidator) : ControllerBase
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

        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        var dto = await leadService.CreateAsync(request, technicianUserId, currentUser.HierarchyPathPrefix, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }
}
