using Asp.Versioning;
using DotGlasses.Application.WidgetExamples;
using DotGlasses.Contracts.WidgetExamples;
using DotGlasses.Web.Authorization;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/widget-examples")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class WidgetExamplesController(
    IWidgetExampleService widgetExampleService,
    IValidator<CreateWidgetExampleRequest> createValidator,
    IValidator<UpdateWidgetExampleRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WidgetExampleDto>>> List(CancellationToken cancellationToken) =>
        Ok(await widgetExampleService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WidgetExampleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await widgetExampleService.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Idempotent upsert keyed on <see cref="CreateWidgetExampleRequest.Id"/> — this is also
    /// the endpoint the Field App's offline sync outbox replays against, so a retried sync of
    /// the same client-generated Id never creates a duplicate.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.WidgetExampleCreate)]
    public async Task<ActionResult<WidgetExampleDto>> Create(CreateWidgetExampleRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        var dto = await widgetExampleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WidgetExampleDto>> Update(Guid id, UpdateWidgetExampleRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        var dto = await widgetExampleService.UpdateAsync(id, request, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await widgetExampleService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
