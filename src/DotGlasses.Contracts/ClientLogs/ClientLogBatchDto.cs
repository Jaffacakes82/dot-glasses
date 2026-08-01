using FluentValidation;

namespace DotGlasses.Contracts.ClientLogs;

/// <summary>
/// Batch of client-side log entries shipped from the Field App to /api/client-logs, through
/// the same offline outbox/sync mechanism as business data — client errors are exactly as
/// likely to happen offline as online.
/// </summary>
public class ClientLogBatchDto
{
    /// <summary>Correlates this batch with server-side logs for the same client session.</summary>
    public Guid CorrelationId { get; set; }

    public List<ClientLogEntryDto> Entries { get; set; } = [];
}

public class ClientLogEntryDto
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

public class ClientLogBatchDtoValidator : AbstractValidator<ClientLogBatchDto>
{
    public ClientLogBatchDtoValidator()
    {
        RuleFor(x => x.CorrelationId).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Level).NotEmpty();
            entry.RuleFor(e => e.Message).NotEmpty().MaximumLength(4000);
        });
    }
}
