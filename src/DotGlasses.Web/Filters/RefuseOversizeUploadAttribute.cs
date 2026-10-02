using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DotGlasses.Web.Filters;

/// <summary>
/// Answers an upload that is bigger than the request size limit with a sentence on the screen it
/// came from, instead of a blank error page.
///
/// Without it the limit is hit while the antiforgery check is reading the form: the read fails,
/// the check reports "no token", and the admin gets an empty 400 with nothing to act on — which is
/// what a photo straight off a phone produced on the Reference Data screen. This runs first (an
/// authorization filter, ordered ahead of the antiforgery one), looks only at the declared
/// Content-Length, and never reads the body. [RequestSizeLimit] stays on the action as the
/// backstop for a request that declares no length.
///
/// Refusing before the antiforgery check gives nothing away and changes nothing: the request is
/// dropped unread, and all it gets is a redirect to a page that needs a sign-in to see.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RefuseOversizeUploadAttribute(long maxRequestBytes, string message) : Attribute, IAuthorizationFilter, IOrderedFilter
{
    /// <summary>Ahead of ValidateAntiForgeryToken, whose order is 1000.</summary>
    public int Order => 0;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.Request.ContentLength is not { } length || length <= maxRequestBytes)
        {
            return;
        }

        // Shown by _DomainRuleViolation.cshtml, like every other refusal on a server-rendered
        // screen. Saved by hand: a filter that answers this early never reaches the stage where
        // TempData is saved for it.
        var tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
        tempData[DomainRuleViolationFilter.TempDataKey] = message;
        tempData.Save();

        context.Result = new RedirectToActionResult("Index", controllerName: null, routeValues: null);
    }
}
