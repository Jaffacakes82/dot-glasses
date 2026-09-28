using DotGlasses.Rules;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DotGlasses.Web.Validation;

/// <summary>
/// The two shapes a rejection arrives in, rendered into the one shape ASP.NET turns into a
/// ValidationProblemDetails. FluentValidation still backs the ten remaining Admin Portal
/// validators (ADR-0002 keeps it: several need async DB-backed rules); DotGlasses.Rules backs the
/// three consultation create endpoints. Both key on the request DTO's own property names, which is
/// what lets a Field App form error and an Admin Portal field error map back the same way.
/// </summary>
public static class ValidationResultExtensions
{
    public static ModelStateDictionary ToModelStateDictionary(this ValidationResult validationResult)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in validationResult.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return modelState;
    }

    public static ModelStateDictionary ToModelStateDictionary(this RuleResult ruleResult)
    {
        var modelState = new ModelStateDictionary();
        foreach (var failure in ruleResult.Failures)
        {
            modelState.AddModelError(failure.Key, failure.Message);
        }

        return modelState;
    }

    /// <summary>A ModelStateDictionary carrying one form-level failure, keyed on the empty
    /// string — the same slot DomainRuleViolationFilter uses for a thrown rejection, for a
    /// refusal a controller wants to return directly as a ValidationProblem instead (the Test/
    /// Lead/Sale create endpoints' current-location check — see ticket 07).</summary>
    public static ModelStateDictionary ToModelStateDictionary(this string formLevelMessage)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, formLevelMessage);
        return modelState;
    }
}
