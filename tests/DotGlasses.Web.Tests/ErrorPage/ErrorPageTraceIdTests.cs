using System.Diagnostics;
using DotGlasses.Web.Controllers;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Tests.ErrorPage;

/// <summary>
/// The ID on the error page is only useful if it can be pasted into a log search. Serilog's
/// CompactJsonFormatter writes the trace ID alone as <c>@tr</c>; the full W3C traceparent
/// (<c>Activity.Id</c>) the page used to show appears in no log field verbatim.
/// </summary>
public class ErrorPageTraceIdTests
{
    [Fact]
    public void WithAW3CActivity_ShowsTheTraceIdSerilogWritesAsTr()
    {
        using var activity = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        var model = ErrorModelFor(traceIdentifier: "0HNABC123:00000001");

        Assert.Equal(activity.TraceId.ToHexString(), model.TraceId);
    }

    [Fact]
    public void WithNoActivity_FallsBackToTheTraceIdentifier()
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            var model = ErrorModelFor(traceIdentifier: "0HNABC123:00000001");

            Assert.Equal("0HNABC123:00000001", model.TraceId);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    private static ErrorViewModel ErrorModelFor(string traceIdentifier)
    {
        // Error() never touches the dashboard service.
        var controller = new HomeController(dashboardQueryService: null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier },
            },
        };

        var result = Assert.IsType<ViewResult>(controller.Error());
        return Assert.IsType<ErrorViewModel>(result.Model);
    }
}
