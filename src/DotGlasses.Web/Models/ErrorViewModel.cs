namespace DotGlasses.Web.Models;

public class ErrorViewModel
{
    public string? TraceId { get; set; }

    public bool ShowTraceId => !string.IsNullOrEmpty(TraceId);
}
