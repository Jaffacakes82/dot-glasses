using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tests")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TestsController(
    IVisionTestService testService,
    ICurrentUserContext currentUser,
    IReferenceDataSnapshotProvider snapshots) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestDto>>> List(CancellationToken cancellationToken) =>
        Ok(await testService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TestDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await testService.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Idempotent upsert keyed on <see cref="CreateTestRequest.Id"/> — the endpoint the Field
    /// App's offline sync outbox replays against. HierarchyPath/TechnicianUserId are stamped
    /// from the authenticated caller's claims, never from the request body.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TestDto>> Create(CreateTestRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } technicianUserId || string.IsNullOrEmpty(currentUser.HierarchyPathPrefix))
        {
            return Problem("The authenticated user has no org assignment and cannot record a test.", statusCode: StatusCodes.Status400BadRequest);
        }

        // One reference-data read for the whole request, then every rule answered in memory —
        // ADR-0002. The provider is scoped and memoized, so this is the request's only load.
        var snapshot = await snapshots.GetAsync(cancellationToken);
        var rules = ConsultationRules.Check(request, snapshot);
        if (!rules.IsValid)
        {
            return ValidationProblem(rules.ToModelStateDictionary());
        }

        var dto = await testService.CreateAsync(request, technicianUserId, currentUser.HierarchyPathPrefix, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }
}
