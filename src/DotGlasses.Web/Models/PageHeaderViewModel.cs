namespace DotGlasses.Web.Models;

/// <summary>Model for Views/Shared/_PageHeader.cshtml — the crumb/title/action header repeated at the top of every Admin Portal screen.</summary>
public class PageHeaderViewModel
{
    public required string Crumb { get; init; }
    public required string Title { get; init; }
    public string? ActionText { get; init; }
    public string? ActionController { get; init; }
    public string? ActionAction { get; init; }
}
