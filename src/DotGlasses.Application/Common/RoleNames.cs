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

    /// <summary>The one deterministic rule for picking a user's role when Identity reports more
    /// than one for the account: alphabetical, ordinal. There's no real precedence to express with
    /// only two role names — the point of this method existing at all is that every caller that
    /// needs "the" role for a user (UserAccessLoader, computing access, and
    /// UserAdminService.ListAsync, rendering User Directory) goes through it instead of its own
    /// unordered FirstOrDefault(), so the directory can never show a different role from the one
    /// access was actually computed with.</summary>
    public static string? Primary(IEnumerable<string> roles) =>
        roles.OrderBy(r => r, StringComparer.Ordinal).FirstOrDefault();
}
