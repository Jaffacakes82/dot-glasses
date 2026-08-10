namespace DotGlasses.Application.Common;

/// <summary>
/// Two roles (2026-08-10 collapse — see CLAUDE.md's Access model section): Manager was removed
/// because it was functionally identical to Admin everywhere except ReferenceDataManage, which
/// already gated on org *level* (DGI), not role.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Admin, User];
}
