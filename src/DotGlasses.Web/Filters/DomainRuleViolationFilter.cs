using DotGlasses.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DotGlasses.Web.Filters;

/// <summary>
/// The single place a DomainRuleViolationException becomes a response (ADR-0003). Registered
/// globally in Program.cs, so every controller action is covered whether or not its author
/// remembered the rule exists — that structural guarantee is the whole point of the filter, and
/// why per-controller try/catch blocks were removed rather than kept alongside it.
///
/// Two shapes, one message:
/// <list type="bullet">
/// <item>JSON API ([ApiController], detected via the framework's own IApiBehaviorMetadata rather
/// than a path prefix) — 400 ValidationProblemDetails keyed on the empty string, the same shape
/// a controller's own ValidationProblem() produces for a form-level error, so DotGlasses.App's
/// SyncService/FormErrors parse it with no special case.</item>
/// <item>Server-rendered MVC screens — POST-redirect-GET back to the screen the POST came from,
/// with the copy in TempData for _DomainRuleViolation.cshtml to render inline. A filter cannot
/// re-render an arbitrary view: ExceptionContext hands over no controller instance, and each
/// screen builds its view model in its own private helper. Redirecting is what a filter *can*
/// do for every screen alike, and it leaves the browser on a GET rather than on a POST URL that
/// re-submits on refresh.</item>
/// </list>
/// </summary>
public sealed class DomainRuleViolationFilter(
    ITempDataDictionaryFactory tempDataDictionaryFactory,
    IUrlHelperFactory urlHelperFactory) : IExceptionFilter
{
    /// <summary>TempData key carrying the rejection copy across the redirect — read by
    /// Views/Shared/_DomainRuleViolation.cshtml, which _Layout renders on every screen.</summary>
    public const string TempDataKey = "DomainRuleViolation";

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DomainRuleViolationException violation)
        {
            return;
        }

        context.Result = IsApiEndpoint(context)
            ? ApiResponse(violation)
            : ScreenResponse(context, violation);
        context.ExceptionHandled = true;
    }

    private static bool IsApiEndpoint(ExceptionContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<IApiBehaviorMetadata>().Any();

    private static IActionResult ApiResponse(DomainRuleViolationException violation)
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [string.Empty] = [violation.Message],
        })
        {
            Status = StatusCodes.Status400BadRequest,
        };

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private IActionResult ScreenResponse(ExceptionContext context, DomainRuleViolationException violation)
    {
        tempDataDictionaryFactory.GetTempData(context.HttpContext)[TempDataKey] = violation.Message;
        return new RedirectResult(ReturnUrl(context));
    }

    /// <summary>The screen the user was on, in preference order: the Referer (same-origin form
    /// POSTs carry the full URL, so filters/selection in the query string survive), then Index on
    /// the same controller, then the dashboard. Only the Referer's path+query is ever used, never
    /// the whole URL — a cross-origin Referer is discarded rather than redirected to.</summary>
    private string ReturnUrl(ExceptionContext context)
    {
        if (SameOriginReferer(context.HttpContext) is { } referer)
        {
            return referer;
        }

        var controllerName = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;
        var urlHelper = urlHelperFactory.GetUrlHelper(context);

        return (controllerName is null ? null : urlHelper.Action("Index", controllerName)) ?? "/";
    }

    private static string? SameOriginReferer(HttpContext httpContext)
    {
        if (!Uri.TryCreate(httpContext.Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer))
        {
            return null;
        }

        var host = httpContext.Request.Host;
        var sameOrigin = string.Equals(referer.Host, host.Host, StringComparison.OrdinalIgnoreCase)
            && (!host.Port.HasValue || referer.Port == host.Port.Value);

        return sameOrigin ? referer.PathAndQuery : null;
    }
}
