using DotGlasses.Domain.Common;

namespace DotGlasses.Application.Users;

/// <summary>The three states a user's account can be in — derived from Identity's own fields, not
/// a stored column (see <see cref="UserAdminRow"/>).</summary>
public static class UserStatuses
{
    public const string Active = "Active";
    public const string Invited = "Invited";
    public const string Suspended = "Suspended";
}

/// <summary>
/// What one Save on the Edit user page changes: only the differences between what the page was
/// loaded with and what was submitted. Applying a difference rather than the submitted state is
/// what keeps one admin's save from undoing another's — an assignment added since the page loaded
/// is in neither set, so it is left alone.
/// </summary>
public record UserEditPlan(string? NewFullName, string? NewRole, IReadOnlyList<Guid> OrgsToAdd, IReadOnlyList<Guid> OrgsToRemove)
{
    public bool ChangesRole => NewRole is not null;
    public bool ChangesName => NewFullName is not null;
    public bool IsEmpty => !ChangesRole && !ChangesName && OrgsToAdd.Count == 0 && OrgsToRemove.Count == 0;

    public static UserEditPlan Diff(
        string? loadedFullName, string? fullName,
        string? loadedRole, string? role,
        IEnumerable<Guid> loadedOrgIds, IEnumerable<Guid> orgIds)
    {
        var loaded = loadedOrgIds.ToHashSet();
        var submitted = orgIds.ToHashSet();
        var name = fullName?.Trim();

        return new UserEditPlan(
            NewFullName: string.Equals(name, loadedFullName?.Trim(), StringComparison.Ordinal) ? null : name,
            NewRole: string.Equals(role, loadedRole, StringComparison.Ordinal) ? null : role,
            OrgsToAdd: submitted.Except(loaded).ToList(),
            OrgsToRemove: loaded.Except(submitted).ToList());
    }
}

/// <summary>The rule for changing your own org assignments: adding one is always fine, and one
/// may be removed only when your scope afterwards is no smaller — the removed organisation sits
/// at or beneath another assignment you keep. Without it an admin could remove access nobody
/// beneath them can give back.</summary>
public static class OwnAssignments
{
    public static bool RemovalShrinksScope(HierarchyPath removed, IEnumerable<HierarchyPath> kept) =>
        !kept.Any(removed.IsSelfOrDescendantOf);
}
