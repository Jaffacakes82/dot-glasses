using FluentValidation;

namespace DotGlasses.Contracts.WidgetExamples;

/// <summary>
/// Id is client-generated (by the Admin Portal's browser or the Field App's offline outbox)
/// so the same request shape doubles as the offline-sync payload: the server treats create as
/// an idempotent upsert keyed on Id, so a retried sync of the same record is a no-op.
/// </summary>
public class CreateWidgetExampleRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
}

public class CreateWidgetExampleRequestValidator : AbstractValidator<CreateWidgetExampleRequest>
{
    public CreateWidgetExampleRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.HierarchyPath)
            .NotEmpty()
            .Matches(@"^/(\d+/)+$")
            .WithMessage("HierarchyPath must be a materialized path like \"/1/4/12/\".");
    }
}
