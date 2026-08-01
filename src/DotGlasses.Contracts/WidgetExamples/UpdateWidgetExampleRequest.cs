using FluentValidation;

namespace DotGlasses.Contracts.WidgetExamples;

public class UpdateWidgetExampleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateWidgetExampleRequestValidator : AbstractValidator<UpdateWidgetExampleRequest>
{
    public UpdateWidgetExampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
