using Asp.Versioning;
using DotGlasses.Contracts.ClientLogs;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

/// <summary>
/// Receives batched client-side log entries from the Field App (brief 3.5a) — shipped through
/// the same offline outbox/sync mechanism as business data, since client errors are exactly as
/// likely offline as online. Each batch carries a correlation id also logged here so a support
/// issue can be traced across both sides.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/client-logs")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ClientLogsController(
    ILogger<ClientLogsController> logger,
    IValidator<ClientLogBatchDto> validator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(ClientLogBatchDto batch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(batch, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        using (logger.BeginScope(new Dictionary<string, object> { ["ClientCorrelationId"] = batch.CorrelationId }))
        {
            foreach (var entry in batch.Entries)
            {
                logger.LogInformation(
                    "Client log [{Level}] {Category}: {Message} (client time {TimestampUtc:o}){ExceptionSuffix}",
                    entry.Level,
                    entry.Category,
                    entry.Message,
                    entry.TimestampUtc,
                    entry.Exception is null ? string.Empty : $"\n{entry.Exception}");
            }
        }

        return Accepted();
    }
}
