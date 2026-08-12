namespace DotGlasses.Web.Models;

public record DirectoryUser(Guid Id, string Name, string Role, IReadOnlyList<string> Scope, string Status, string LastLogin, string Sales, bool CanManage);

public class UserDirectoryViewModel
{
    public required IReadOnlyList<DirectoryUser> Users { get; init; }
    public string? Search { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
