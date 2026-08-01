namespace DotGlasses.Application.Common;

/// <summary>
/// [OPEN] The real permission matrix is pending the CEO conversation — these three role names
/// are the only thing currently agreed (see brief section 3.3).
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, User];
}
