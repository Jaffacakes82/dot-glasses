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
    IEmailSender emailSender) : Controller
{
    private static readonly Dictionary<string, string> StatusColor = new()
    {
        ["Active"] = "var(--dot-green)",
        ["Invited"] = "var(--dot-yellow)",
        ["Suspended"] = "#cccccc",
    };

    private const int PageSize = 25;

    public async Task<IActionResult> Index(string? search, string? role, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["StatusColor"] = StatusColor;
        ViewData["AvailableOrgs"] = await organisationAdminService.ListAsync(cancellationToken);
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
            ViewData["AvailableOrgs"] = await organisationAdminService.ListAsync(cancellationToken);
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

    /// <summary>Server-side only for now: how the User Directory's edit form offers a role change
    /// is its own piece of work (map ticket 03). The rule it must enforce lives here already — the
    /// same all-assignments check as Suspend, since a role applies across the user's whole scope.
    /// An unknown role is refused by the service as a DomainRuleViolationException.</summary>
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

    private async Task<bool> CanManageAsync(UserAdminRow user) =>
        (await authorizationService.AuthorizeAsync(User, new UserAssignments(user.AssignmentPaths), AuthorizationPolicies.ManageUsersInScope)).Succeeded;

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
                canManage));
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
