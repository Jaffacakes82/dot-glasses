using DotGlasses.Application.Common;
using DotGlasses.Application.Notifications;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Common;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class UserDirectoryController(
    IUserAdminService userAdminService,
    IOrganisationAdminService organisationAdminService,
    IAuthorizationService authorizationService,
    IValidator<InviteUserRequest> inviteValidator,
    IEmailSender emailSender,
    ICurrentUserContext currentUser) : Controller
{
    private static readonly Dictionary<string, string> StatusColor = new()
    {
        [UserStatuses.Active] = "var(--dot-green)",
        [UserStatuses.Invited] = "var(--dot-yellow)",
        [UserStatuses.Suspended] = "#cccccc",
    };

    /// <summary>TempData key for the line the directory shows after an Edit user save.</summary>
    public const string NoticeKey = "UserDirectoryNotice";

    private const int PageSize = 25;

    public async Task<IActionResult> Index(string? search, string? role, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["StatusColor"] = StatusColor;
        ViewData["InvitePicker"] = await BuildInvitePickerAsync([], cancellationToken);
        return View(await BuildUserListAsync(search, role, status, page, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(InviteUserRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await inviteValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            ViewData["StatusColor"] = StatusColor;
            ViewData["InvitePicker"] = await BuildInvitePickerAsync(request.OrgNodeIds, cancellationToken);
            return View(nameof(Index), await BuildUserListAsync(null, null, null, 1, cancellationToken));
        }

        if (!await CanAssignAllAsync(request.OrgNodeIds, cancellationToken))
        {
            return Forbid();
        }

        var result = await userAdminService.InviteAsync(request.Email, request.FullName, request.Role, request.OrgNodeIds, cancellationToken);
        var setPasswordUrl = Url.Action(nameof(AccountController.SetPassword), "Account", new { userId = result.UserId, token = result.PasswordResetToken }, Request.Scheme)!;

        await emailSender.SendPasswordSetupInviteAsync(result.Email, request.FullName, setPasswordUrl, cancellationToken);
        TempData["SetPasswordLink"] = setPasswordUrl;
        TempData["SetPasswordLinkFor"] = result.Email;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var target = await FindManageableUserAsync(id, cancellationToken);
        if (target is null)
        {
            return Forbid();
        }

        var token = await userAdminService.RegeneratePasswordResetTokenAsync(id, cancellationToken);
        var setPasswordUrl = Url.Action(nameof(AccountController.SetPassword), "Account", new { userId = id, token }, Request.Scheme)!;

        await emailSender.SendPasswordSetupInviteAsync(target.Email, target.DisplayName, setPasswordUrl, cancellationToken);
        TempData["SetPasswordLink"] = setPasswordUrl;
        TempData["SetPasswordLinkFor"] = target.Email;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        if (await FindManageableUserAsync(id, cancellationToken) is null)
        {
            return Forbid();
        }

        await userAdminService.SuspendAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsuspend(Guid id, CancellationToken cancellationToken)
    {
        if (await FindManageableUserAsync(id, cancellationToken) is null)
        {
            return Forbid();
        }

        await userAdminService.UnsuspendAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The Edit user page: role, org assignments and full name on one form. Reachable
    /// for any user the caller can see — what they may change on it is decided per field.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        // Every change the page offers is an Admin's; a User sees the directory, not this.
        if (currentUser.Role != RoleNames.Admin)
        {
            return Forbid();
        }

        var detail = await userAdminService.GetForEditAsync(id, cancellationToken);
        return detail is null ? Forbid() : View(await BuildEditViewModelAsync(detail, cancellationToken));
    }

    /// <summary>
    /// One Save. Only the differences between what the page was loaded with and what was posted
    /// are applied (UserEditPlan), and each kind of change keeps its own permission (ADR-0006): a
    /// role or name change acts on the user as a whole, so it needs every assignment in the
    /// caller's scope; adding or removing one assignment needs only that organisation in scope.
    /// A refusal from the service comes back to this page through DomainRuleViolationFilter.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditUserRequest request, CancellationToken cancellationToken)
    {
        var detail = currentUser.Role == RoleNames.Admin ? await userAdminService.GetForEditAsync(id, cancellationToken) : null;
        if (detail is null)
        {
            return Forbid();
        }

        var plan = UserEditPlan.Diff(
            request.LoadedFullName, request.FullName,
            request.LoadedRole, request.Role,
            request.LoadedOrgNodeIds, request.OrgNodeIds);

        if ((plan.ChangesRole || plan.ChangesName) && !await CanManageAsync(detail.AssignmentPaths))
        {
            return Forbid();
        }

        // Every organisation being added or removed has to be one the caller manages. An active
        // one comes from the scoped list; a deactivated one can only be removed, and is found
        // among the caller's own deactivated organisations.
        var manageable = (await organisationAdminService.ListAsync(cancellationToken))
            .Concat(await organisationAdminService.ListDeactivatedAsync(cancellationToken))
            .ToDictionary(o => o.Id);
        foreach (var orgNodeId in plan.OrgsToAdd.Concat(plan.OrgsToRemove))
        {
            if (!manageable.TryGetValue(orgNodeId, out var org)
                || !(await authorizationService.AuthorizeAsync(User, org.HierarchyPath, AuthorizationPolicies.ManageOrgInScope)).Succeeded)
            {
                return Forbid();
            }
        }

        if (!plan.IsEmpty)
        {
            await userAdminService.UpdateAsync(id, plan, cancellationToken);
        }

        var name = string.IsNullOrWhiteSpace(request.FullName) ? detail.Email : request.FullName.Trim();
        TempData[NoticeKey] = await userAdminService.GetForEditAsync(id, cancellationToken) is null
            ? $"Saved. {name} is no longer in your scope."
            : plan.IsEmpty ? $"Nothing was changed for {name}." : $"Saved changes to {name}.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<EditUserViewModel> BuildEditViewModelAsync(UserEditDetail detail, CancellationToken cancellationToken)
    {
        var allInScope = await CanManageAsync(detail.AssignmentPaths);
        var lockedReason = detail.IsSelf
            ? "You can't change your own role. Another admin must change it."
            : allInScope
                ? null
                : detail.AssignmentPaths.Count == 0
                    ? "Assign an organisation first; then the role and name can be changed."
                    : "This person also has organisations outside your scope, so their role and name can only be changed by an admin who manages all of them.";

        var visibleOrgs = await organisationAdminService.ListAsync(cancellationToken);
        var picker = OrgPickerViewModel.Build(
            "editUserOrgs",
            visibleOrgs,
            detail.Assignments.Select(a => a.OrgNodeId).ToHashSet(),
            detail.Assignments.Where(a => a.IsDeactivated));

        return new EditUserViewModel(
            detail,
            CanChangeRole: allInScope && !detail.IsSelf,
            RoleLockedReason: lockedReason,
            CanChangeName: allInScope,
            picker);
    }

    /// <summary>Kept beside the Edit page's save for callers that change a role alone. The rule it
    /// must enforce is the same all-assignments check as Suspend, since a role applies across the
    /// user's whole scope. An unknown role, or the caller's own, is refused by the service as a
    /// DomainRuleViolationException.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(Guid id, string role, CancellationToken cancellationToken)
    {
        if (await FindManageableUserAsync(id, cancellationToken) is null)
        {
            return Forbid();
        }

        await userAdminService.ChangeRoleAsync(id, role, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<OrgPickerViewModel> BuildInvitePickerAsync(IReadOnlyCollection<Guid> tickedIds, CancellationToken cancellationToken) =>
        OrgPickerViewModel.Build("inviteUserOrgs", await organisationAdminService.ListAsync(cancellationToken), tickedIds);

    /// <summary>An invite is checked as the user it will create: every org being assigned must be
    /// in the caller's scope — the same rule as acting on an existing user. An org the caller
    /// can't see at all fails it outright.</summary>
    private async Task<bool> CanAssignAllAsync(IReadOnlyCollection<Guid> orgNodeIds, CancellationToken cancellationToken)
    {
        var visibleOrgs = (await organisationAdminService.ListAsync(cancellationToken)).ToDictionary(o => o.Id);
        if (!orgNodeIds.All(visibleOrgs.ContainsKey))
        {
            return false;
        }

        var paths = orgNodeIds.Select(id => HierarchyPath.Parse(visibleOrgs[id].HierarchyPath)).ToList();
        var result = await authorizationService.AuthorizeAsync(User, new UserAssignments(paths), AuthorizationPolicies.ManageUsersInScope);
        return result.Succeeded;
    }

    /// <summary>Resource-based check against all of the target user's org assignments (ADR-0006)
    /// — re-checked here even though the view already hides the triggering form for a user who'd
    /// fail it; never trust the hidden-button UX alone. A user not listed at all (no assignment
    /// in the caller's scope) fails too.</summary>
    private async Task<UserAdminRow?> FindManageableUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var target = (await userAdminService.ListAsync(cancellationToken)).FirstOrDefault(u => u.Id == userId);
        if (target is null)
        {
            return null;
        }

        return await CanManageAsync(target) ? target : null;
    }

    private Task<bool> CanManageAsync(UserAdminRow user) => CanManageAsync(user.AssignmentPaths);

    private async Task<bool> CanManageAsync(IReadOnlyCollection<HierarchyPath> assignmentPaths) =>
        (await authorizationService.AuthorizeAsync(User, new UserAssignments(assignmentPaths), AuthorizationPolicies.ManageUsersInScope)).Succeeded;

    private async Task<UserDirectoryViewModel> BuildUserListAsync(string? search, string? role, string? status, int page, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        var pageResult = await userAdminService.ListPagedAsync(search, role, status, page, PageSize, cancellationToken);

        var users = new List<DirectoryUser>();
        foreach (var row in pageResult.Items)
        {
            var canManage = await CanManageAsync(row);
            users.Add(new DirectoryUser(
                row.Id,
                row.DisplayName,
                row.Role,
                row.OrgNames,
                row.Status,
                row.LastLoginUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
                row.SalesCount.ToString(),
                canManage,
                IsSelf: row.Id == currentUser.UserId,
                CanEdit: currentUser.Role == RoleNames.Admin));
        }

        return new UserDirectoryViewModel
        {
            Users = users,
            Search = search,
            Role = role,
            Status = status,
            Page = page,
            PageSize = PageSize,
            TotalCount = pageResult.TotalCount,
            TotalPages = pageResult.TotalPages,
        };
    }
}
