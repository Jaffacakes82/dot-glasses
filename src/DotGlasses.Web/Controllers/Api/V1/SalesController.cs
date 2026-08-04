using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Sales;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sales")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SalesController(
    ISaleService saleService,
    ICurrentUserContext currentUser,
    IValidator<CreateSaleRequest> createValidator) : ControllerBase
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

        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        var dto = await saleService.CreateAsync(request, technicianUserId, currentUser.HierarchyPathPrefix, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }
}
